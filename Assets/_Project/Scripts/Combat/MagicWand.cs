// =============================================================================
//  MagicWand.cs
//  Author  : [Your Name]
//  Project : TopDownShooter – Protagonist: Lunaria (Mage)
//  Created : 2026
//
//  PURPOSE
//  -------
//  Concrete weapon implementation of the IWeapon Strategy Pattern.
//  Manages an ObjectPool<Projectile> and spawns magic projectiles from a
//  designated FirePoint, inheriting the player's world-space rotation so
//  shots always travel toward the mouse cursor.
//
//  STRATEGY PATTERN ROLE: Concrete Strategy
//  ─────────────────────────────────────────
//  • Implements IWeapon.ExecuteAttack() as its primary contract.
//  • PlayerCombat (Context) only calls ExecuteAttack() — it has zero
//    knowledge of pools, prefabs, fireRate, or firePouints.
//
//  OBJECT POOL ARCHITECTURE
//  ─────────────────────────
//  Unity's native ObjectPool<T> is used (UnityEngine.Pool namespace).
//  The pool lives entirely inside this component; no singleton or
//  global manager is required.
//
//    createFunc      → Instantiate prefab, inject pool reference.
//    actionOnGet     → Reset projectile state, set position/rotation, activate.
//    actionOnRelease → Deactivate (pooled instances are never Destroyed).
//    actionOnDestroy → Destroy the GameObject if the pool is disposed.
//    collectionCheck → Enabled in Debug builds to catch double-release bugs.
//
//  FUTURE HOOKS
//  ► SO    : Replace projectile prefab / fire rate with a WeaponDataSO ref.
//  ► VFX   : Spawn a muzzle-flash particle at FirePoint in FireProjectile().
//  ► Audio : Play a cast SFX in FireProjectile().
//  ► FSM   : Expose bool IsReloading / CanFire for state-machine gates.
// =============================================================================

using UnityEngine;
using UnityEngine.Pool;

namespace TopDownShooter.Combat
{
    /// <summary>
    /// Concrete Strategy: Lunaria's Magic Wand weapon.
    /// Owns and manages an <see cref="ObjectPool{T}"/> of <see cref="Projectile"/>
    /// instances and enforces a fire-rate cooldown between shots.
    /// </summary>
    public sealed class MagicWand : MonoBehaviour, IWeapon
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR-EXPOSED PARAMETERS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Firing")]
        [Tooltip("Transform marking the tip of the wand where projectiles spawn. " +
                 "This should be an empty child GameObject of the player.")]
        [SerializeField] private Transform firePoint;

        [Tooltip("The Projectile prefab to pool and fire. Must have a Projectile " +
                 "component, a Kinematic Rigidbody, and a Trigger Collider.")]
        [SerializeField] private Projectile projectilePrefab;

        [Tooltip("Minimum time in seconds between consecutive shots.")]
        [SerializeField] private float fireRate = 0.25f;

        [Header("Object Pool Settings")]
        [Tooltip("Number of projectile instances created and warmed up at startup.")]
        [SerializeField] private int poolDefaultCapacity = 10;

        [Tooltip("Maximum number of instances the pool will hold in reserve. " +
                 "Instances above this cap are Destroyed rather than pooled.")]
        [SerializeField] private int poolMaxSize = 30;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // The managed pool — created once in Awake(), lives for the scene lifetime.
        private IObjectPool<Projectile> _projectilePool;

        // Timestamp of the last successful shot for fire-rate enforcement.
        private float _lastFireTime = float.NegativeInfinity;

