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

namespace TopDownShooter.World
{
    /// <summary>
    /// A world object that requires a specific <see cref="ConsumableDataSO"/> Key
    /// to be in the player's consumable slot before granting Victory.
    /// Implements <see cref="IWorldInteractable"/> to integrate with
    /// <see cref="PlayerInventory"/>'s E-key interaction flow.
    /// </summary>
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
            if (_requiredKey == null)
            {
                Debug.LogError("[VictoryDoor] No required key assigned! " +
                               "Assign a ConsumableDataSO to _requiredKey in the Inspector.", this);
                return;
            }

            if (inventory.CurrentConsumable == _requiredKey)
            {
                Debug.Log($"[VictoryDoor] Player used the key '{_requiredKey.DisplayName}'. " +
                          "Door unlocked — Victory!");
                GameManager.Instance.ChangeState(GameManager.GameState.Victory);
            }
            else
            {
                string held = inventory.CurrentConsumable != null
                    ? $"'{inventory.CurrentConsumable.DisplayName}'"
                    : "nothing";

                Debug.Log($"[VictoryDoor] The door requires '{_requiredKey.DisplayName}', " +
                          $"but the player is holding {held}. Access denied.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR GIZMOS
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Draw a gold wire cube to make the door visible in the Scene view.
            Gizmos.color = new Color(1f, 0.84f, 0f, 0.7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            // Small icon above the door.
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1.5f,
                $"[VictoryDoor]\nKey: {(_requiredKey != null ? _requiredKey.DisplayName : "NOT SET")}");
        }
#endif
    }
}
