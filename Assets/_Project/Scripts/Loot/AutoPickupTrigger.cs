// ==============================================================
// AutoPickupTrigger.cs
// --------------------------------------------------------------
// PURPOSE:
//   Acts as the single, centralised "sensor" for the Auto-Loot
//   system. It is the ONLY place responsible for:
//     1. Detecting when the player enters the pickup trigger.
//     2. Delegating the effect to an ICollectible implementor
//        (Strategy Pattern — the "what" is decoupled from the "when").
//     3. Destroying the parent GameObject after collection.
//
//   This script MUST NOT contain any game-logic for health,
//   coins, or any other pickup type. It only calls Collect()
//   and Destroy(). All concrete behaviour lives in scripts
//   that implement ICollectible.
//
// SETUP:
//   Attach this component to the root pickup GameObject alongside
//   a Collider set to "Is Trigger". Then add a concrete collectible
//   component (e.g., CoinCollectible) to the same GameObject or
//   any child. AutoPickupTrigger will locate it automatically.
// ==============================================================

using UnityEngine;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Detects player trigger entry and delegates collection to the
    /// <see cref="ICollectible"/> found on this GameObject or its children.
    /// Strictly centralises object destruction after a successful pickup.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class AutoPickupTrigger : MonoBehaviour
    {
        // ----------------------------------------------------------
        // PRIVATE STATE
        // ----------------------------------------------------------

        /// <summary>
        /// Cached reference to the ICollectible strategy on this
        /// GameObject or any of its children. Resolved once in Awake.
        /// </summary>
        private ICollectible _collectible;

        // ----------------------------------------------------------
        // UNITY LIFECYCLE
        // ----------------------------------------------------------

        /// <summary>
        /// Resolves and caches the <see cref="ICollectible"/> strategy.
        /// Logs an error if none is found so the issue is immediately
        /// visible in the Console rather than silently failing at runtime.
        /// </summary>
        private void Awake()
        {
            // Search this GameObject first, then all children.
            // This allows the collectible logic to live on a child object
            // (e.g., a visual mesh) without breaking the architecture.
            _collectible = GetComponent<ICollectible>() ?? GetComponentInChildren<ICollectible>();

            if (_collectible == null)
            {
                Debug.LogError(
                    $"[AutoPickupTrigger] No ICollectible found on '{gameObject.name}' " +
                    $"or its children. This pickup will be inert. " +
                    $"Add a CoinCollectible, HealthCollectible, or custom ICollectible component.",
                    gameObject
                );
            }
        }

        // ----------------------------------------------------------
        // TRIGGER DETECTION
        // ----------------------------------------------------------

        /// <summary>
        /// Called by Unity's physics engine when another Collider enters
        /// this trigger volume. If the entering object is tagged "Player"
        /// and a valid <see cref="ICollectible"/> strategy is cached,
        /// executes the collection and then destroys this GameObject.
        /// </summary>
        /// <param name="other">The Collider that entered the trigger.</param>
        private void OnTriggerEnter(Collider other)
        {
            // Early-out guard: only process player collisions.
            if (!other.CompareTag("Player")) return;

            // Early-out guard: do nothing if setup failed in Awake.
            if (_collectible == null) return;

            // Delegate the pickup effect to the concrete strategy.
            // The ICollectible implementation is responsible only for
            // applying its effect. It must NOT call Destroy itself.
            _collectible.Collect(other.gameObject);

            // Strictly centralised destruction: this is the ONLY place
            // in the entire loot system where Destroy is called on the
            // pickup object. All ICollectible implementations must omit Destroy.
            Destroy(gameObject);
        }
    }
}
