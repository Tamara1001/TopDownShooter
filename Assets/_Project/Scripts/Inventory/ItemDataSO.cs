
using UnityEngine;

namespace TopDownShooter.Inventory
{
    /// <summary>
    /// Abstract data blueprint shared by every item archetype.
    /// Subclass this to create <see cref="WeaponDataSO"/>,
    /// <see cref="RelicDataSO"/>, or <see cref="ConsumableDataSO"/>.
    /// </summary>
    public abstract class ItemDataSO : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        //  IDENTITY
        // ─────────────────────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Unique string identifier used by save systems, achievement " +
                 "trackers, and analytics. Must be unique across all ItemDataSO assets.")]
        [SerializeField] private string _itemID;

        [Tooltip("Human-readable name shown in the HUD, tooltips, and save files.")]
        [SerializeField] private string _displayName;

        // ─────────────────────────────────────────────────────────────────────
        //  VISUALS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Visuals")]
        [Tooltip("Icon displayed in the inventory HUD slot. " +
                 "Recommended size: 128×128 or 256×256 pixels.")]
        [SerializeField] private Sprite _icon;

        // ─────────────────────────────────────────────────────────────────────
        //  WORLD REPRESENTATION
        // ─────────────────────────────────────────────────────────────────────

        [Header("World Object")]
        [Tooltip("The prefab spawned onto the floor when this item is dropped " +
                 "(swapped out of inventory). Must have an ItemPickup component, " +
                 "a SphereCollider (Is Trigger = true), and a Collider for visuals.")]
        [SerializeField] private GameObject _dropPrefab;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC READ-ONLY GETTERS
        //  External systems read these; only the SO asset editor writes them.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Unique string identifier for this item archetype.</summary>
        public string     ItemID      => _itemID;

        /// <summary>Display-friendly name for HUD and tooltips.</summary>
        public string     DisplayName => _displayName;

        /// <summary>Icon sprite for the inventory HUD slot.</summary>
        public Sprite     Icon        => _icon;

        /// <summary>
        /// Prefab spawned at the player's position when this item is dropped.
        /// Must contain an <see cref="ItemPickup"/> component.
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
