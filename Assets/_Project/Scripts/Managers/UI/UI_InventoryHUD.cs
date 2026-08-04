
using UnityEngine;
using TopDownShooter.Inventory;
using TopDownShooter.Player;

namespace TopDownShooter.UI
{
    /// <summary>
    /// Escucha los eventos de ranuras de <see cref="PlayerInventory"/> y enruta
    /// el <see cref="ItemDataSO"/> actualizado a la ranura <see cref="UI_InventorySlot"/> correspondiente.
    /// Maneja el registro dinámico del jugador para que el HUD funcione independientemente del
    /// orden de carga de la escena o la reaparición del jugador.
    /// </summary>
    public sealed class UI_InventoryHUD : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("Ranuras de Inventario")]
        [Tooltip("Componente de ranura para la ranura de inventario de Arma.")]
        [SerializeField] private UI_InventorySlot _weaponSlot;

        [Tooltip("Componente de ranura para la ranura de inventario de Reliquia.")]
        [SerializeField] private UI_InventorySlot _relicSlot;

        [Tooltip("Componente de ranura para la ranura de inventario de Consumible.")]
        [SerializeField] private UI_InventorySlot _consumableSlot;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO PRIVADO EN TIEMPO DE EJECUCIÓN
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// El inventario actualmente vinculado. Almacenado en caché para que pueda ser
        /// desvinculado de forma segura antes de volver a vincularse tras reaparecer o recargar la escena.
        /// </summary>
        private PlayerInventory _boundInventory;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Suscripción de Nivel 1: saber el instante en que existe un nuevo Transform del jugador.
            GameManager.OnPlayerRegistered += OnPlayerRegistered;
        }

        private void Start()
        {
            ValidateSlots();

            // Si el jugador fue registrado antes de que esta interfaz se habilitara (por ejemplo, el Awake
            // del jugador se ejecuta antes del Start del HUD), vincular inmediatamente sin esperar
            // al evento OnPlayerRegistered.
            if (GameManager.Instance != null && GameManager.Instance.PlayerTransform != null)
            {
                BindToPlayer(GameManager.Instance.PlayerTransform);
            }
        }

        private void OnDisable()
        {
            // Desuscripción de Nivel 1. Es seguro llamarlo incluso si OnEnable nunca se disparó
            // porque el operador delegate -= en C# sobre un método no suscrito es una operación nula.
            GameManager.OnPlayerRegistered -= OnPlayerRegistered;
        }

        private void OnDestroy()
        {
            // Limpieza de Nivel 2: evitar que las llamadas de retorno lleguen a un objeto destruido.
            UnbindCurrentInventory();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TIER-1 EVENT HANDLER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Llamado por <see cref="GameManager.OnPlayerRegistered"/> cada vez que se publica un nuevo
        /// Transform del jugador, incluyendo al reaparecer tras una recarga de escena.
        /// </summary>
        private void OnPlayerRegistered(Transform playerTransform)
        {
            BindToPlayer(playerTransform);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  BINDING LOGIC
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Encuentra el <see cref="PlayerInventory"/> en <paramref name="player"/>,
        /// se desuscribe de forma segura del inventario anterior (seguridad al reaparecer),
        /// se suscribe a los eventos de las tres ranuras y sincroniza inmediatamente la interfaz
        /// con el estado actual del inventario.
        /// </summary>
        /// <param name="player">El Transform raíz del jugador. No debe ser nulo.</param>
        private void BindToPlayer(Transform player)
        {
            if (player == null)
            {
                Debug.LogWarning("[UI_InventoryHUD] BindToPlayer called with a null Transform.");
                return;
            }

            if (!player.TryGetComponent<PlayerInventory>(out PlayerInventory inventory))
            {
                Debug.LogWarning("[UI_InventoryHUD] No PlayerInventory found on the " +
                                 $"registered player '{player.name}'. " +
                                 "Ensure PlayerInventory is on the Player root.", player);
                return;
            }

            // ── Desuscribirse de cualquier inventario previamente vinculado ───────────────
            // Crítico al reaparecer: evita que el inventario antiguo (ahora destruido)
            // duplique los eventos en el HUD que aún sigue activo.
            UnbindCurrentInventory();

            // ── Nivel 2: Suscribirse al nuevo inventario ────────────────────────
            _boundInventory = inventory;
            _boundInventory.OnWeaponChanged     += OnWeaponChanged;
            _boundInventory.OnRelicChanged      += OnRelicChanged;
            _boundInventory.OnConsumableChanged += OnConsumableChanged;

            // ── Sincronización inmediata: enviar el estado actual de la ranura a la interfaz ─────────────
            // Sin esto, el HUD mostraría ranuras vacías hasta el próximo evento,
            // lo cual es incorrecto cuando el jugador ya tiene objetos (por ejemplo, en Continuar).
            _weaponSlot?    .UpdateSlot(_boundInventory.CurrentWeapon);
            _relicSlot?     .UpdateSlot(_boundInventory.CurrentRelic);
            _consumableSlot?.UpdateSlot(_boundInventory.CurrentConsumable);

            Debug.Log($"[UI_InventoryHUD] Bound to PlayerInventory on '{player.name}'.");
        }

        /// <summary>
        /// Se desuscribe de forma segura de todos los eventos en <see cref="_boundInventory"/>
        /// y limpia la referencia. Llamado antes de volver a vincular y al destruir.
        /// </summary>
        private void UnbindCurrentInventory()
        {
            if (_boundInventory == null) return;

            _boundInventory.OnWeaponChanged     -= OnWeaponChanged;
            _boundInventory.OnRelicChanged      -= OnRelicChanged;
            _boundInventory.OnConsumableChanged -= OnConsumableChanged;
            _boundInventory = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MANEJADORES DE EVENTOS DE NIVEL 2
        //  Cada manejador recibe el nuevo ItemDataSO (o nulo al limpiar) y
        //  lo enruta al UI_InventorySlot correspondiente. Sin lógica de juego aquí.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Invocado por <see cref="PlayerInventory.OnWeaponChanged"/>.
        /// Pasa los datos del arma (o nulo) a la ranura visual de arma.
        /// </summary>
        private void OnWeaponChanged(WeaponDataSO weaponData)
        {
            _weaponSlot?.UpdateSlot(weaponData);
        }

        /// <summary>
        /// Invocado por <see cref="PlayerInventory.OnRelicChanged"/>.
        /// Pasa los datos de la reliquia (o nulo) a la ranura visual de reliquia.
        /// </summary>
        private void OnRelicChanged(RelicDataSO relicData)
        {
            _relicSlot?.UpdateSlot(relicData);
        }

        /// <summary>
        /// Invocado por <see cref="PlayerInventory.OnConsumableChanged"/>.
        /// Pasa los datos del consumible (o nulo) a la ranura visual de consumible.
        /// </summary>
        private void OnConsumableChanged(ConsumableDataSO consumableData)
        {
            _consumableSlot?.UpdateSlot(consumableData);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        private void ValidateSlots()
        {
            if (_weaponSlot == null)
                Debug.LogError("[UI_InventoryHUD] _weaponSlot is not assigned. " +
                               "Assign it in the Inspector.", this);

            if (_relicSlot == null)
                Debug.LogError("[UI_InventoryHUD] _relicSlot is not assigned. " +
                               "Assign it in the Inspector.", this);

            if (_consumableSlot == null)
                Debug.LogError("[UI_InventoryHUD] _consumableSlot is not assigned. " +
                               "Assign it in the Inspector.", this);
        }
    }
}
