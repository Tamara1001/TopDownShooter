// =============================================================================
//  IWeaponConfigurable.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Optional configuration interface for weapon logic MonoBehaviours.
//  Separates the concern of "how a weapon attacks" (IWeapon) from
//  "how a weapon receives its stats" (IWeaponConfigurable).
//
//  WHY A SEPARATE INTERFACE?
//  ─────────────────────────
//  IWeapon's contract is intentionally minimal: void ExecuteAttack().
//  Adding Configure() there would force EVERY weapon to implement it,
//  even simple hardcoded ones that don't use ScriptableObject stats.
//
//  By making IWeaponConfigurable opt-in, PlayerCombat checks at runtime:
//    if (weapon is IWeaponConfigurable cfg) cfg.Configure(weaponData);
//
//  Weapons that don't need SO stats simply don't implement this interface.
//  No base class, no abstract method, no forcing — pure opt-in.
//
//  CALL SITE (PlayerCombat.HandleWeaponChanged):
//    1. Instantiate WeaponDataSO.WeaponLogicPrefab as a child of the Player.
//    2. Cast to IWeapon → _equippedWeapon.
//    3. If also IWeaponConfigurable → call Configure(weaponData).
//       The weapon now knows its fire rate, damage, etc. from the SO.
//
//  FUTURE EXPANSION:
//  ► Pass a full WeaponDataSO for all current stats (done).
//  ► Add void Deconfigure() if weapons need cleanup when unequipped.
//  ► Extend WeaponDataSO with spread, range, ammo — Configure() picks them up
//    automatically; weapon logic just reads the properties it cares about.
// =============================================================================

using TopDownShooter.Inventory;

namespace TopDownShooter.Combat
{
    /// <summary>
    /// Optional interface implemented by weapon MonoBehaviours that wish to
    /// receive their runtime stats from a <see cref="TopDownShooter.Inventory.WeaponDataSO"/>
    /// upon instantiation.
    ///
    /// <para>
    /// <b>Usage pattern in <see cref="PlayerCombat"/>:</b>
    /// <code>
    /// if (instance is IWeaponConfigurable configurable)
    ///     configurable.Configure(weaponData);
    /// </code>
    /// </para>
    ///
    /// <para>
    /// Weapons that have fully hardcoded stats may omit this interface entirely —
    /// <see cref="PlayerCombat"/> performs the cast defensively and skips if null.
    /// </para>
    /// </summary>
    public interface IWeaponConfigurable
    {
        /// <summary>
        /// Called once by <see cref="PlayerCombat"/> immediately after the weapon
        /// logic MonoBehaviour is instantiated as a child of the Player.
        ///
        /// <para>
        /// Implementations should read only the properties they care about from
        /// <paramref name="stats"/> and store them locally. The SO reference itself
        /// should NOT be stored long-term to keep data ownership clear.
        /// </para>
        /// </summary>
        /// <param name="stats">
        /// The <see cref="WeaponDataSO"/> of the item that was just picked up.
        /// Contains fire rate, base damage, and any other weapon-specific fields.
        /// </param>
        void Configure(WeaponDataSO stats);
    }
}
