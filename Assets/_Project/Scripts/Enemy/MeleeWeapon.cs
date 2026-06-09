// ==============================================================
// MeleeWeapon.cs
// --------------------------------------------------------------
// PURPOSE:
//   Concrete Strategy implementation of IWeapon for close-range
//   melee attacks. When the AttackState calls ExecuteAttack(),
//   this component performs a cone-shaped Physics.OverlapSphere
//   query — zero per-frame allocations after warmup — and applies
//   damage to every IDamageable hit inside the arc.
//
// STRATEGY PATTERN ROLE: Concrete Strategy
//   • IWeapon    = Abstract Strategy  (contract)
//   • MeleeWeapon = Concrete Strategy  (this file)
//   • EnemyBrain = Context            (calls ExecuteAttack())
//
// PERFORMANCE CONTRACT:
//   • Physics.OverlapSphereNonAlloc() writes into a pre-allocated
//     _hitBuffer array — NOT OverlapSphere() which allocates a new
//     Collider[] on every call.
//   • Vector3.Dot replaces a cosine / angle check: one multiply-add
//     vs a full Mathf.Acos computation.
//   • No new() allocations inside ExecuteAttack().
//
// DECOUPLING GUARANTEE:
//   • MeleeWeapon knows nothing about FSM states, NavMeshAgent,
//     or enemy health. It only receives an ExecuteAttack() signal
//     and resolves its own spatial logic.
// ==============================================================

using UnityEngine;
using TopDownShooter.Combat;

namespace TopDownShooter.Enemy
{
    /// <summary>
    /// Concrete <see cref="IWeapon"/> strategy for melee attacks.
    /// Uses a Physics overlap sphere combined with a dot-product
    /// cone check to hit only targets in front of the enemy within
    /// a configurable arc — with zero per-call heap allocations.
    /// </summary>
    public sealed class MeleeWeapon : MonoBehaviour, IWeapon
    {
        // ----------------------------------------------------------
        // INSPECTOR FIELDS
        // ----------------------------------------------------------

        [Header("Damage")]
        [Tooltip("Flat damage applied to every IDamageable inside the hit cone.")]
        [SerializeField] private int _damage = 20;

        [Header("Hit Detection")]
        [Tooltip("Radius of the OverlapSphere centered on this transform. " +
                 "Should match (or be slightly larger than) EnemyStatsSO.AttackRange.")]
        [SerializeField] private float _attackRadius = 2f;

        [Tooltip("Half-angle of the attack cone in degrees. " +
                 "90° = full hemisphere in front, 45° = focused forward cone.")]
        [Range(1f, 180f)]
        [SerializeField] private float _attackAngle = 45f;

        [Tooltip("LayerMask for potential targets. Assign the 'Player' layer so the " +
                 "overlap sphere only considers player colliders, skipping terrain, " +
                 "props, and other enemies — no TryGetComponent calls wasted.")]
        [SerializeField] private LayerMask _targetMask;

        [Header("Buffer")]
        [Tooltip("Maximum number of colliders the overlap sphere will record per swing. " +
                 "Increase only if multiple damageable targets can overlap simultaneously.")]
        [SerializeField] private int _hitBufferSize = 8;

        // ----------------------------------------------------------
        // PRIVATE STATE
        // ----------------------------------------------------------

        // Pre-allocated buffer — filled by OverlapSphereNonAlloc().
        // Allocated once in Awake() using the Inspector value; never
        // re-allocated at runtime, eliminating per-attack GC pressure.
        private Collider[] _hitBuffer;

        // Cached dot-product threshold.
        // cos(attackAngle) is computed once in Awake() from the Inspector
        // value so the Tick-rate hot path performs only a single Dot().
        // Dot(forward, dir) > cosHalfAngle  ⟺  angle < attackAngle / 2
        private float _cosHalfAngle;

        // Cached transform reference — avoids the property overhead of
        // accessing UnityEngine.Object.transform in a tight loop.
        private Transform _transform;

        // ----------------------------------------------------------
        // UNITY LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            _transform     = transform;
            _hitBuffer     = new Collider[_hitBufferSize];
            _cosHalfAngle  = Mathf.Cos(_attackAngle * 0.5f * Mathf.Deg2Rad);

            ValidateSetup();
        }

        // ----------------------------------------------------------
        // IWEAPON IMPLEMENTATION
        // ----------------------------------------------------------

