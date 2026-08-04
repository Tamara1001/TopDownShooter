
using UnityEngine;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Strategy interface for collectible items.
    /// Any pickup effect — healing, coins, ammo, buffs — must implement
    /// this contract to integrate with the <see cref="AutoPickupTrigger"/> system.
    /// </summary>
    public interface ICollectible
    {
        /// <summary>
        /// Applies this collectible's effect to the given player GameObject.
        /// Called by <see cref="AutoPickupTrigger"/> when the player enters
        /// the trigger volume. Implementations must NOT call Destroy here;
        /// object lifetime is strictly managed by <see cref="AutoPickupTrigger"/>.
        /// </summary>
        /// <param name="player">The player's root GameObject that entered the trigger.</param>
        void Collect(GameObject player);
    }
}
