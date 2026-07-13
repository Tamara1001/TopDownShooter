// =============================================================================
// LockedBossDoor.cs
// -----------------------------------------------------------------------------
// PURPOSE:
//   Acts as a physical world lock for Boss Room doors. Implements the
//   IWorldInteractable interface to intercept player interaction events.
//   Checks the player's inventory for the correct ConsumableDataSO key before
//   commanding the DoorController to open.
//
//   Also implements IDoorLock so that DoorController.OpenDoor() can query
//   the lock state and refuse to open until the key is used. This prevents
//   RoomController.ClearRoom() from bypassing the key requirement.
// =============================================================================

using UnityEngine;
using TopDownShooter.Inventory;
using TopDownShooter.Player;
using TopDownShooter.Interaction;
using TopDownShooter.Dungeon;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TopDownShooter.World
{
    [RequireComponent(typeof(Collider))]
    public class LockedBossDoor : MonoBehaviour, IWorldInteractable, IDoorLock
    {
        [Header("Lock Configuration")]
        [Tooltip("The ScriptableObject key required in the player's consumable slot to unlock this door.")]
        [SerializeField] private ConsumableDataSO _requiredKey;
        
        [Tooltip("The visual and physical door controller to act upon when unlocked.")]
        [SerializeField] private DoorController _doorController;

        // ── IDoorLock implementation ──────────────────────────────────────────
        // DoorController queries this via the interface to veto OpenDoor().
        // IsLocked is true until the player uses the correct key.
        public bool IsLocked => !IsUnlocked;

        // Exposed so external systems (e.g. debug tools) can read the state.
        public bool IsUnlocked { get; private set; } = false;

        /// <summary>
        /// Called by PlayerInventory when the player presses the Interact input.
        /// </summary>
        public void Interact(PlayerInventory inventory)
        {
            // Guard clauses
            if (IsUnlocked || _doorController == null) return;

            if (_requiredKey == null)
            {
                Debug.LogError("[LockedBossDoor] No key assigned in the inspector!", this);
                return;
            }

            // Evaluate if the player is holding the required key
            if (inventory != null && inventory.CurrentConsumable == _requiredKey)
            {
                Debug.Log("[LockedBossDoor] Key accepted! Unlocking boss door.");
                IsUnlocked = true;  // IDoorLock.IsLocked becomes false — DoorController.OpenDoor() unblocked.
                _doorController.OpenDoor();
                
                // Disable this script (and optionally its collider if it's strictly for interaction)
                // so the prompt never appears again.
                this.enabled = false;
                
                Collider col = GetComponent<Collider>();
                if (col != null && col.isTrigger)
                {
                    col.enabled = false;
                }
            }
            else
            {
                string held = inventory?.CurrentConsumable?.DisplayName ?? "None";
                Debug.Log($"[LockedBossDoor] Locked. Requires '{_requiredKey.DisplayName}', " +
                          $"but player holds '{held}'.");
                // Future Hook: Play "Locked" SFX here or show a temporary UI floating text.
            }
        }

        // ----------------------------------------------------------
        // EDITOR UTILITIES
        // ----------------------------------------------------------

        /// <summary>
        /// One-click fix for a BoxCollider that is buried inside the door mesh
        /// and therefore invisible to PlayerInventory's OverlapSphere.
        /// Run via right-click → "Reset Collider Size" in the Inspector.
        /// Adjust the size to match your door prefab after running this.
        /// </summary>
        [ContextMenu("Reset Collider Size")]
        private void ResetColliderSize()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                Debug.LogWarning("[LockedBossDoor] No BoxCollider found on this GameObject. " +
                                 "Add one and run this again.", this);
                return;
            }

            box.center = Vector3.zero;
            box.size   = new Vector3(3f, 5f, 2f);   // Visible, walk-through interactable volume.

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            Debug.Log("[LockedBossDoor] BoxCollider reset to (3, 5, 2). " +
                      "Adjust size in the Inspector to fit your door prefab.", this);
#endif
        }

        // ----------------------------------------------------------
        // EDITOR GIZMOS
        // ----------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Draw a red wire cube at the lock's position
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(2f, 2f, 0.5f));
            
            // Draw a label above it
            Handles.Label(transform.position + Vector3.up * 2.5f,
                $"[Locked Boss Door]\n{(IsUnlocked ? "UNLOCKED" : "LOCKED")}");
        }
#endif
    }
}
