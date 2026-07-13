// =============================================================================
//  DoorController.cs
//  Project : TopDownShooter – Procedural Dungeon System
//
//  PURPOSE
//  -------
//  Handles the physical collision toggle and smooth vertical translation of
//  door prefabs. Replaces the previous alpha-fade approach with an opaque
//  "sink into the floor" animation that is fully compatible with URP Opaque
//  materials and eliminates all transparent-sorting artifacts.
//  Used by RoomController to lock players in during combat encounters and
//  release them when cleared.
//
//  ARCHITECTURE
//  ─────────────
//  • Discovers all child Transforms that own a Renderer (visual parts only).
//  • Caches each visual part's original (closed) localPosition in Awake().
//  • Open position = closedLocalPosition + Vector3.down * _sinkDistance.
//  • A single coroutine interpolates between the two positions using
//    smoothstep easing for a polished feel at no extra cost.
//  • Public API (OpenDoor / CloseDoor) is identical to the previous version —
//    no other scripts require changes.
//  • Supports an IDoorLock veto: if a sibling component on this GameObject
//    implements IDoorLock and reports IsLocked == true, OpenDoor() is a no-op.
//    This prevents RoomController.ClearRoom() from bypassing LockedBossDoor.
//  • isTrigger colliders are never toggled, preserving IWorldInteractable
//    detection and RoomController.OnTriggerEnter activation.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Controls the vertical translation and collision toggle of a door.
    /// Opens by sinking child visual parts below the floor; closes by raising
    /// them back to their original authored positions. Uses 100% Opaque
    /// materials — no alpha transparency required.
    /// </summary>
    public sealed class DoorController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  NESTED TYPES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Caches one visual part's Transform and its authored (closed)
        /// local position so the animation can always return to exactly
        /// the right place regardless of prefab hierarchy depth.
        /// </summary>
        private class VisualPartCache
        {
            /// <summary>The child Transform to move.</summary>
            public Transform Part;

            /// <summary>
            /// The localPosition recorded in Awake — this is the CLOSED state.
            /// </summary>
            public Vector3 ClosedLocalPosition;

            /// <summary>
            /// Derived from ClosedLocalPosition + Vector3.down * sinkDistance.
            /// This is the OPEN (sunken) state.
            /// </summary>
            public Vector3 OpenLocalPosition;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Animation")]
        [Tooltip("Duration of the open/close translation in seconds.")]
        [SerializeField] private float _slideDuration = 0.4f;

        [Tooltip("Distance (world units) the visual parts sink below their " +
                 "resting position when the door is open. " +
                 "Should be at least as tall as your door mesh.")]
        [SerializeField] private float _sinkDistance = 5f;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // Physical colliders — toggled on CloseDoor / OpenDoor.
        private Collider[] _colliders;

        // Visual parts that will be translated during animation.
        private List<VisualPartCache> _visualParts = new List<VisualPartCache>();

        // Active slide coroutine — stopped before launching a new one so that
        // calling OpenDoor() mid-close (or vice-versa) never fights itself.
        private Coroutine _slideCoroutine;

        // Cached lock component — queried once in Awake, zero per-frame cost.
        // Any sibling MonoBehaviour implementing IDoorLock can veto OpenDoor().
        private IDoorLock _lock;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Cache any IDoorLock sibling — e.g. LockedBossDoor on the same prefab root.
            // GetComponent is O(1) and only runs once, so the veto costs nothing at runtime.
            _lock = GetComponent<IDoorLock>();

            // Discover all physical barriers — isTrigger check applied in SetCollidersState.
            _colliders = GetComponentsInChildren<Collider>();

            // ── Discover visual parts ──────────────────────────────────────────
            // We move only Transforms that own a Renderer, avoiding accidental
            // displacement of sockets, spawner nodes, or other non-visual children.
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                Transform t = r.transform;
                Vector3 closed = t.localPosition;

                _visualParts.Add(new VisualPartCache
                {
                    Part               = t,
                    ClosedLocalPosition = closed,
                    OpenLocalPosition  = closed + Vector3.down * _sinkDistance
                });
            }

            // ── Lock-aware initialization ──────────────────────────────────────
            // Locked door  → start in CLOSED position (raised, solid, blocking).
            // Unlocked door → start in OPEN position (sunken, passable) — this is
            //                 the default for combat/corridor doors that only close
            //                 when the player enters the room.
            if (_lock != null && _lock.IsLocked)
            {
                SetToPosition(closed: true);
                SetCollidersState(true);
            }
            else
            {
                SetToPosition(closed: false);
                SetCollidersState(false);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Raises the door to its closed position and enables physics collision.
        /// Safe to call while the door is already closing or mid-animation —
        /// the previous coroutine is stopped and a new one starts from the
        /// current translated position.
        /// </summary>
        public void CloseDoor()
        {
            SetCollidersState(true);

            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlideRoutine(closingDoor: true));
        }

        /// <summary>
        /// Sinks the door below the floor and disables physics collision.
        /// Silently vetoed when a sibling <see cref="IDoorLock"/> reports
        /// <c>IsLocked == true</c> — e.g. when RoomController.ClearRoom()
        /// broadcasts to all doors but the boss door is still key-locked.
        /// </summary>
        public void OpenDoor()
        {
            // ── Lock veto ────────────────────────────────────────────────────
            // A LockedBossDoor (or any IDoorLock) on this same GameObject can
            // block this call until the player uses the key via Interact().
            if (_lock != null && _lock.IsLocked)
            {
                Debug.Log($"[DoorController] OpenDoor() blocked by lock on '{gameObject.name}'. " +
                          "Waiting for the player to use the required key.", this);
                return;
            }

            SetCollidersState(false);

            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlideRoutine(closingDoor: false));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SLIDE ANIMATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Smoothly translates every visual part between its open and closed
        /// local positions over <see cref="_slideDuration"/> seconds.
        /// Uses smoothstep easing (Mathf.SmoothStep) for a satisfying feel.
        /// </summary>
        /// <param name="closingDoor">
        /// <c>true</c>  → animate toward ClosedLocalPosition (raise).<br/>
        /// <c>false</c> → animate toward OpenLocalPosition   (sink).
        /// </param>
        private IEnumerator SlideRoutine(bool closingDoor)
        {
            if (_visualParts.Count == 0)
            {
                _slideCoroutine = null;
                yield break;
            }

            // Sample the current position from the first part so that
            // interrupting a half-finished animation starts from where it is,
            // not from a hard-coded endpoint.
            Vector3 startPos = _visualParts[0].Part.localPosition;

            float elapsed = 0f;

            while (elapsed < _slideDuration)
            {
                elapsed += Time.deltaTime;

                // Clamp t to [0,1] — last delta may overshoot _slideDuration.
                float t = Mathf.Clamp01(elapsed / _slideDuration);

                // Smoothstep: fast start/end, slower in the middle — feels
                // mechanical without requiring an AnimationCurve asset.
                float smooth = Mathf.SmoothStep(0f, 1f, t);

                for (int i = 0; i < _visualParts.Count; i++)
                {
                    VisualPartCache part = _visualParts[i];
                    if (part.Part == null) continue;

                    // Recompute per-part start on first frame using the
                    // cached closed/open endpoints for parts after index 0.
                    Vector3 from = (i == 0)
                        ? startPos
                        : (closingDoor ? part.OpenLocalPosition : part.ClosedLocalPosition);

                    Vector3 to = closingDoor
                        ? part.ClosedLocalPosition
                        : part.OpenLocalPosition;

                    part.Part.localPosition = Vector3.LerpUnclamped(from, to, smooth);
                }

                yield return null;
            }

            // Snap to exact target — eliminates any floating-point drift.
            for (int i = 0; i < _visualParts.Count; i++)
            {
                if (_visualParts[i].Part == null) continue;

                _visualParts[i].Part.localPosition = closingDoor
                    ? _visualParts[i].ClosedLocalPosition
                    : _visualParts[i].OpenLocalPosition;
            }

            _slideCoroutine = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  POSITION UTILITY
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Instantly snaps all visual parts to either the closed or open
        /// position without animation. Used during Awake() initialisation.
        /// </summary>
        private void SetToPosition(bool closed)
        {
            for (int i = 0; i < _visualParts.Count; i++)
            {
                VisualPartCache part = _visualParts[i];
                if (part.Part == null) continue;

                part.Part.localPosition = closed
                    ? part.ClosedLocalPosition
                    : part.OpenLocalPosition;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  COLLISION UTILITY
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Enables or disables every non-trigger <see cref="Collider"/> in
        /// the door hierarchy. isTrigger colliders are intentionally skipped —
        /// they are used for IWorldInteractable detection (VictoryDoor,
        /// LockedBossDoor) and RoomController.OnTriggerEnter room activation.
        /// Disabling them would silently break both systems.
        /// </summary>
        private void SetCollidersState(bool state)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null && !_colliders[i].isTrigger)
                {
                    _colliders[i].enabled = state;
                }
            }
        }
    }
}
