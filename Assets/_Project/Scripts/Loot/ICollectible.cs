// ==============================================================
// ICollectible.cs
// --------------------------------------------------------------
// PURPOSE:
//   Defines the contract for any pickup object that can be
//   collected by the player. This interface is the cornerstone
//   of the Auto-Loot Strategy Pattern architecture.
//
//   Separating the "what happens on collect" logic (Strategy)
//   from the "who triggers the collect" logic (AutoPickupTrigger)
//   eliminates code duplication and enables unlimited new pickup
//   types without touching the trigger system.
//
// USAGE:
//   Implement this interface on any MonoBehaviour that represents
//   a pickup effect (e.g., CoinCollectible, HealthCollectible).
//   Place it on the same GameObject (or a child) alongside an
//   AutoPickupTrigger component.
// ==============================================================

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
