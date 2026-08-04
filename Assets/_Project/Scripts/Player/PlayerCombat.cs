

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TopDownShooter.Inventory;

namespace TopDownShooter.Combat
{
    /// <summary>
    /// Contexto de Strategy: recibe la entrada de ataque y la delega en el
    /// <see cref="IWeapon"/> activo. Se suscribe a <see cref="Player.PlayerInventory.OnWeaponChanged"/>
    /// e instanciar dinámicamente el hijo de lógica de arma correcto en tiempo de ejecución.
    /// Adjuntar esto junto a <c>PlayerController3D</c> y <c>PlayerInventory</c>
    /// en el GameObject raíz de Player.
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  PARÁMETROS EXPUESTOS EN EL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("Banderas – Control en tiempo de ejecución")]
        [Tooltip("Establecer en falso para evitar cualquier entrada de ataque (por ejemplo, en menús, cinemáticas).")]
        [SerializeField] private bool canAttack = true;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO PRIVADO
        // ─────────────────────────────────────────────────────────────────────

        // La estrategia de arma actualmente equipada — solo IWeapon; sin tipo concreto.
        private IWeapon _equippedWeapon;

        // El GameObject hijo activo que posee el MonoBehaviour del arma.
        // Se mantiene para que podamos destruirlo limpiamente al cambiar (activa OnDestroy
        // en el arma, lo que elimina su ObjectPool).
        private GameObject _liveWeaponObject;

        // El plano SO del arma actualmente equipada.
        // Almacenado en caché aquí (no se vuelve a leer del inventario en cada frame) para que la puerta de recursos
        // en OnAttack tenga acceso O(1) sin sobrecarga de GetComponent.
        private WeaponDataSO _currentWeaponData;

        // Referencia almacenada en caché a PlayerInventory en el mismo GameObject.
        private Player.PlayerInventory _playerInventory;

        // Referencia almacenada en caché al gestor de recursos — utilizada para controlar ataques.
        // Adquirida una vez en Awake; nulo = no hay sistema de recursos presente (ataques gratuitos).
        private Player.PlayerResourceComponent _resourceComponent;

        // Referencia almacenada en caché al Animator del jugador (en el hijo del modelo 3D).
        private Animator _animator;

        // ─────────────────────────────────────────────────────────────────────
        //  PROPIEDADES PÚBLICAS DE SOLO LECTURA (para consultas de FSM / HUD / logros)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Devuelve la estrategia del arma actualmente equipada. Nulo = con las manos vacías.</summary>
        public IWeapon CurrentWeapon => _equippedWeapon;

        /// <summary>Verdadero cuando la entrada de ataque está permitida globalmente.</summary>
        public bool CanAttack
        {
            get => canAttack;
            set => canAttack = value;   // FSM puede desactivar ataques durante animaciones
        }

        /// <summary>
        /// Inyecta multiplicadores de combate directamente al arma equipada
        /// (invocado por modificadores del Dungeon Master).
        /// </summary>
        public void SetWeaponDungeonMultipliers(float damageMultiplier, float cooldownMultiplier)
        {
            if (_equippedWeapon != null)
            {
                _equippedWeapon.SetDungeonMultipliers(damageMultiplier, cooldownMultiplier);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EVENTOS ESTÁTICOS (suscritos por HUD / otra UI sin necesidad de una referencia)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Se activa cuando un ataque es rechazado específicamente porque el Maná es insuficiente.
        /// Estático para que el HUD pueda suscribirse sin mantener una referencia a este componente.
        /// </summary>
        public static event Action OnManaDepleted;

        /// <summary>
        /// Se activa cuando un ataque es rechazado específicamente porque la Energía es insuficiente.
        /// Estático para que el HUD pueda suscribirse sin mantener una referencia a este componente.
        /// </summary>
        public static event Action OnEnergyDepleted;

        // ─────────────────────────────────────────────────────────────────────
        //  CICLO DE VIDA DE UNITY
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            SubscribeToInventory();

            _animator = GetComponentInChildren<Animator>();
            if (_animator == null)
            {
                Debug.LogWarning("[PlayerCombat] No Animator found in children. Attack animations will not play.", this);
            }

            // Dependencia opcional — si no está presente, todas las armas disparan gratis.
            if (!TryGetComponent(out _resourceComponent))
            {
                Debug.LogWarning("[PlayerCombat] No PlayerResourceComponent found on this " +
                                 "GameObject. Weapons will fire without resource cost.", this);
            }
        }

        private void OnDestroy()
        {
            // Desuscribirse siempre para evitar llamadas de delegados obsoletas después de que este componente
            // sea destruido (por ejemplo, descarga de la escena, muerte del jugador).
            if (_playerInventory != null)
                _playerInventory.OnWeaponChanged -= HandleWeaponChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INICIALIZACIÓN
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resuelve el <see cref="Player.PlayerInventory"/> en este GameObject
        /// y se suscribe a <see cref="Player.PlayerInventory.OnWeaponChanged"/>.
        /// Registra un error claro si falta el componente en lugar de fallar más tarde.
        /// </summary>
        private void SubscribeToInventory()
        {
            if (!TryGetComponent(out _playerInventory))
            {
                Debug.LogError("[PlayerCombat] No PlayerInventory found on this GameObject. " +
                               "Attach PlayerInventory to the same root as PlayerCombat. " +
                               "Attack input will be silently ignored until resolved.", this);
                return;
            }

            _playerInventory.OnWeaponChanged += HandleWeaponChanged;
            Debug.Log("[PlayerCombat] Subscribed to PlayerInventory.OnWeaponChanged. " +
                      "Player starts empty-handed.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MANEJADOR DE CAMBIO DE ARMA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Suscriptor de <see cref="Player.PlayerInventory.OnWeaponChanged"/>.
        /// Destruye el hijo de lógica de arma antiguo, luego instancia y configura
        /// el nuevo a partir del <see cref="WeaponDataSO"/>.
        ///
        /// <para>
        /// ALGORITMO:
        /// <list type="number">
        ///   <item>Desmantelar: destruir el hijo antiguo → su OnDestroy elimina el pool.</item>
        ///   <item>Si newWeapon es nulo (ranura vaciada), detenerse aquí — el jugador está con las manos vacías.</item>
        ///   <item>Guardia: verificar que WeaponLogicPrefab esté asignado en el SO.</item>
        ///   <item>Instanciar el prefab de lógica como hijo de este transform Player.</item>
        ///   <item>Cast a IWeapon. Registrar error y limpiar si el cast falla.</item>
        ///   <item>Opcional: si también es IWeaponConfigurable, llamar a Configure(newWeapon).</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="newWeapon">
        /// El <see cref="WeaponDataSO"/> del arma recién recogida,
        /// o <c>null</c> si la ranura del arma fue vaciada.
        /// </param>
        private void HandleWeaponChanged(WeaponDataSO newWeapon)
        {
            // Guardar el SO en caché para que OnAttack pueda leer los costos de recursos sin ninguna
            // llamada a GetComponent. Debe actualizarse ANTES de que TearDown borre el antiguo.
            _currentWeaponData = newWeapon;

            // ── Paso 1: Desmantelar el arma antigua ───────────────────────────
            TearDownCurrentWeapon();

            // ── Paso 2: Comprobación de nulos — el jugador está ahora con las manos vacías ────────────
            if (newWeapon == null)
            {
                Debug.Log("[PlayerCombat] Weapon slot cleared. Player is empty-handed.");
                return;
            }

            // ── Paso 3: Validar la referencia al prefab de lógica del SO ───────────
            if (newWeapon.WeaponLogicPrefab == null)
            {
                Debug.LogError($"[PlayerCombat] WeaponDataSO '{newWeapon.DisplayName}' has no " +
                               "WeaponLogicPrefab assigned. Cannot equip this weapon. " +
                               "Assign an IWeapon MonoBehaviour prefab in the SO.", this);
                return;
            }

            // ── Paso 4: Instanciar como hijo de este Player ──────────────
            // Generar como hijo significa que el arma hereda la posición y rotación del jugador
            // en espacio del mundo automáticamente, por lo que los Transforms de fire-point
            // siguen siendo correctos sin ninguna sincronización manual.
            _liveWeaponObject = Instantiate(
                newWeapon.WeaponLogicPrefab.gameObject,
                transform.position,
                transform.rotation,
                transform);   // ← padre = transform de este Player

            // ── Paso 5: Hacer cast del MonoBehaviour raíz a IWeapon ────────────
            // GetComponent<IWeapon>() encuentra el primer IWeapon en la raíz o
            // en cualquier hijo. Usamos el tipo de MonoBehaviour raíz para el cast ya que
            // WeaponLogicPrefab está garantizado que está en la raíz.
            _equippedWeapon = _liveWeaponObject.GetComponent<IWeapon>();

            if (_equippedWeapon == null)
            {
                Debug.LogError($"[PlayerCombat] The instantiated prefab for '{newWeapon.DisplayName}' " +
                               "does not have an IWeapon component. " +
                               "Ensure the prefab's root script implements IWeapon.", this);

                // Limpiar el hijo huérfano para evitar un GameObject flotante.
                Destroy(_liveWeaponObject);
                _liveWeaponObject = null;
                return;
            }

            // ── Paso 6: Inyección de estadísticas opcional a través de IWeaponConfigurable ────
            // Este es el ÚNICO lugar donde los datos del SO fluyen hacia la lógica.
            // El arma lee lo que necesita; PlayerCombat permanece agnóstico a los datos.
            if (_equippedWeapon is IWeaponConfigurable configurable)
            {
                configurable.Configure(newWeapon);
                Debug.Log($"[PlayerCombat] Configured '{newWeapon.DisplayName}' via IWeaponConfigurable.");
            }
            else
            {
                Debug.Log($"[PlayerCombat] '{newWeapon.DisplayName}' does not implement " +
                          "IWeaponConfigurable — using its hardcoded Inspector values.");
            }

            Debug.Log($"[PlayerCombat] Weapon equipped: '{newWeapon.DisplayName}' " +
                      $"({_equippedWeapon.GetType().Name}).");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TEARDOWN HELPER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Borra el estado del arma actual y destruye el GameObject hijo activo.
        /// Destruir al hijo activa el <c>MagicWand.OnDestroy</c> (o el OnDestroy
        /// de cualquier arma), lo que limpia el ObjectPool de forma correcta.
        /// </summary>
        private void TearDownCurrentWeapon()
        {
            if (_liveWeaponObject != null)
            {
                string oldName = _equippedWeapon?.GetType().Name ?? "Unknown";
                Destroy(_liveWeaponObject);
                _liveWeaponObject = null;
                Debug.Log($"[PlayerCombat] Destroyed old weapon logic: '{oldName}'.");
            }

            _equippedWeapon = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  NEW INPUT SYSTEM – MESSAGE CALLBACK
        //  (Called automatically by PlayerInput in "Send Messages" mode)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Recibe la acción Attack (Botón) del mapa de acciones Player.
        ///
        /// CONTRATO DE NOMBRE DE MÉTODO:
        /// El nombre del método "OnAttack" debe coincidir exactamente con el nombre de la acción de entrada
        /// "Attack" (PlayerInput antepone "On" y llama al método a través de reflexión).
        ///
        /// FLUJO:
        /// [Botón izquierdo del ratón] → PlayerInput (Send Messages) → OnAttack()
        ///    → Compuerta CanAttack → _equippedWeapon.ExecuteAttack()
        ///    → MagicWand.ExecuteAttack() → compuerta de cadencia de fuego → pool.Get()
        ///    → El proyectil se lanza hacia el objetivo del ratón
        /// </summary>
        public void OnAttack(InputValue value)
        {
            // Solo reaccionar al evento de presión, no a la liberación.
            if (!value.isPressed) return;

            // ── Compuerta de estado del juego ───────────────────────────────────────────────
            // Bloquear todos los disparos fuera del estado de juego (muerte, victoria, pausa,
            // menús). Esto evita que la animación de muerte/victoria sea interrumpida por
            // un clic perdido que registre el disparo de un arma.
            if (GameManager.Instance == null ||
                GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            // Compuerta global — los sistemas FSM o de cinemáticas pueden desactivar ataques de forma externa.
            if (!canAttack) return;

            // Operación nula segura cuando se está con las manos vacías.
            if (_equippedWeapon == null) return;

            // ── Compuerta de recursos (Parte 4) ────────────────────────────────────────
            // Leer el costo del SO en caché e intentar consumirlo.
            // Si el componente falta, tratamos todas las armas como gratuitas.
            if (_resourceComponent != null && _currentWeaponData != null)
            {
                switch (_currentWeaponData.ResourceType)
                {
                    case WeaponResourceType.Mana:
                        if (!_resourceComponent.TryConsumeMana(_currentWeaponData.ResourceCost))
                        {
                            Debug.Log("[PlayerCombat] Not enough Mana to attack. " +
                                      $"Required: {_currentWeaponData.ResourceCost}.");
                            OnManaDepleted?.Invoke();
                            return;   // Abort — do NOT fire
                        }
                        break;

                    case WeaponResourceType.Energy:
                        if (!_resourceComponent.TryConsumeEnergy(_currentWeaponData.ResourceCost))
                        {
                            Debug.Log("[PlayerCombat] Not enough Energy to attack. " +
                                      $"Required: {_currentWeaponData.ResourceCost}.");
                            OnEnergyDepleted?.Invoke();
                            return;   // Abort — do NOT fire
                        }
                        break;

                        // WeaponResourceType.None — cae por defecto; sin costo, sin compuerta.
                }
            }

            if (_animator != null)
                _animator.SetTrigger("Attack");

            // Delegar por completo a la estrategia — PlayerCombat no sabe CÓMO.
            _equippedWeapon.ExecuteAttack();
        }
    }
}