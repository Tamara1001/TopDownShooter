


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
                Debug.Log($"[VictoryDoor] Key '{neededKeyName}' accepted.");

                // ── Verificación del modo de juego activo ────────────────────
                // El comportamiento post-victoria depende de si el jugador
                // completó el Tutorial o una partida Normal.
                if (GameManager.Instance.CurrentMode == GameManager.GameMode.Tutorial)
                {
                    // Modo Tutorial: en lugar de mostrar la pantalla de victoria,
                    // se lanza una partida Normal desde cero. StartNewGame() se
                    // encarga de cambiar el modo a Normal y recargar la escena,
                    // lo que dispara la generación procedural en OnSceneLoaded.
                    Debug.Log("[VictoryDoor] Tutorial completed! Starting a fresh Normal run.");
                    GameManager.Instance.StartNewGame();
                    return; // Evitar que el resto de la lógica de victoria se ejecute.
                }

                // Modo Normal: flujo de victoria estándar.
                // El GameManager congela el tiempo y notifica a la UI.
                Debug.Log($"[VictoryDoor] Triggering Victory!");
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
