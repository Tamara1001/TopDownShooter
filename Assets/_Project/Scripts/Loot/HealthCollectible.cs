// ==============================================================
// HealthCollectible.cs
// --------------------------------------------------------------
// PURPOSE:
//   Concrete ICollectible strategy for health pickups.
//   Implements the "what happens" when a health pickup is collected:
//   it restores _healAmount HP to the player's HealthComponent.
//
//   This script replaces HealthPickup.cs. The key architectural
//   improvements are:
//     - Heal amount is now configurable per-prefab in the Inspector.
//     - Collision detection is fully delegated to AutoPickupTrigger.
//     - Destruction is fully delegated to AutoPickupTrigger.
//     - This class is a pure Strategy: it only applies its effect.
//
// SETUP:
//   Add this component to the same GameObject as AutoPickupTrigger
//   (or any of its children). Configure _healAmount in the Inspector.
// ==============================================================

using UnityEngine;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Concrete <see cref="ICollectible"/> strategy that restores health to
    /// the player's <see cref="HealthComponent"/> when collected.
    /// Works in conjunction with <see cref="AutoPickupTrigger"/>.
    /// </summary>
    public sealed class HealthCollectible : MonoBehaviour, ICollectible
    {
        // ----------------------------------------------------------
        // INSPECTOR FIELDS
        // ----------------------------------------------------------

        [Header("Health Settings")]

        [Tooltip("Amount of HP to restore when this pickup is collected. Must be positive.")]
        [SerializeField] private int _healAmount = 10;

        // ----------------------------------------------------------
        // ICollectible IMPLEMENTATION
        // ----------------------------------------------------------

        /// <summary>
        /// Restores <see cref="_healAmount"/> HP to the player's
        /// <see cref="HealthComponent"/>. Silently skips if no HealthComponent
        /// is found, avoiding a hard crash that would also suppress the
        /// Destroy call in <see cref="AutoPickupTrigger"/>. Do NOT call Destroy here.
        /// </summary>
        /// <param name="player">
        /// The player's root <see cref="GameObject"/> passed by
        /// <see cref="AutoPickupTrigger.OnTriggerEnter"/>.
        /// </param>
        public void Collect(GameObject player)
        {
            // TryGetComponent avoids a thrown exception on failure and is
            // allocation-free — preferable over GetComponent in hot paths.
            if (player.TryGetComponent<HealthComponent>(out HealthComponent health))
            {
                health.Heal(_healAmount);
            }
            else
            {
                Debug.LogWarning(
                    $"[HealthCollectible] Player '{player.name}' has no HealthComponent. " +
                    $"No healing was applied. The pickup will still be destroyed.",
                    gameObject
                );
            }

            // NOTE: Do NOT call Destroy(gameObject) here.
            // Object destruction is strictly the responsibility of AutoPickupTrigger.
        }
    }
}
