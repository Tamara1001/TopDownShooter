

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TopDownShooter.Inventory;
using TopDownShooter.Interaction;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Inventario fijo de tres ranuras para el personaje del jugador.
    /// Maneja la recogida (E), soltar al intercambiar, y uso de consumibles (Q).
    /// Adjunte este MonoBehaviour al GameObject del Jugador junto con
    /// <see cref="PlayerController3D"/> y <see cref="Combat.PlayerCombat"/>.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("Pickup Detection")]
        [Tooltip("Radio en espacio de mundo de la OverlapSphere utilizada para detectar objetos ItemPickup cercanos cuando el jugador presiona Interactuar (E). Debe ser ligeramente mayor que los radios de los SphereCollider de los objetos.")]
        [SerializeField] private float _pickupRadius = 1.5f;

        [Tooltip("LayerMask para la(s) capa(s) que contienen colisionadores ItemPickup. Asigne la capa 'Pickup' para un mejor rendimiento — la esfera de superposición omite por completo todas las demás capas.")]
        [SerializeField] private LayerMask _pickupLayerMask;

        [Header("World Interaction")]
        [Tooltip("LayerMask para objetos del mundo que implementan IWorldInteractable (por ejemplo, VictoryDoor). Asigne la capa 'Interactable'. Se comprueba ANTES de la esfera de recogida, para que las puertas tengan prioridad sobre los objetos del suelo.")]
        [SerializeField] private LayerMask _interactableLayerMask;

        [Header("Drop Offset")]
        [Tooltip("Desplazamiento en espacio local desde la posición del jugador donde se instancian los objetos soltados. Evita que los objetos aparezcan dentro del colisionador del jugador. (0, 0, 0.8) = justo en frente del jugador.")]
        [SerializeField] private Vector3 _dropOffset = new Vector3(0f, 0f, 0.8f);

        [Header("Buffer Settings")]
        [Tooltip("Número máximo de colisionadores que registra la OverlapSphere por llamada. Auméntelo solo si muchos objetos pueden superponerse simultáneamente.")]
        [SerializeField] private int _overlapBufferSize = 8;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO DE LAS RANURAS DE INVENTARIO  (privado — nunca expuesto como mutable)
        // ─────────────────────────────────────────────────────────────────────

        // Cada ranura contiene la plantilla DATA (SO) del objeto equipado actualmente.
        // Nulo significa que la ranura está vacía.
        private WeaponDataSO      _currentWeapon;
        private RelicDataSO       _currentRelic;
        private ConsumableDataSO  _currentConsumable;

        // ─────────────────────────────────────────────────────────────────────
        //  EVENTOS  (Patrón Observador — el HUD y otros sistemas se suscriben aquí)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Se activa cada vez que cambia la ranura de Arma (recogida o vaciado).
        /// El argumento es el nuevo <see cref="WeaponDataSO"/>, o <c>null</c> si se vacía.
        /// </summary>
        public event Action<WeaponDataSO>     OnWeaponChanged;

        /// <summary>
        /// Se activa cada vez que cambia la ranura de Reliquia (recogida o vaciado).
        /// El argumento es el nuevo <see cref="RelicDataSO"/>, o <c>null</c> si se vacía.
        /// </summary>
        public event Action<RelicDataSO>      OnRelicChanged;

        /// <summary>
        /// Se activa cada vez que cambia la ranura de Consumible (recogida, uso o vaciado).
        /// El argumento es el nuevo <see cref="ConsumableDataSO"/>, o <c>null</c> si se vacía.
        /// </summary>
        public event Action<ConsumableDataSO> OnConsumableChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  PROPIEDADES PÚBLICAS DE SÓLO LECTURA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Datos del arma equipada actualmente. Nulo si la ranura está vacía.</summary>
        public WeaponDataSO     CurrentWeapon     => _currentWeapon;

        /// <summary>Datos de la reliquia equipada actualmente. Nulo si la ranura está vacía.</summary>
        public RelicDataSO      CurrentRelic      => _currentRelic;

        /// <summary>Datos del consumible equipado actualmente. Nulo si la ranura está vacía.</summary>
        public ConsumableDataSO CurrentConsumable => _currentConsumable;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO DE EJECUCIÓN PRIVADO
        // ─────────────────────────────────────────────────────────────────────

        // Buffer de superposición preasignado — cero asignaciones durante la recogida.
        private Collider[] _overlapBuffer;

        // Transform guardado en caché para consultas de posición dentro de bucles estrechos.
        private Transform _transform;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _transform     = transform;
            _overlapBuffer = new Collider[_overlapBufferSize];

            ValidateSetup();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DEVOLUCIONES DE LLAMADA DEL SISTEMA DE ENTRADAS  (Send Messages — llamadas por PlayerInput)
        //
        //  CONTRATO DE NOMENCLATURA:
        //  Nombre del método = "On" + nombre exacto de la Acción en CharacterActions.inputactions
        //  Estos se llaman mediante reflexión — cualquier error tipográfico rompe silenciosamente la vinculación.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Recibe la acción "Interact" (tecla E) de PlayerInput.
        /// Orden de prioridad:
        ///   1. Busca un <see cref="IWorldInteractable"/> (puertas, interruptores) a través de
        ///      <see cref="_interactableLayerMask"/> — estos tienen prioridad sobre los objetos.
        ///   2. Vuelve a <see cref="TryPickupNearestItem"/> para recogidas en el suelo.
        /// </summary>
        public void OnInteract(InputValue value)
        {
            // Solo responder al evento de presión, no al de liberación.
            if (!value.isPressed) return;

            // ── Prioridad 1: Objetos interactuables del mundo (puertas, interruptores, NPCs) ──
            if (TryWorldInteract()) return;

            // ── Prioridad 2: Comportamiento alternativo de recogida de objetos ──────────────
            TryPickupNearestItem();
        }

        /// <summary>
        /// Recibe la acción "Consume" (tecla Q) de PlayerInput.
        /// Utiliza el consumible equipado actualmente si hay uno.
        /// Los objetos de misión (<see cref="ConsumableDataSO.IsQuestItem"/> == true) están
        /// bloqueados intencionalmente — deben usarse mediante el flujo de interacción de la tecla E.
        /// </summary>
        public void OnConsume(InputValue value)
        {
            if (!value.isPressed) return;

            if (_currentConsumable == null)
            {
                Debug.Log("[PlayerInventory] OnConsume: La ranura de consumibles está vacía.");
                return;
            }

            // Guardia: los objetos de misión (por ejemplo, llaves) no se pueden consumir con Q.
            if (_currentConsumable.IsQuestItem)
            {
                Debug.Log($"[PlayerInventory] No se pueden consumir objetos de misión. " +
                          $"'{_currentConsumable.DisplayName}' debe usarse " +
                          "interactuando (E) con el objeto del mundo apropiado.");
                return;
            }

            ConsumeCurrentItem();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INTERACCIÓN CON EL MUNDO
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Realiza un OverlapSphere usando <see cref="_interactableLayerMask"/> y
        /// llama a <see cref="IWorldInteractable.Interact"/> en el primer componente coincidente
        /// que se encuentre.
        /// </summary>
        /// <returns><c>true</c> si se encontró y llamó a un interactuable del mundo; <c>false</c> en caso contrario.</returns>
        private bool TryWorldInteract()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                _transform.position,
                _pickupRadius,
                _overlapBuffer,
                _interactableLayerMask);

            if (hitCount == 0) return false;

            IWorldInteractable interactable = null;

            for (int i = 0; i < hitCount; i++)
            {
                if (_overlapBuffer[i].TryGetComponent<IWorldInteractable>(out IWorldInteractable found))
                {
                    interactable = found;
                    break; // Usar el primero válido que se encuentre.
                }
            }

            // Limpiar referencias del buffer para evitar que el GC retenga objetos muertos.
            for (int i = 0; i < hitCount; i++)
                _overlapBuffer[i] = null;

            if (interactable == null) return false;

            interactable.Interact(this);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  LÓGICA DE RECOGIDA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Encuentra el <see cref="ItemPickup"/> más cercano dentro de <see cref="_pickupRadius"/>,
        /// lee su <see cref="ItemDataSO"/>, suelta cualquier objeto existente en la ranura correspondiente
        /// y asigna el nuevo.
        ///
        /// ALGORITMO:
        ///   1. Physics.OverlapSphereNonAlloc → llena _overlapBuffer, cero asignaciones.
        ///   2. Iterar colisiones, TryGetComponent para encontrar instancias de ItemPickup.
        ///   3. Seguir el más cercano por sqrMagnitude (evita sqrt hasta la selección).
        ///   4. Búsqueda de patrones en el subtipo de ItemDataSO para determinar la ranura de destino.
        ///   5. Intercambio atómico: soltar antiguo → asignar nuevo → DestroyPickup.
        /// </summary>
        private void TryPickupNearestItem()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                _transform.position,
                _pickupRadius,
                _overlapBuffer,
                _pickupLayerMask);

            if (hitCount == 0) return;

            // ── Encontrar el ItemPickup más cercano en los resultados ─────────────────
            ItemPickup nearest        = null;
            float      nearestSqrDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (!_overlapBuffer[i].TryGetComponent<ItemPickup>(out ItemPickup pickup))
                    continue;

                float sqrDist = (_overlapBuffer[i].transform.position
                                 - _transform.position).sqrMagnitude;

                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest        = pickup;
                }
            }

            // Limpiar referencias del buffer para evitar que el GC retenga objetos muertos.
            for (int i = 0; i < hitCount; i++)
                _overlapBuffer[i] = null;

            if (nearest == null) return;

            // ── Leer los datos del objeto y enrutar a la ranura correcta ────────
            ItemDataSO itemData = nearest.GetItemData();
            if (itemData == null)
            {
                Debug.LogWarning("[PlayerInventory] Found an ItemPickup with no ItemDataSO assigned. " +
                                 "Configure the pickup in the Inspector.", nearest);
                return;
            }

            ExecutePickup(itemData, nearest);
        }

        /// <summary>
        /// Realiza el intercambio atómico de ranuras para el objeto dado.
        /// Utiliza coincidencia de patrones de C# (is T t) para determinar la ranura —
        /// sin comparaciones de cadenas, seguro en tiempo de compilación, cero asignaciones.
        /// </summary>
        /// <param name="itemData">La plantilla del objeto que se está recogiendo.</param>
        /// <param name="pickup">El objeto del mundo que se destruirá después de la recogida.</param>
        private void ExecutePickup(ItemDataSO itemData, ItemPickup pickup)
        {
            if (itemData is WeaponDataSO weapon)
            {
                SwapWeapon(weapon);
            }
            else if (itemData is RelicDataSO relic)
            {
                SwapRelic(relic);
            }
            else if (itemData is ConsumableDataSO consumable)
            {
                SwapConsumable(consumable);
            }
            else
            {
                // A prueba de futuro: subtipo desconocido. Registrar pero no fallar.
                Debug.LogWarning($"[PlayerInventory] Unknown ItemDataSO subtype: " +
                                 $"'{itemData.GetType().Name}'. Add a new slot or handler.", this);
                return; // NO destruir la recogida si no podemos procesarla.
            }

            // La ranura está ahora actualizada. Eliminar el objeto del mundo.
            pickup.DestroyPickup();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  AYUDANTES DE INTERCAMBIO DE RANURAS
        //  Cada ayudante: (1) suelta el existente, (2) asigna el nuevo, (3) activa el evento.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Suelta el arma actual (si la hay) y equipa la nueva.
        /// </summary>
        private void SwapWeapon(WeaponDataSO newWeapon)
        {
            if (_currentWeapon != null)
            {
                DropItem(_currentWeapon);
                Debug.Log($"[PlayerInventory] Dropped weapon: '{_currentWeapon.DisplayName}'.");
            }

            _currentWeapon = newWeapon;
            Debug.Log($"[PlayerInventory] Equipped weapon: '{_currentWeapon.DisplayName}'.");

            // Notificar al HUD, PlayerCombat y a cualquier otro observador.
            OnWeaponChanged?.Invoke(_currentWeapon);
        }

        /// <summary>
        /// Suelta la reliquia actual (si la hay) y equipa la nueva.
        /// </summary>
        private void SwapRelic(RelicDataSO newRelic)
        {
            if (_currentRelic != null)
            {
                DropItem(_currentRelic);
                Debug.Log($"[PlayerInventory] Dropped relic: '{_currentRelic.DisplayName}'.");
            }

            _currentRelic = newRelic;
            Debug.Log($"[PlayerInventory] Equipped relic: '{_currentRelic.DisplayName}'.");

            OnRelicChanged?.Invoke(_currentRelic);
        }

        /// <summary>
        /// Suelta el consumible actual (si lo hay) y recoge el nuevo.
        /// </summary>
        private void SwapConsumable(ConsumableDataSO newConsumable)
        {
            if (_currentConsumable != null)
            {
                DropItem(_currentConsumable);
                Debug.Log($"[PlayerInventory] Dropped consumable: '{_currentConsumable.DisplayName}'.");
            }

            _currentConsumable = newConsumable;
            Debug.Log($"[PlayerInventory] Picked up consumable: '{_currentConsumable.DisplayName}'.");

            OnConsumableChanged?.Invoke(_currentConsumable);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  LÓGICA DE SOLTAR (DROP)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Instancia el <see cref="ItemDataSO.DropPrefab"/> del objeto en la
        /// posición del jugador (con un desplazamiento local configurable) para que el objeto
        /// soltado pueda ser recogido de nuevo inmediatamente.
        ///
        /// El soltado utiliza <see cref="_dropOffset"/> en espacio local (relativo al jugador)
        /// para que el objeto siempre caiga en frente del jugador independientemente de su rotación.
        /// </summary>
        /// <param name="item">La plantilla del objeto cuyo DropPrefab se instanciará.</param>
        private void DropItem(ItemDataSO item)
        {
            if (item.DropPrefab == null)
            {
                Debug.LogWarning($"[PlayerInventory] '{item.DisplayName}' has no DropPrefab assigned. " +
                                 "The item is lost permanently. Assign a DropPrefab in the SO.", this);
                return;
            }

            // Convertir el desplazamiento local al espacio de mundo usando la rotación actual del jugador.
            Vector3 worldDropPosition = _transform.TransformPoint(_dropOffset);

            // Instanciar en la posición Y del jugador para evitar que los objetos floten o se hundan.
            worldDropPosition.y = _transform.position.y;

            Instantiate(item.DropPrefab, worldDropPosition, Quaternion.identity);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  LÓGICA DE CONSUMIR
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Activa el objeto consumible equipado actualmente y vacía la ranura.
        ///
        /// Parte 1 CÓDIGO VACÍO: Registra la acción y activa <see cref="OnConsumableChanged"/>.
        ///
        /// Parte 2 EXPANSIÓN:
        /// Lee <c>_currentConsumable.HealAmount</c> y llama a
        /// <c>GetComponent&lt;HealthComponent&gt;().Heal(healAmount)</c>.
        /// Lee <c>_currentConsumable.SpeedBoostMultiplier</c> y llama a
        /// <c>PlayerStats.ApplyTemporarySpeedBoost(...)</c>.
        /// Enruta los VFX y SFX a través de los administradores dedicados.
        /// </summary>
        private void ConsumeCurrentItem()
        {
            Debug.Log($"[PlayerInventory] Consuming '{_currentConsumable.DisplayName}'.");

            // Aplicar curación si el jugador tiene un HealthComponent.
            if (TryGetComponent<HealthComponent>(out var health))
            {
                health.Heal(_currentConsumable.HealAmount);
                Debug.Log($"[PlayerInventory] Healed {_currentConsumable.HealAmount} HP.");
            }
            else
            {
                Debug.LogWarning("[PlayerInventory] No HealthComponent found on this GameObject. " +
                                 "Healing effect was skipped.", this);
            }

            // Aplicar un aumento de velocidad temporal si este consumible define uno.
            // Ambas comprobaciones deben pasar: duración > 0 (efecto temporal) Y multiplicador > 0 (tipo velocidad).
            // Las pociones de curación simples (EffectDuration == 0) se omiten intencionalmente.
            if (TryGetComponent<PlayerStatsComponent>(out var stats))
            {
                if (_currentConsumable.EffectDuration > 0f && _currentConsumable.SpeedBoostMultiplier > 0f)
                {
                    stats.ApplyTemporarySpeedBoost(
                        _currentConsumable.SpeedBoostMultiplier,
                        _currentConsumable.EffectDuration);
                }
            }

            // Vaciar la ranura después del uso — los consumibles son de un solo uso.
            _currentConsumable = null;
            OnConsumableChanged?.Invoke(null);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDACIÓN
        // ─────────────────────────────────────────────────────────────────────

        private void ValidateSetup()
        {
            if (_pickupLayerMask.value == 0)
            {
                Debug.LogWarning("[PlayerInventory] Pickup LayerMask is empty (Everything). " +
                                 "The OverlapSphere will test ALL colliders in the scene. " +
                                 "Assign the 'Pickup' layer for better performance.", this);
            }

            if (_interactableLayerMask.value == 0)
            {
                Debug.LogWarning("[PlayerInventory] Interactable LayerMask is empty. " +
                                 "World interactables (doors, switches) will not be detected. " +
                                 "Assign the 'Interactable' layer in the Inspector.", this);
            }

            if (_overlapBufferSize <= 0)
            {
                Debug.LogError("[PlayerInventory] Overlap buffer size must be > 0. Defaulting to 8.", this);
                _overlapBufferSize = 8;
                _overlapBuffer = new Collider[_overlapBufferSize];
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GIZMOS EN EDITOR
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Radio de recogida — verde
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.12f);
            Gizmos.DrawSphere(transform.position, _pickupRadius);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, _pickupRadius);

            // Radio interactuable — cian (mismo radio, diferente color)
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.12f);
            Gizmos.DrawSphere(transform.position, _pickupRadius);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _pickupRadius);

            // Posición de desplazamiento de caída — punto naranja
            Vector3 dropWorld = transform.TransformPoint(_dropOffset);
            dropWorld.y = transform.position.y;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
            Gizmos.DrawSphere(dropWorld, 0.08f);
            Gizmos.DrawLine(transform.position, dropWorld);
        }
#endif
    }
}
