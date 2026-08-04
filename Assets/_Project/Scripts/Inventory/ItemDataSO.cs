
using UnityEngine;

namespace TopDownShooter.Inventory
{
    /// <summary>
    /// Plantilla de datos abstracta compartida por cada arquetipo de objeto.
    /// Subclasifique esto para crear <see cref="WeaponDataSO"/>,
    /// <see cref="RelicDataSO"/> o <see cref="ConsumableDataSO"/>.
    /// </summary>
    public abstract class ItemDataSO : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        //  IDENTITY
        // ─────────────────────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Identificador único de cadena utilizado por sistemas de guardado, logros y analíticas. Debe ser único en todos los assets ItemDataSO.")]
        [SerializeField] private string _itemID;

        [Tooltip("Nombre legible por humanos que se muestra en el HUD, tooltips y archivos de guardado.")]
        [SerializeField] private string _displayName;

        // ─────────────────────────────────────────────────────────────────────
        //  VISUALS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Visuals")]
        [Tooltip("Icono mostrado en la ranura del HUD del inventario. Tamaño recomendado: 128×128 o 256×256 píxeles.")]
        [SerializeField] private Sprite _icon;

        // ─────────────────────────────────────────────────────────────────────
        //  WORLD REPRESENTATION
        // ─────────────────────────────────────────────────────────────────────

        [Header("World Object")]
        [Tooltip("El prefab generado en el suelo cuando se suelta este objeto (se retira del inventario). Debe tener un componente ItemPickup, un SphereCollider (Is Trigger = true) y un Collider para los elementos visuales.")]
        [SerializeField] private GameObject _dropPrefab;

        // ─────────────────────────────────────────────────────────────────────
        //  GETTERS PÚBLICOS DE SOLO LECTURA
        //  Los sistemas externos leen estos; solo el editor de assets del SO escribe en ellos.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Identificador único de cadena para este arquetipo de objeto.</summary>
        public string     ItemID      => _itemID;

        /// <summary>Nombre amigable para mostrar en HUD y tooltips.</summary>
        public string     DisplayName => _displayName;

        /// <summary>Sprite del icono para la ranura del HUD del inventario.</summary>
        public Sprite     Icon        => _icon;

        /// <summary>
        /// Prefab generado en la posición del jugador cuando se suelta este objeto.
        /// Debe contener un componente <see cref="ItemPickup"/>.
        /// </summary>
        public GameObject DropPrefab  => _dropPrefab;

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR VALIDATION
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_itemID))
                UnityEngine.Debug.LogWarning(
                    $"[ItemDataSO] '{name}': ItemID is empty. " +
                    "Every item must have a unique ID.", this);

            if (string.IsNullOrWhiteSpace(_displayName))
                UnityEngine.Debug.LogWarning(
                    $"[ItemDataSO] '{name}': DisplayName is empty.", this);

            if (_dropPrefab == null)
                UnityEngine.Debug.LogWarning(
                    $"[ItemDataSO] '{name}': DropPrefab is not assigned. " +
                    "The item cannot be dropped without a prefab.", this);
        }
#endif
    }
}
