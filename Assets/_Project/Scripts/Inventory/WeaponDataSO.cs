// =============================================================================
//  WeaponDataSO.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Concrete ScriptableObject for WEAPON item archetypes.
//  Extends ItemDataSO with weapon-specific stats and the logic prefab reference
//  that PlayerCombat instantiates when this weapon is picked up.
//
//  PART 2 — LIVE:
//  WeaponLogicPrefab is the key link between DATA (this SO) and LOGIC (the
//  IWeapon MonoBehaviour). PlayerCombat instantiates it as a child, then calls
//  IWeaponConfigurable.Configure(this) so the logic reads its stats from here.
//
//  PART 4 — RESOURCE COST:
//  Added WeaponResourceType enum and resource cost so PlayerCombat can gate
//  attack execution through PlayerResourceComponent before firing.
//
//  OOP RULE: The SO owns stats. The logic prefab owns behaviour.
//  They only meet at the moment of instantiation — zero tight coupling.
// =============================================================================

using UnityEngine;

namespace TopDownShooter.Inventory
{
    // ─────────────────────────────────────────────────────────────────────────
    //  RESOURCE TYPE ENUM  (Part 4)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Determines which player resource (if any) is consumed per attack.
    /// Configured on the <see cref="WeaponDataSO"/> asset and read by
    /// <see cref="TopDownShooter.Combat.PlayerCombat"/> before each shot.
    /// </summary>
    public enum WeaponResourceType
    {
        /// <summary>This weapon has no resource cost. Always fires freely.</summary>
        None,
        /// <summary>Consumes Mana. Used by magical weapons (staves, grimoires).</summary>
        Mana,
        /// <summary>Consumes Energy. Used by physical weapons (daggers, bows).</summary>
        Energy
    }

    /// <summary>
    /// ScriptableObject blueprint for equippable weapon items.
    /// Drop into a slot and the player gains access to a new attack pattern.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewWeaponData",
        menuName = "TopDownShooter/Inventory/Weapon Data")]
    public sealed class WeaponDataSO : ItemDataSO
    {
        // ─────────────────────────────────────────────────────────────────────
        //  WEAPON STATS  (Part 2 expansion stubs)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Weapon Stats")]
        [Tooltip("Base damage dealt per hit. Read by the IWeapon strategy.")]
        [Min(1)]
        [SerializeField] private int _baseDamage = 10;

        [Tooltip("Minimum seconds between consecutive shots / swings.")]
        [Min(0.05f)]
        [SerializeField] private float _fireRate = 0.25f;

        // ─────────────────────────────────────────────────────────────────────
        //  RESOURCE COST  (Part 4)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Resource Cost")]
        [Tooltip("Which player resource this weapon consumes on each attack. " +
                 "None = free to use, Mana = magical, Energy = physical.")]
        [SerializeField] private WeaponResourceType _resourceType = WeaponResourceType.None;

        [Tooltip("Amount of the chosen resource consumed per attack. " +
                 "Ignored when ResourceType is None.")]
        [Min(0)]
        [SerializeField] private int _resourceCost = 0;

        // ─────────────────────────────────────────────────────────────────────
        //  WEAPON LOGIC PREFAB  (Part 2)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Weapon Logic")]
        [Tooltip("A prefab whose root MonoBehaviour implements IWeapon (e.g. MagicWand, " +
                 "MeleeWeapon, RangedWeapon). PlayerCombat will Instantiate this as a " +
                 "child of the Player and call IWeaponConfigurable.Configure() on it. " +
                 "The prefab MUST have exactly one IWeapon component on its root.")]
        [SerializeField] private MonoBehaviour _weaponLogicPrefab;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC GETTERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Base damage per attack, used by the IWeapon strategy.</summary>
        public int                BaseDamage        => _baseDamage;

        /// <summary>Minimum interval in seconds between consecutive attacks.</summary>
        public float              FireRate           => _fireRate;

        /// <summary>
        /// Which resource (Mana, Energy, or None) this weapon spends per shot.
        /// Read by <see cref="TopDownShooter.Combat.PlayerCombat"/> before each attack.
        /// </summary>
        public WeaponResourceType ResourceType       => _resourceType;

        /// <summary>
        /// How much of <see cref="ResourceType"/> is consumed per attack.
        /// Ignored when <see cref="ResourceType"/> is <see cref="WeaponResourceType.None"/>.
        /// </summary>
        public int                ResourceCost       => _resourceCost;

        /// <summary>
        /// The MonoBehaviour prefab PlayerCombat instantiates as a child of the Player
        /// when this weapon is equipped. Must implement <see cref="TopDownShooter.Combat.IWeapon"/>.
        /// </summary>
        public MonoBehaviour      WeaponLogicPrefab  => _weaponLogicPrefab;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (_weaponLogicPrefab != null &&
                !(_weaponLogicPrefab is TopDownShooter.Combat.IWeapon))
            {
                UnityEngine.Debug.LogWarning(
                    $"[WeaponDataSO] '{name}': The assigned WeaponLogicPrefab " +
                    $"('{_weaponLogicPrefab.GetType().Name}') does not implement IWeapon. " +
                    "PlayerCombat will log an error at runtime.", this);
            }
        }
#endif
    }
}
