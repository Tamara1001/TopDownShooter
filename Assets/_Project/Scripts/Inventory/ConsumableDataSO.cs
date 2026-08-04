


using UnityEngine;

namespace TopDownShooter.Inventory
{
    /// <summary>
    /// ScriptableObject blueprint for single-use consumable items.
    /// Consumed via the Consume input action (Q key); clears the slot on use.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewConsumableData",
        menuName = "TopDownShooter/Inventory/Consumable Data")]
    public sealed class ConsumableDataSO : ItemDataSO
    {
        // ─────────────────────────────────────────────────────────────────────
        //  QUEST ITEM FLAG  (Part 3)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Quest Item")]
        [Tooltip("When true, this consumable is a quest item (e.g. a Key). " +
                 "PlayerInventory will block the Q-key consume path so it " +
                 "cannot be accidentally destroyed. Use E to interact with " +
                 "world objects that require this item.")]
        [SerializeField] private bool _isQuestItem = false;

        // ─────────────────────────────────────────────────────────────────────
        //  CONSUMABLE EFFECT PARAMETERS  (Part 2 expansion stubs)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Consumable Effect")]
        [Tooltip("Flat hit points restored immediately on use. " +
                 "0 = no healing (e.g. speed boost only). Clamped to MaxHealth.")]
        [Min(0)]
        [SerializeField] private int _healAmount = 30;

        [Tooltip("Duration in seconds for any timed effect (e.g. speed boost). " +
                 "0 = instantaneous (healing potions, one-shot buffs).")]
        [Min(0f)]
        [SerializeField] private float _effectDuration = 0f;

        [Tooltip("Percentage speed boost applied for EffectDuration seconds. " +
                 "0 = no speed change. Read by PlayerStats in Part 2.")]
        [Range(0f, 5f)]
        [SerializeField] private float _speedBoostMultiplier = 0f;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC GETTERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When true this item is a quest item (e.g. a Key) and cannot be
        /// consumed via the Q key. Use E to interact with world objects.
        /// </summary>
        public bool  IsQuestItem         => _isQuestItem;

        /// <summary>Flat health points restored when this consumable is used.</summary>
        public int   HealAmount          => _healAmount;

        /// <summary>Duration in seconds for any timed buff/effect. 0 = instant.</summary>
        public float EffectDuration      => _effectDuration;

        /// <summary>Fractional speed boost multiplier for timed effects.</summary>
        public float SpeedBoostMultiplier => _speedBoostMultiplier;
    }
}
