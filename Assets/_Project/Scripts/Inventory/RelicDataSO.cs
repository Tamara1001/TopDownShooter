// =============================================================================
//  RelicDataSO.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Concrete ScriptableObject for RELIC item archetypes.
//  Relics are passive items that grant persistent stat modifiers to the player
//  while equipped in the Relic slot (e.g. +20% speed, damage aura, shield).
//
//  CURRENT SCOPE (Part 1 — Data Layer):
//  Defines the menu path and relic-specific extension fields.
//  The modifier fields below are stubs ready for the PlayerStats system
//  in Part 2.
//
//  FUTURE HOOKS (Part 2):
//  ► PlayerStats : When RelicDataSO is assigned, PlayerInventory fires
//                  OnRelicEquipped(RelicDataSO). A PlayerStats component
//                  subscribes and applies the modifiers below.
//  ► VFX         : RelicDataSO can carry a particle effect prefab for the
//                  persistent aura shown while equipped.
// =============================================================================

using UnityEngine;

namespace TopDownShooter.Inventory
{
    /// <summary>
    /// ScriptableObject blueprint for passive relic items.
    /// Equipped in the Relic slot; grants stat modifiers while held.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewRelicData",
        menuName = "TopDownShooter/Inventory/Relic Data")]
    public sealed class RelicDataSO : ItemDataSO
    {
        // ─────────────────────────────────────────────────────────────────────
        //  RELIC MODIFIERS  (Part 2 expansion stubs)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Passive Modifiers")]
        [Tooltip("Percentage movement speed bonus while this relic is equipped. " +
                 "0 = no bonus, 0.2 = +20%. Read by PlayerStats in Part 2.")]
        [Range(-1f, 5f)]
        [SerializeField] private float _moveSpeedModifier = 0f;

        [Tooltip("Flat bonus added to the player's maximum health while equipped. " +
                 "Can be negative (cursed relics). Read by PlayerStats in Part 2.")]
        [SerializeField] private int _maxHealthBonus = 0;

        [Tooltip("Percentage damage multiplier while equipped. " +
                 "0 = no change, 0.5 = +50% damage. Read by PlayerStats in Part 2.")]
        [Range(-1f, 5f)]
        [SerializeField] private float _damageModifier = 0f;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC GETTERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Fractional speed multiplier applied by PlayerStats while equipped.</summary>
        public float MoveSpeedModifier => _moveSpeedModifier;

        /// <summary>Flat max-health bonus applied by PlayerStats while equipped.</summary>
        public int   MaxHealthBonus    => _maxHealthBonus;

        /// <summary>Fractional damage multiplier applied by PlayerStats while equipped.</summary>
        public float DamageModifier    => _damageModifier;
    }
}
