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

using System.Collections;
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

        [Header("Visual Feedback")]
        [Tooltip("HDR color pulsed onto the door emission when the player is in range.")]
        [SerializeField] private Color _approachGlowColor = new Color(1.2f, 0.8f, 0f, 1f);  // amber HDR

        [Tooltip("HDR color briefly flashed when the player tries to open without the key.")]
        [SerializeField] private Color _denyFlashColor = new Color(2.5f, 0.1f, 0f, 1f);     // red HDR

        [Tooltip("Duration (seconds) of the deny flash cycle.")]
        [SerializeField] [Range(0.1f, 1f)] private float _flashDuration = 0.35f;

        // ── IDoorLock implementation ──────────────────────────────────────────
        // DoorController queries this via the interface to veto OpenDoor().
        // IsLocked is true until the player uses the correct key.
        public bool IsLocked => !IsUnlocked;

        // Exposed so external systems (e.g. debug tools) can read the state.
        public bool IsUnlocked { get; private set; } = false;

        // ── Renderer cache for emission effects ──────────────────────────────
        // Cached once in Awake — zero GetComponent calls during gameplay.
        // MaterialPropertyBlock lets us tint per-renderer without creating
        // new Material instances, keeping the asset database clean.
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private Coroutine _flashCoroutine;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            // Cache all renderers in this hierarchy once.
            // Used by both OnPlayerApproach (glow) and the deny flash.
            _renderers = GetComponentsInChildren<Renderer>();
            _propBlock = new MaterialPropertyBlock();

            // Ensure emission keyword is enabled on every material so the
            // property block colour actually shows at runtime.
            foreach (Renderer r in _renderers)
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m != null)
                        m.EnableKeyword("_EMISSION");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PROXIMITY FEEDBACK
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Call this when the player enters the interaction radius of this door.
        /// Lights up a subtle amber emission to signal "this object is interactive".
        /// Wire up from InteractionDebugger, a ProximityTrigger, or
        /// PlayerInventory.TryWorldInteract (before the Interact() call).
        /// </summary>
        public void OnPlayerApproach()
        {
            if (IsUnlocked) return;
            SetEmission(_approachGlowColor);
        }

        /// <summary>
        /// Call this when the player leaves the interaction radius.
        /// Extinguishes the approach glow (unless a deny flash is running).
        /// </summary>
        public void OnPlayerLeave()
        {
            if (_flashCoroutine != null) return;   // Let the flash finish first.
            SetEmission(Color.black);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IWorldInteractable
        // ─────────────────────────────────────────────────────────────────────

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

                // Kill any active flash before opening — the door is going away.
                StopDenyFlash();
                SetEmission(Color.black);

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

                // ── Deny flash ───────────────────────────────────────────────
                // Briefly pulse the door red to give clear visual feedback that
                // the interaction was rejected. The flash restores the emission
                // to its idle state (black) when done.
                StopDenyFlash();
                _flashCoroutine = StartCoroutine(FlashDenyColor());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VISUAL FEEDBACK HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fades emission to <see cref="_denyFlashColor"/>, holds briefly,
        /// then fades it back to black. One pulse only — clean and readable.
        /// </summary>
        private IEnumerator FlashDenyColor()
        {
            float half = _flashDuration * 0.5f;

            // Ramp UP to deny color
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                SetEmission(Color.Lerp(Color.black, _denyFlashColor, t / half));
                yield return null;
            }

            SetEmission(_denyFlashColor);

            // Ramp DOWN back to black
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                SetEmission(Color.Lerp(_denyFlashColor, Color.black, t / half));
                yield return null;
            }

            SetEmission(Color.black);
            _flashCoroutine = null;
        }

        /// <summary>
        /// Stops any running deny flash coroutine immediately.
        /// Called before starting a new flash (prevents two running at once)
        /// and when the door unlocks (cleans up without waiting for it to end).
        /// </summary>
        private void StopDenyFlash()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }
        }

        /// <summary>
        /// Applies <paramref name="color"/> to the <c>_EmissionColor</c> property
        /// of every cached Renderer via a MaterialPropertyBlock.
        /// Using a property block avoids instantiating new Material objects,
        /// keeping the asset database and memory clean.
        /// </summary>
        private void SetEmission(Color color)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColorID, color);
                _renderers[i].SetPropertyBlock(_propBlock);
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
