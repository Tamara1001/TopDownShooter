
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
        /// Minimum seconds between consecutive attacks.
        /// Contexts (Player, EnemyBrain) check this to gate their fire rate.
        /// </summary>
        float Cooldown { get; }

        /// <summary>
        /// Executes the weapon's primary attack logic.
        /// Called by <see cref="PlayerCombat"/> every time the Attack input fires.
        /// Implementations are responsible for their own fire-rate gating,
        /// projectile spawning, sound, VFX, etc.
        /// </summary>
        void ExecuteAttack();

        /// <summary>
        /// Inyecta multiplicadores de daño y cooldown temporalmente
        /// (utilizado por el sistema D20 Dungeon Master).
        /// </summary>
        void SetDungeonMultipliers(float damageMultiplier, float cooldownMultiplier);

        // ─── Future contract methods (uncomment as systems are built) ──────────
        // void ExecuteAlternateAttack();   // Right-click / secondary fire
        // void Reload();                   // For ammo-based weapons
        // bool CanFire { get; }            // FSM gate: is the weapon ready?
    }
}
