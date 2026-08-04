
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
