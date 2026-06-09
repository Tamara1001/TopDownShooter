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
//   • IWeapon             = Abstract Strategy   (attack contract)
//   • IWeaponConfigurable = Configuration hook   (stat injection)
//   • MeleeWeapon         = Concrete Strategy    (this file)
//   • EnemyBrain / PlayerCombat = Context        (calls ExecuteAttack())
//
// PART 2 — IWeaponConfigurable:
//   Configure(WeaponDataSO) is called once by the Context immediately
//   after this component is activated/instantiated. It overwrites the
//   Inspector-default _damage with the SO's BaseDamage, so a single
//   prefab can serve multiple weapon archetypes with different power levels.
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
using TopDownShooter.Inventory;

namespace TopDownShooter.Enemy
{
    /// <summary>
    /// Concrete <see cref="IWeapon"/> strategy for melee attacks.
    /// Also implements <see cref="IWeaponConfigurable"/> so a Context
    /// (e.g. <see cref="TopDownShooter.Combat.PlayerCombat"/> or
    /// <see cref="EnemyBrain"/>) can push <see cref="WeaponDataSO"/> stats
    /// at runtime without this class importing the SO directly.
    /// Uses a Physics overlap sphere combined with a dot-product
    /// cone check to hit only targets in front of the attacker within
    /// a configurable arc — with zero per-call heap allocations.
    /// </summary>
    public sealed class MeleeWeapon : MonoBehaviour, IWeapon, IWeaponConfigurable
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
        // IWEAPONCONFIGURABLE IMPLEMENTATION
        // ----------------------------------------------------------

        /// <summary>
        /// Called once by the Context (e.g. <see cref="TopDownShooter.Combat.PlayerCombat"/>)
        /// immediately after this component is instantiated or activated.
        /// Overwrites the Inspector-default <see cref="_damage"/> with
        /// <see cref="WeaponDataSO.BaseDamage"/> so a single prefab can serve
        /// multiple weapon archetypes with different power levels.
        ///
        /// <para>
        /// Only <c>_damage</c> is injected here. Geometric fields
        /// (<c>_attackRadius</c>, <c>_attackAngle</c>, <c>_cosHalfAngle</c>)
        /// remain under Inspector control because they define the shape of the
        /// hitbox — a shared prefab concern, not a per-SO data concern.
        /// </para>
        /// </summary>
        /// <param name="stats">The <see cref="WeaponDataSO"/> of the equipped weapon.
        /// Passing <c>null</c> is a no-op: existing Inspector values are kept.</param>
        public void Configure(WeaponDataSO stats)
        {
            if (stats == null)
            {
                Debug.LogWarning($"[MeleeWeapon] '{name}': Configure called with null " +
                                 "WeaponDataSO. Keeping existing Inspector _damage value.", this);
                return;
            }

            _damage = stats.BaseDamage;

            // ► Part 3: inject stats.AttackRange into _attackRadius here
            //             once WeaponDataSO gains a dedicated range field.

#if UNITY_EDITOR
            Debug.Log($"[MeleeWeapon] '{name}': Configured via SO — " +
                      $"_damage overridden to {_damage}.");
#endif
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
