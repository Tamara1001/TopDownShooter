// =============================================================================
// LockedBossDoor.cs
// -----------------------------------------------------------------------------
// PURPOSE:
//   Acts as a physical world lock for Boss Room doors. Implements the
//   IWorldInteractable interface to intercept player interaction events.
//   Checks the player's inventory for the correct ConsumableDataSO key before
//   commanding the DoorController to open.
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
    public class LockedBossDoor : MonoBehaviour, IWorldInteractable
    {
        [Header("Lock Configuration")]
        [Tooltip("The ScriptableObject key required in the player's consumable slot to unlock this door.")]
        [SerializeField] private ConsumableDataSO _requiredKey;
        
        [Tooltip("The visual and physical door controller to act upon when unlocked.")]
        [SerializeField] private DoorController _doorController;

        private bool _isUnlocked = false;

        /// <summary>
        /// Called by PlayerInventory when the player presses the Interact input.
        /// </summary>
        public void Interact(PlayerInventory inventory)
        {
            // Guard clauses
            if (_isUnlocked || _doorController == null) return;

            if (_requiredKey == null)
            {
                Debug.LogError("[LockedBossDoor] No key assigned in the inspector!", this);
                return;
            }

            // Evaluate if the player is holding the required key
            if (inventory != null && inventory.CurrentConsumable == _requiredKey)
            {
                Debug.Log("[LockedBossDoor] Key accepted! Unlocking boss door.");
                _isUnlocked = true;
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
                Debug.Log($"[LockedBossDoor] Locked: Requires {_requiredKey.DisplayName}");
                // Future Hook: Play "Locked" SFX here or show a temporary UI floating text.
            }
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
            Handles.Label(transform.position + Vector3.up * 2.5f, "[Locked Boss Door]");
        }
#endif
    }
}
