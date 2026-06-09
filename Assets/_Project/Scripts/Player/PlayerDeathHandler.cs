// =============================================================================
//  PlayerDeathHandler.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Bridges the HealthComponent's death event to the GameManager FSM.
//  Strictly follows Single Responsibility: this script ONLY listens for
//  the player's death and delegates the state transition — no animations,
//  no UI, no cleanup logic here.
//
//  ARCHITECTURE
//  ─────────────
//  • RequireComponent ensures HealthComponent is always present on the same
//    GameObject, making the dependency explicit and failure impossible.
//  • Subscribes in OnEnable / unsubscribes in OnDisable to be safe with
//    object pooling or re-enabling scenarios.
//  • The GameManager handles all downstream consequences (UI, timeScale, etc.)
//    via its own OnStateChanged event — this handler stays ignorant of them.
//
//  ATTACH TO
//  ─────────
//  The Player prefab (same root GameObject as HealthComponent).
// =============================================================================

using UnityEngine;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Listens to the player <see cref="HealthComponent"/>'s <c>OnDied</c> event
    /// and triggers the <see cref="GameManager.GameState.GameOver"/> state transition.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class PlayerDeathHandler : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE REFERENCES
        // ─────────────────────────────────────────────────────────────────────

        private HealthComponent _health;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            _health.OnDied += HandlePlayerDied;
        }

        private void OnDisable()
        {
            _health.OnDied -= HandlePlayerDied;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EVENT HANDLERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called exactly once by <see cref="HealthComponent"/> when the player's
        /// HP reaches zero. Delegates the Game Over state transition to the
        /// <see cref="GameManager"/>, which owns all downstream consequences.
        /// </summary>
        private void HandlePlayerDied()
        {
            Debug.Log("[PlayerDeathHandler] Player has died. Triggering Game Over.");
            GameManager.Instance.ChangeState(GameManager.GameState.GameOver);
        }
    }
}
