// =============================================================================
//  InteractionDebugger.cs
//  Project : TopDownShooter
//
//  !! TEMPORARY DIAGNOSTIC SCRIPT — REMOVE BEFORE SHIPPING !!
//  ───────────────────────────────────────────────────────────
//  Attach this to the Player GameObject alongside PlayerInventory.
//  Every <_logInterval> seconds it runs the same OverlapSphere that
//  PlayerInventory.TryWorldInteract() uses and logs what it finds.
//
//  HOW TO READ THE OUTPUT
//  ──────────────────────
//  Case 1 — "No interactables found in range"
//    The OverlapSphere hit nothing on _interactableLayerMask.
//    Sub-case A: "All-layer scan found: VictoryDoor (layer: Default)"
//      → The door exists but is on the WRONG layer. Change it to 'Interactable'.
//    Sub-case B: "All-layer scan found nothing either"
//      → The door is out of range OR has no collider at all.
//
//  Case 2 — "Found interactable: VictoryDoor (layer: Interactable)"
//    The door is correctly detected. If E-key still does nothing, the bug
//    is inside VictoryDoor.Interact() — check the [VictoryDoor] Console logs.
//
//  SETUP
//  ─────
//  • _interactableLayerMask must match the value set on PlayerInventory.
//  • _detectionRadius    must match PlayerInventory._pickupRadius (default 1.5).
// =============================================================================

using UnityEngine;
using TopDownShooter.Interaction;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Temporary diagnostic helper. Runs a periodic OverlapSphere identical to
    /// <see cref="PlayerInventory.TryWorldInteract"/> and logs every
    /// <see cref="IWorldInteractable"/> it finds (or warns when it finds none).
    /// Attach to the Player GameObject; disable or delete before shipping.
    /// </summary>
    public sealed class InteractionDebugger : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Mirror these values from PlayerInventory")]
        [Tooltip("Must match PlayerInventory._pickupRadius (default: 1.5). " +
                 "The OverlapSphere will not detect a door that sits outside this radius.")]
        [SerializeField] private float _detectionRadius = 1.5f;

        [Tooltip("Must match PlayerInventory._interactableLayerMask. " +
                 "If this mask is empty, PlayerInventory will never detect anything.")]
        [SerializeField] private LayerMask _interactableLayerMask;

        [Header("Throttle")]
        [Tooltip("Seconds between each diagnostic scan. " +
                 "Keep at 0.5 or higher to avoid flooding the Console.")]
        [SerializeField] [Range(0.1f, 5f)] private float _logInterval = 0.5f;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        private float _nextLogTime = 0f;

        // Pre-allocated buffer — reused every scan, zero GC allocations.
        private readonly Collider[] _buffer = new Collider[16];

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            // Warn immediately if the mask is unset — this is the #1 silent failure.
            if (_interactableLayerMask.value == 0)
            {
                Debug.LogWarning("[InteractionDebugger] _interactableLayerMask is EMPTY (Nothing). " +
                                 "The scan will never find any interactables. " +
                                 "Set it to the same LayerMask as PlayerInventory._interactableLayerMask.", this);
            }

            Debug.Log($"[InteractionDebugger] Attached to '{gameObject.name}'. " +
                      $"Scanning radius: {_detectionRadius}m every {_logInterval}s. " +
                      $"LayerMask value: {_interactableLayerMask.value} " +
                      $"({LayerMaskToString(_interactableLayerMask)}).", this);
        }

        private void Update()
        {
            if (Time.time < _nextLogTime) return;
            _nextLogTime = Time.time + _logInterval;

            RunDiagnosticScan();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DIAGNOSTIC LOGIC
        // ─────────────────────────────────────────────────────────────────────

        private void RunDiagnosticScan()
        {
            Vector3 origin = transform.position;

            // ── Pass 1: Masked scan (replicates PlayerInventory exactly) ─────
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                _detectionRadius,
                _buffer,
                _interactableLayerMask);

            bool foundAny = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _buffer[i];
                if (col == null) continue;

                // Mirror PlayerInventory: only care about objects with IWorldInteractable.
                if (col.TryGetComponent<IWorldInteractable>(out _))
                {
                    Debug.Log($"[InteractionDebugger] Found interactable: '{col.gameObject.name}' " +
                              $"on layer '{LayerMask.LayerToName(col.gameObject.layer)}' " +
                              $"| Distance: {Vector3.Distance(origin, col.transform.position):F2}m.", this);
                    foundAny = true;
                }
                else
                {
                    // Collider is on the right layer but missing IWorldInteractable —
                    // this is another common setup mistake.
                    Debug.LogWarning($"[InteractionDebugger] Collider '{col.gameObject.name}' " +
                                     $"is on layer '{LayerMask.LayerToName(col.gameObject.layer)}' " +
                                     "but has NO IWorldInteractable component. " +
                                     "Add VictoryDoor / LockedBossDoor to this GameObject.", this);
                }
            }

            // Clear buffer to release stale object references.
            System.Array.Clear(_buffer, 0, hitCount);

            if (foundAny) return;

            // ── Pass 2: All-layer fallback — reveals layer misconfiguration ──
            // If Pass 1 found nothing, scan ALL layers to see if the object
            // is simply on the wrong layer.
            int allHitCount = Physics.OverlapSphereNonAlloc(
                origin,
                _detectionRadius,
                _buffer,
                ~0);                          // ~0 = every layer

            bool foundOnWrongLayer = false;

            for (int i = 0; i < allHitCount; i++)
            {
                Collider col = _buffer[i];
                if (col == null) continue;
                if (col.gameObject == gameObject) continue;   // Skip the Player itself.

                if (col.TryGetComponent<IWorldInteractable>(out _))
                {
                    Debug.LogWarning($"[InteractionDebugger] All-layer scan found: '{col.gameObject.name}' " +
                                     $"(layer: '{LayerMask.LayerToName(col.gameObject.layer)}'). " +
                                     "This object has IWorldInteractable but is NOT on the " +
                                     "_interactableLayerMask. Change its layer in the Inspector " +
                                     "to match PlayerInventory._interactableLayerMask.", this);
                    foundOnWrongLayer = true;
                }
            }

            System.Array.Clear(_buffer, 0, allHitCount);

            if (!foundOnWrongLayer)
            {
                Debug.LogWarning($"[InteractionDebugger] No interactables found in range " +
                                 $"(radius: {_detectionRadius}m). " +
                                 "Either the door is too far away, has no Collider, " +
                                 "or has no IWorldInteractable component.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a human-readable list of layer names included in the mask.
        /// Useful for verifying the mask is set correctly without opening the Inspector.
        /// </summary>
        private static string LayerMaskToString(LayerMask mask)
        {
            if (mask.value == 0) return "Nothing";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                if ((mask.value & (1 << i)) != 0)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(layerName);
                    }
                }
            }
            return sb.Length > 0 ? sb.ToString() : $"Unknown mask {mask.value}";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR GIZMOS
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Semi-transparent fill — shows the exact detection bubble.
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.08f);
            Gizmos.DrawSphere(transform.position, _detectionRadius);

            // Solid wire ring — easy to judge distance at a glance.
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_detectionRadius + 0.2f),
                $"[Debugger] r={_detectionRadius}m");
        }
#endif
    }
}
