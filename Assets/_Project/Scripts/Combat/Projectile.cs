// =============================================================================
//  Projectile.cs
//  Author  : [Your Name]
//  Project : TopDownShooter – Protagonist: Lunaria (Mage)
//  Created : 2026
//
//  PURPOSE
//  -------
//  Drives a single magic projectile that travels in a straight line along its
//  local forward axis (world direction inherited from the player's rotation at
//  the moment of firing). Returns itself to the Object Pool on collision or
//  when its lifetime expires – never uses Destroy().
//
//  ARCHITECTURE NOTES
//  ------------------
//  • Movement is transform-based (no Rigidbody physics), so no gravity or
//    drag is applied. The Rigidbody on the prefab must be set to Kinematic.
//  • The pool reference is injected by MagicWand via SetPool() after Get().
//    This avoids any static coupling or singleton dependency in Projectile.cs.
//  • OnTriggerEnter is used for collision – the Collider on the prefab must
//    have "Is Trigger" = true.
//  • Layer filtering: we check that we did NOT hit the "Player" layer to
//    prevent self-collision on the frame of spawning.
//
//  POOLING LIFECYCLE
//  ─────────────────
//    Pool.Get()   →  OnGetFromPool()  →  [in flight]  →  ReturnToPool()
//                                                              ↓
//                                                       Pool.Release()
//                                                              ↓
//                                                       OnReturnToPool()
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.Pool;

namespace TopDownShooter.Combat
{
    /// <summary>
    /// Self-propelled magic projectile managed entirely by an
    /// <see cref="ObjectPool{T}"/> injected from <see cref="MagicWand"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Projectile : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR-EXPOSED PARAMETERS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Motion")]
        [Tooltip("Travel speed in units per second along local forward.")]
        [SerializeField] private float projectileSpeed = 18f;

        [Tooltip("Maximum time in seconds before the projectile auto-returns " +
                 "to the pool, even if it hasn't hit anything. Prevents leaks.")]
        [SerializeField] private float lifetime = 4f;

        [Header("Layer Filtering")]
        [Tooltip("The layer mask of objects this projectile should NOT react to. " +
                 "Assign the 'Player' layer here to avoid self-collision.")]
        [SerializeField] private LayerMask ignoreLayers;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // Injected by MagicWand immediately after pool.Get() — never null in flight.
        private IObjectPool<Projectile> _pool;

        // Damage injected by the spawning weapon via SetDamage().
        // Defaults to 0 so a missing SetDamage() call causes no unintended damage.
        private int _damage;

        // Tracks elapsed time since this projectile was retrieved from the pool.
        private float _activeTimer;

        // Cached transform for performance (avoids repeated property access).
        private Transform _transform;

        // Guard flag: prevents ReturnToPool() from being called twice in the
        // same frame (e.g. if two colliders trigger on the same physics step).
        private bool _isReturned;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _transform = transform;

            // Validate that the Collider is set as a Trigger.
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning("[Projectile] The Collider on this projectile is NOT set as a " +
                                 "Trigger. Set 'Is Trigger = true' on the Collider component.", this);
            }
        }

        private void Update()
        {
            MoveForward();
            CheckLifetime();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API  (called by MagicWand / pool callbacks)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Injects the pool reference so this projectile can release itself.
        /// Called by <see cref="MagicWand"/> immediately after <c>pool.Get()</c>.
        /// </summary>
        public void SetPool(IObjectPool<Projectile> pool)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool),
                "[Projectile] Pool reference must not be null.");
        }

        /// <summary>
        /// Sets the damage this projectile will deal on impact.
        /// Called by the spawning weapon (MagicWand, RangedWeapon) immediately
        /// after retrieving the instance from the pool, before it goes active.
        /// </summary>
        /// <param name="damage">Positive integer damage value.</param>
        public void SetDamage(int damage)
        {
            _damage = damage;
        }

        /// <summary>
        /// Called by the pool's <c>actionOnGet</c> delegate.
        /// Resets all state so a recycled instance behaves like a fresh one.
        /// </summary>
        public void OnGetFromPool()
        {
            _isReturned  = false;
            _activeTimer = 0f;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Called by the pool's <c>actionOnRelease</c> delegate.
        /// Hides the GameObject; the pool keeps it alive for future reuse.
        /// </summary>
        public void OnReturnToPool()
        {
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MOVEMENT
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Translates the projectile along its local forward axis each frame.
        ///
        /// WHY LOCAL FORWARD?
        /// The projectile's rotation is copied from the player at spawn time
        /// (see MagicWand.FireProjectile). Moving along local +Z means the
        /// direction is baked into the transform — no separate velocity vector
        /// needed. This is cheaper and simpler than a physics-driven approach.
        /// </summary>
        private void MoveForward()
        {
            _transform.Translate(
                Vector3.forward * (projectileSpeed * Time.deltaTime),
                Space.Self   // Crucial: local space = follows the baked rotation
            );
        }

        // ─────────────────────────────────────────────────────────────────────
        //  LIFETIME GUARD
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the projectile to the pool after <see cref="lifetime"/> seconds,
        /// acting as a safety net for projectiles that never hit anything
        /// (e.g. fired into open air or through gaps in the terrain).
        /// </summary>
        private void CheckLifetime()
        {
            _activeTimer += Time.deltaTime;
            if (_activeTimer >= lifetime)
                ReturnToPool();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  COLLISION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Detects when the projectile overlaps a Trigger or solid Collider.
        ///
        /// LAYER FILTERING:
        /// We use a bitwise check against <see cref="ignoreLayers"/> to skip
        /// the player's own colliders (the CharacterController layer). This
        /// prevents the projectile from immediately returning to pool on the
        /// frame it is spawned inside the player's collider volume.
        ///
        /// FUTURE HOOKS:
        /// ► ICombat  : Call other.GetComponent&lt;IDamageable&gt;()?.TakeDamage(damage)
        /// ► VFX      : Spawn a hit-particle effect before returning to pool.
        /// ► Audio    : AudioSource.PlayClipAtPoint(hitSFX, transform.position)
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            // Ignorar capas filtradas (ej. para no golpear a Lunaria)
            if (((1 << other.gameObject.layer) & ignoreLayers.value) != 0) return;

            // Buscar la interfaz y aplicar daño
            if (other.TryGetComponent<IDamageable>(out IDamageable target))
            {
                target.TakeDamage(_damage);
            }

            ReturnToPool();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  POOL RETURN
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Safely returns this projectile to its pool exactly once.
        /// The <see cref="_isReturned"/> guard prevents double-release
        /// if two colliders trigger in the same physics step.
        /// </summary>
        private void ReturnToPool()
        {
            if (_isReturned) return;
            _isReturned = true;

            if (_pool == null)
            {
                Debug.LogError("[Projectile] Pool reference is null. " +
                               "Falling back to Destroy() — check MagicWand.FireProjectile().", this);
                Destroy(gameObject);
                return;
            }

            _pool.Release(this);
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR VISUALISATION
        // ─────────────────────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.6f);
            Gizmos.DrawRay(transform.position, transform.forward * 1.2f);
        }
#endif
    }
}
