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
//  OOP RULE: The SO owns stats. The logic prefab owns behaviour.
//  They only meet at the moment of instantiation — zero tight coupling.
// =============================================================================

using UnityEngine;

namespace TopDownShooter.Inventory
{
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
        public int           BaseDamage        => _baseDamage;

        /// <summary>Minimum interval in seconds between consecutive attacks.</summary>
        public float         FireRate           => _fireRate;

        /// <summary>
        /// The MonoBehaviour prefab PlayerCombat instantiates as a child of the Player
        /// when this weapon is equipped. Must implement <see cref="TopDownShooter.Combat.IWeapon"/>.
        /// </summary>
        public MonoBehaviour WeaponLogicPrefab  => _weaponLogicPrefab;

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
