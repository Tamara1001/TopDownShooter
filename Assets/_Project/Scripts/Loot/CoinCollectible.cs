// ==============================================================
// CoinCollectible.cs
// --------------------------------------------------------------
// PURPOSE:
//   Concrete ICollectible strategy for coin pickups.
//   Implements the "what happens" when a coin is collected:
//   it adds _coinValue coins to the player's PlayerWallet.
//
//   This script replaces CoinPickup.cs. The key architectural
//   improvements are:
//     - Coin value is now configurable per-prefab in the Inspector.
//     - Collision detection is fully delegated to AutoPickupTrigger.
//     - Destruction is fully delegated to AutoPickupTrigger.
//     - This class is a pure Strategy: it only applies its effect.
//
// SETUP:
//   Add this component to the same GameObject as AutoPickupTrigger
//   (or any of its children). Configure _coinValue in the Inspector.
// ==============================================================

using UnityEngine;
using TopDownShooter.Player;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Concrete <see cref="ICollectible"/> strategy that awards coins to
    /// the player's <see cref="PlayerWallet"/> when collected.
    /// Works in conjunction with <see cref="AutoPickupTrigger"/>.
    /// </summary>
    public sealed class CoinCollectible : MonoBehaviour, ICollectible
    {
        // ----------------------------------------------------------
        // INSPECTOR FIELDS
        // ----------------------------------------------------------

        [Header("Coin Settings")]

        [Tooltip("Number of coins to award when this pickup is collected. Must be positive.")]
        [SerializeField] private int _coinValue = 1;

        // ----------------------------------------------------------
        // ICollectible IMPLEMENTATION
        // ----------------------------------------------------------

        /// <summary>
        /// Awards <see cref="_coinValue"/> coins to the player's
        /// <see cref="PlayerWallet"/>. Silently skips if no wallet is found,
        /// avoiding a hard crash that would also suppress the Destroy call
        /// in <see cref="AutoPickupTrigger"/>. Do NOT call Destroy here.
        /// </summary>
        /// <param name="player">
        /// The player's root <see cref="GameObject"/> passed by
        /// <see cref="AutoPickupTrigger.OnTriggerEnter"/>.
        /// </param>
        public void Collect(GameObject player)
        {
            // TryGetComponent avoids a thrown exception on failure and is
            // allocation-free — preferable over GetComponent in hot paths.
            if (player.TryGetComponent<PlayerWallet>(out PlayerWallet wallet))
            {
                wallet.AddCoins(_coinValue);
            }
            else
            {
                Debug.LogWarning(
                    $"[CoinCollectible] Player '{player.name}' has no PlayerWallet component. " +
                    $"Coins were NOT awarded. The pickup will still be destroyed.",
                    gameObject
                );
            }

            // NOTE: Do NOT call Destroy(gameObject) here.
            // Object destruction is strictly the responsibility of AutoPickupTrigger.
        }
    }
}