        /// <summary>
        /// Called by <see cref="EnemyBrain.PerformAttack"/> on every
        /// completed attack cooldown cycle.
        ///
        /// ALGORITHM:
        ///   1. OverlapSphereNonAlloc fills _hitBuffer with colliders
        ///      on the _targetMask layer — no allocation.
        ///   2. For each collider, compute the normalised direction from
        ///      the enemy to the target.
        ///   3. Dot product against the enemy's forward vector gives
        ///      cos(θ). If cos(θ) ≥ cos(halfAngle) the target is inside
        ///      the cone.
        ///   4. TryGetComponent<IDamageable> and apply damage. Using
        ///      TryGetComponent avoids a null-check allocation path.
        /// </summary>
        public void ExecuteAttack()
        {
            // ── Step 1: broad-phase sphere cast ─────────────────
            // NonAlloc version writes into the pre-allocated buffer.
            // Returns the number of colliders found (≤ _hitBufferSize).
            int hitCount = Physics.OverlapSphereNonAlloc(
                _transform.position,
                _attackRadius,
                _hitBuffer,
                _targetMask);

            // ── Step 2 & 3: narrow-phase cone filter ────────────
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hitBuffer[i];

                // Direction from enemy centre to the collider's centre.
                // We zero the Y component to stay on the horizontal plane,
                // matching the XZ-only facing rotation used in AttackState.
                Vector3 toTarget = hit.transform.position - _transform.position;
                toTarget.y = 0f;

                // Skip the target if they are at exactly the same position
                // (degenerate case — Normalize would produce a zero vector).
                if (toTarget == Vector3.zero) continue;

                // dot(forward, dir_normalized) ≥ cos(halfAngle)
                // ⟹ target is within the forward cone.
                // sqrMagnitude is used to normalise without Sqrt until needed.
                float dot = Vector3.Dot(_transform.forward, toTarget.normalized);

                if (dot < _cosHalfAngle) continue; // Outside cone — skip.

                // ── Step 4: apply damage ─────────────────────────
                // TryGetComponent<T> is allocation-free in modern Unity.
                if (hit.TryGetComponent<IDamageable>(out IDamageable target))
                {
                    target.TakeDamage(_damage);

                    // Logging scoped to the Editor; stripped from builds.
#if UNITY_EDITOR
                    Debug.Log($"[MeleeWeapon] '{name}' hit '{hit.name}' for {_damage} damage.");
#endif
                }
            }

            // ── Step 5: clear buffer references ─────────────────
            // Nulling used slots prevents the GC from keeping
            // destroyed colliders alive after the buffer is reused.
            for (int i = 0; i < hitCount; i++)
                _hitBuffer[i] = null;
        }

        // ----------------------------------------------------------
        // VALIDATION
        // ----------------------------------------------------------

        /// <summary>
        /// Checks that the Inspector is configured correctly and logs
        /// actionable error messages if not. Disables the component
        /// rather than throwing an exception so the rest of the scene
        /// can still run during iteration.
        /// </summary>
        private void ValidateSetup()
        {
            bool valid = true;

            if (_damage <= 0)
            {
                Debug.LogWarning($"[MeleeWeapon] '{name}': Damage is {_damage}. " +
                                 "Set a positive value in the Inspector.", this);
            }

            if (_targetMask.value == 0)
            {
                Debug.LogError($"[MeleeWeapon] '{name}': Target LayerMask is empty. " +
                               "The weapon will never detect any targets. " +
                               "Assign the 'Player' layer in the Inspector.", this);
                valid = false;
            }

            if (_hitBufferSize <= 0)
            {
                Debug.LogError($"[MeleeWeapon] '{name}': Hit buffer size must be > 0.", this);
                valid = false;
            }

            if (!valid) enabled = false;
        }

        // ----------------------------------------------------------
        // EDITOR GIZMOS
        // ----------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw the full sphere radius in transparent red.
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.1f);
            Gizmos.DrawSphere(transform.position, _attackRadius);

            // Draw the attack cone boundary rays on the horizontal plane.
            // Visualises the half-angle on both sides of the forward vector.
            float halfAngleRad = _attackAngle * 0.5f * Mathf.Deg2Rad;
            Vector3 forward    = transform.forward;

            // Left and right cone edges (XZ plane).
            Vector3 leftEdge  = Quaternion.Euler(0f,  _attackAngle * 0.5f, 0f) * forward;
            Vector3 rightEdge = Quaternion.Euler(0f, -_attackAngle * 0.5f, 0f) * forward;

            Gizmos.color = new Color(1f, 0.3f, 0.0f, 0.8f);
            Gizmos.DrawRay(transform.position, forward   * _attackRadius);
            Gizmos.DrawRay(transform.position, leftEdge  * _attackRadius);
            Gizmos.DrawRay(transform.position, rightEdge * _attackRadius);

            // Label in Scene view.
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.3f,
                $"Melee: {_attackAngle}° / {_attackRadius}m");
        }
#endif
    }
}
