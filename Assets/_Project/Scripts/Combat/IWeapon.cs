// =============================================================================
//  IWeapon.cs
//  Author  : [Your Name]
//  Project : TopDownShooter – Protagonist: Lunaria (Mage)
//  Created : 2026
//
//  PURPOSE
//  -------
//  Defines the contract that every weapon in the game must fulfil.
//  This is the core of the Strategy Pattern for the weapon system.
//
//  STRATEGY PATTERN ROLE: Abstract Strategy
//  ─────────────────────────────────────────
//  • IWeapon is the abstract "strategy" interface.
//  • MagicWand (and any future weapon) are the "concrete strategies".
//  • PlayerCombat is the "context" that delegates to whichever IWeapon
//    is currently equipped – it never knows which concrete type it holds.
//
//  This decoupling means you can add an IceLance, FireStaff, or
//  LightningRod weapon at any time without touching PlayerCombat.cs.
//
//  FUTURE HOOKS
//  ► SO   : Add a WeaponDataSO property to expose stats without subclassing.
//  ► FSM  : Add bool CanFire { get; } so the FSM can gate attacks.
//  ► UI   : Add string WeaponName / Sprite WeaponIcon for HUD display.
// =============================================================================

namespace TopDownShooter.Combat
{
    /// <summary>
    /// Abstract Strategy contract for all equippable weapons.
    /// Any MonoBehaviour implementing this interface can be used as Lunaria's
    /// active weapon via the Strategy Pattern in <see cref="PlayerCombat"/>.
    /// </summary>
    public interface IWeapon
    {
        /// <summary>
        /// Executes the weapon's primary attack logic.
        /// Called by <see cref="PlayerCombat"/> every time the Attack input fires.
        /// Implementations are responsible for their own fire-rate gating,
        /// projectile spawning, sound, VFX, etc.
        /// </summary>
        void ExecuteAttack();

        // ─── Future contract methods (uncomment as systems are built) ──────────
        // void ExecuteAlternateAttack();   // Right-click / secondary fire
        // void Reload();                   // For ammo-based weapons
        // bool CanFire { get; }            // FSM gate: is the weapon ready?
    }
}
