// ==============================================================
// PlayerRegistration.cs
// --------------------------------------------------------------
// PURPOSE:
//   A microscopic "glue" MonoBehaviour that bridges the Player
//   GameObject and the GameManager's player registry.
//
//   Attach it to the Player prefab root. In Awake() it announces
//   the player's Transform to GameManager.RegisterPlayer(), which
//   then fires the OnPlayerRegistered event — waking any EnemyBrain
//   instances that were waiting in their WaitForPlayer coroutine.
//
// WHY A SEPARATE SCRIPT?
//   Single Responsibility Principle: PlayerController3D handles
//   input and movement; HealthComponent handles damage; this script
//   handles exactly ONE concern — publishing the player's existence.
//   No other script needs to be modified to support respawning.
//
// RESPAWN SUPPORT:
//   Because RegisterPlayer() is called in Awake(), it runs every
//   time the Player prefab is instantiated — whether on initial
//   load, after a respawn, or when returned from an object pool.
//   GameManager.OnPlayerRegistered fires each time, so enemies,
//   minimap cameras, and any other subscriber auto-update.
//
// DEATH / REMOVAL:
//   OnDestroy calls GameManager.UnregisterPlayer() so the reference
//   is cleared when the player is permanently removed (e.g., during
//   a "Game Over" before the next respawn or scene reload). This
//   prevents enemies from chasing a dangling Transform.
//   If your game uses pooling instead of Destroy, call
//   UnregisterPlayer() from the pool's release callback instead.
// ==============================================================

using UnityEngine;

/// <summary>
/// Lightweight component attached to the Player prefab.
/// Registers and unregisters the player's <see cref="Transform"/>
/// with <see cref="GameManager"/> so all systems (EnemyBrain,
/// minimap, camera rigs) can obtain a canonical, spawn-order-
/// independent reference to the player.
/// </summary>
public class PlayerRegistration : MonoBehaviour
{
    // ----------------------------------------------------------
    // UNITY LIFECYCLE
    // ----------------------------------------------------------

    /// <summary>
    /// Publishes this Transform to GameManager as early as possible
    /// (Awake runs before Start on all other scripts in the same frame).
    ///
    /// Uses a null-guard on <see cref="GameManager.Instance"/> so the
    /// script degrades gracefully in isolated test scenes that have no
    /// GameManager — a warning is logged but nothing breaks.
    /// </summary>
    private void Awake()
    {
        if (GameManager.Instance == null)
        {
            // Graceful degradation: EnemyBrain's Tier 2 (tag search)
            // will still find the player normally.
            Debug.LogWarning("[PlayerRegistration] GameManager.Instance is null. " +
                             "Player will NOT be registered centrally. " +
                             "Add a GameManager to the scene for full multi-system support.");
            return;
        }

        GameManager.Instance.RegisterPlayer(transform);
    }

    /// <summary>
    /// Clears the central player reference when this GameObject is
    /// permanently destroyed (e.g., game over without immediate respawn).
    ///
    /// POOLING NOTE: If the player is deactivated (pooled) rather than
    /// destroyed, replace this with an explicit UnregisterPlayer() call
    /// in your pool's release callback to keep the timeline predictable.
    /// </summary>
    private void OnDestroy()
    {
        // Guard: only unregister if WE are the currently registered player.
        // This prevents a newly respawned player from being unregistered
        // by the old instance's OnDestroy firing a frame later.
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.PlayerTransform == transform)
        {
            GameManager.Instance.UnregisterPlayer();
        }
    }
}
