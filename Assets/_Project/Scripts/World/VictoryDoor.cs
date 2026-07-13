// =============================================================================
//  VictoryDoor.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  A world interactable object that triggers the Victory condition when the
//  player interacts with it while holding the required Key item.
//
//  ARCHITECTURE  (Single Responsibility)
//  ─────────────
//  • This script owns the "check key + trigger win" logic — nothing else.
//  • It reads the player's inventory via the IWorldInteractable contract,
//    keeping it fully decoupled from the Player prefab hierarchy.
//  • The GameManager owns all downstream consequences of Victory (UI, timeScale).
//
//  SETUP CHECKLIST
//  ───────────────
//  1. Attach this script to a door-shaped GameObject in the scene.
//  2. Assign the matching ConsumableDataSO (Key) to _requiredKey in Inspector.
//  3. Add a Collider (Trigger or regular) to the same GameObject.
//  4. Set the GameObject's layer to the layer configured as _interactableLayerMask
//     in the Player's PlayerInventory component.
// =============================================================================

using UnityEngine;
using TopDownShooter.Inventory;
using TopDownShooter.Player;
using TopDownShooter.Interaction;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TopDownShooter.World
{
    /// <summary>
    /// A world object that requires a specific <see cref="ConsumableDataSO"/> Key
    /// to be in the player's consumable slot before granting Victory.
    /// Implements <see cref="IWorldInteractable"/> to integrate with
    /// <see cref="PlayerInventory"/>'s E-key interaction flow.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class VictoryDoor : MonoBehaviour, IWorldInteractable
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Door Configuration")]
        [Tooltip("The ConsumableDataSO representing the Key that unlocks this door. " +
                 "Must be a Quest Item (IsQuestItem = true) so it cannot be consumed " +
                 "accidentally with Q.")]
        [SerializeField] private ConsumableDataSO _requiredKey;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // Prevents the Victory state from triggering more than once if the player
        // presses E repeatedly before the state transition completes.
        // Mirrors the _isUnlocked guard used in LockedBossDoor.
        // Exposed publicly so debug tools can query the state without reflection.
        public bool IsUnlocked { get; private set; } = false;

        // ─────────────────────────────────────────────────────────────────────
        //  IWorldInteractable IMPLEMENTATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="PlayerInventory"/> when the player presses E
        /// near this door. Checks whether the player holds <see cref="_requiredKey"/>
        /// in the consumable slot and transitions to Victory if so.
        /// </summary>
        /// <param name="inventory">The player's inventory to inspect.</param>
        public void Interact(PlayerInventory inventory)
        {
            // ── Entry-point trace ────────────────────────────────────────────
            // If this line NEVER appears in the Console, the problem is upstream:
            // the door's collider is missing, is on the wrong layer, or sits outside
            // the PlayerInventory._pickupRadius OverlapSphere.
            Debug.Log($"[VictoryDoor] Interact called by '{inventory?.name ?? "NULL"}'. " +
                      $"Player holds: '{inventory?.CurrentConsumable?.DisplayName ?? "None"}'.");

            // ── Guard: already unlocked ──────────────────────────────────────
            if (IsUnlocked) return;

            // ── Guard: inventory must be valid ───────────────────────────────
            if (inventory == null)
            {
                Debug.LogError("[VictoryDoor] Interact received a null PlayerInventory reference. " +
                               "Check that PlayerInventory calls Interact(this).", this);
                return;
            }

            // ── Guard: required key must be configured ───────────────────────
            if (_requiredKey == null)
            {
                Debug.LogError("[VictoryDoor] _requiredKey is not assigned! " +
                               "Assign a ConsumableDataSO to _requiredKey in the Inspector.", this);
                return;
            }

            // ── Key comparison ───────────────────────────────────────────────
            // SO reference equality: two pickups sharing the same SO asset are
            // the same key type — no string comparison needed or desired.
            string heldKeyName   = inventory.CurrentConsumable?.DisplayName ?? "None";
            string neededKeyName = _requiredKey.DisplayName;

            Debug.Log($"[VictoryDoor] Key check — Required: '{neededKeyName}' | " +
                      $"Player holds: '{heldKeyName}'.");

            if (inventory.CurrentConsumable == _requiredKey)
            {
                Debug.Log($"[VictoryDoor] Key '{neededKeyName}' accepted. Triggering Victory!");
                IsUnlocked = true;
                GameManager.Instance.ChangeState(GameManager.GameState.Victory);
            }
            else
            {
                Debug.Log($"[VictoryDoor] Access denied. " +
                          $"Required '{neededKeyName}' but player holds '{heldKeyName}'. " +
                          "Obtain the correct key item and try again.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR UTILITIES & GIZMOS
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        /// <summary>
        /// One-click fix for a BoxCollider that is buried inside the door mesh
        /// and therefore invisible to PlayerInventory's OverlapSphere.
        /// Run via right-click → "Reset Collider Size" in the Inspector.
        /// Adjust the size to match your prefab after running this.
        /// </summary>
        [ContextMenu("Reset Collider Size")]
        private void ResetColliderSize()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                Debug.LogWarning("[VictoryDoor] No BoxCollider found on this GameObject. " +
                                 "Add one and run this again.", this);
                return;
            }

            box.center = Vector3.zero;
            box.size   = new Vector3(3f, 5f, 2f);   // Visible, walk-through interactable volume.

            EditorUtility.SetDirty(this);
            Debug.Log("[VictoryDoor] BoxCollider reset to (3, 5, 2). " +
                      "Adjust size in the Inspector to fit your door prefab.", this);
        }

        private void OnDrawGizmos()
        {
            // Draw a gold wire cube to make the door visible in the Scene view.
            Gizmos.color = new Color(1f, 0.84f, 0f, 0.7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            // Small icon above the door.
            Handles.Label(
                transform.position + Vector3.up * 1.5f,
                $"[VictoryDoor]\n" +
                $"Key: {(_requiredKey != null ? _requiredKey.DisplayName : "NOT SET")}\n" +
                $"{(IsUnlocked ? "UNLOCKED" : "LOCKED")}");
        }
#endif
    }
}