        // Cached transform reference.
        private Transform _transform;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _transform = transform;
            ValidateReferences();
            CreatePool();
        }

        private void OnDestroy()
        {
            // Explicitly dispose the pool to release all managed instances.
            // This is important if the weapon is unequipped/destroyed mid-game.
            _projectilePool?.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IWEAPON IMPLEMENTATION  (Strategy contract)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="PlayerCombat"/> each time the Attack input fires.
        /// Enforces fire-rate gating, then retrieves a pooled projectile and
        /// launches it from <see cref="firePoint"/>.
        /// </summary>
        public void ExecuteAttack()
        {
            if (!CanFire()) return;

            _lastFireTime = Time.time;
            FireProjectile();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  POOL FACTORY METHODS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Initialises the <see cref="ObjectPool{T}"/> with the four lifecycle
        /// delegates. All allocation happens here — nowhere else.
        /// </summary>
        private void CreatePool()
        {
            _projectilePool = new ObjectPool<Projectile>(
                createFunc:      CreateProjectile,
                actionOnGet:     OnGetProjectile,
                actionOnRelease: OnReleaseProjectile,
                actionOnDestroy: OnDestroyProjectile,
                collectionCheck: true,          // Throws if the same instance is released twice (Debug)
                defaultCapacity: poolDefaultCapacity,
                maxSize:         poolMaxSize
            );
        }

        /// <summary>
        /// Pool <c>createFunc</c>: Instantiate a new projectile instance and
        /// inject the pool reference before the instance is ever activated.
        /// This is the ONLY place where Instantiate() is called for projectiles.
        /// </summary>
        private Projectile CreateProjectile()
        {
            // Spawn inactive at the firePoint – exact position/rotation
            // will be set in OnGetProjectile() each time it's retrieved.
            Projectile instance = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

            // Inject the pool so the projectile can release itself on collision.
            instance.SetPool(_projectilePool);

            // Deactivate immediately; the pool will activate it via actionOnGet.
            instance.gameObject.SetActive(false);

            return instance;
        }

        /// <summary>
        /// Pool <c>actionOnGet</c>: Called every time a projectile is retrieved.
        /// Positions and orients the projectile, then activates it.
        ///
        /// ROTATION INHERITANCE:
        /// We copy the player's (firePoint's parent's) world rotation so the
        /// projectile travels in exactly the direction Lunaria is facing.
        /// Since Projectile.MoveForward() uses Space.Self (local +Z), the
        /// baked rotation is all we need — no separate velocity vector required.
        /// </summary>
        private void OnGetProjectile(Projectile projectile)
        {
            // Snap to fire point position and inherit the player's world rotation.
            projectile.transform.SetPositionAndRotation(firePoint.position, firePoint.rotation);

            // Reset the projectile's internal state (timer, flags).
            projectile.OnGetFromPool();
        }

        /// <summary>
        /// Pool <c>actionOnRelease</c>: Called when the projectile returns to the pool.
        /// Deactivates the GameObject — it remains alive in memory for reuse.
        /// </summary>
        private void OnReleaseProjectile(Projectile projectile)
        {
            projectile.OnReturnToPool();
        }

        /// <summary>
        /// Pool <c>actionOnDestroy</c>: Called only when the pool is over capacity
        /// or being disposed. Destroys the excess instance permanently.
        /// </summary>
        private void OnDestroyProjectile(Projectile projectile)
        {
            if (projectile != null)
                Destroy(projectile.gameObject);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FIRE LOGIC
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves one projectile from the pool, which triggers
        /// <see cref="OnGetProjectile"/> to position and activate it.
        ///
        /// ► VFX : Spawn/play muzzle flash at firePoint here.
        /// ► Audio: Play cast sound clip here.
        /// </summary>
        private void FireProjectile()
        {
            // Pool.Get() calls CreateProjectile() if empty, else recycles one.
            _projectilePool.Get();

            // ► VFX : VFXManager.Instance?.PlayMuzzleFlash(firePoint.position, firePoint.rotation);
            // ► Audio: _audioSource?.PlayOneShot(_castSFX);
        }

        /// <summary>
        /// Returns true if enough time has elapsed since the last shot.
        /// </summary>
        private bool CanFire()
        {
            return Time.time >= _lastFireTime + fireRate;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates Inspector references at startup. Disables the script
        /// and logs clear error messages if anything is missing.
        /// </summary>
        private void ValidateReferences()
        {
            bool valid = true;

            if (firePoint == null)
            {
                Debug.LogError("[MagicWand] 'Fire Point' Transform is not assigned in the Inspector. " +
                               "Create an empty child GameObject and assign it.", this);
                valid = false;
            }

            if (projectilePrefab == null)
            {
                Debug.LogError("[MagicWand] 'Projectile Prefab' is not assigned in the Inspector. " +
                               "Assign a Prefab with a Projectile component.", this);
                valid = false;
            }

            if (!valid) enabled = false;
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR VISUALISATION
        // ─────────────────────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            if (firePoint == null) return;

            // Draw the fire point position
            Gizmos.color = new Color(1f, 0.4f, 0.1f);
            Gizmos.DrawSphere(firePoint.position, 0.08f);

            // Draw the fire direction
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            Gizmos.DrawRay(firePoint.position, firePoint.forward * 1.5f);
        }
#endif
    }
}
