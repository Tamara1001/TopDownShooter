// ==============================================================
// RangedWeapon.cs
// --------------------------------------------------------------
// PURPOSE:
//   Concrete Strategy implementation of IWeapon for ranged attacks.
//   Owns and manages a UnityEngine.Pool.ObjectPool<Projectile>,
//   mirroring the architecture of MagicWand.cs used by the player.
//
// STRATEGY PATTERN ROLE: Concrete Strategy
//   • IWeapon       = Abstract Strategy  (contract)
//   • RangedWeapon  = Concrete Strategy  (this file)
//   • EnemyBrain    = Context            (calls ExecuteAttack())
//
// OBJECT POOL LIFECYCLE (mirrors MagicWand.cs):
//   Pool.Get()  →  OnGetProjectile()   → [projectile in flight]
//                                              ↓  (on collision / lifetime)
//                                       Projectile.ReturnToPool()
//                                              ↓
//                                       Pool.Release()
//                                              ↓
//                                       OnReleaseProjectile()
//
// STRAIGHT-LINE TRAVEL CONTRACT:
//   The projectile's rotation is set to match the firePoint at the
//   moment of firing. Projectile.MoveForward() translates along
//   local +Z (Space.Self), so the baked rotation steers it — no
//   separate velocity vector, no homing logic, no Rigidbody forces.
//
// PERFORMANCE CONTRACT:
//   • Instantiate() is called ONLY inside CreateProjectile(), which
//     the pool invokes only when the inactive pool is exhausted.
//   • All per-shot work is: one Pool.Get() call + two transform
//     writes. Zero allocations per attack.
// ==============================================================

using UnityEngine;
using UnityEngine.Pool;
using TopDownShooter.Combat;

namespace TopDownShooter.Enemy
{
    /// <summary>
    /// Concrete <see cref="IWeapon"/> strategy for ranged attacks.
    /// Fires pooled <see cref="Projectile"/> instances from a
    /// <see cref="_firePoint"/> Transform in a straight line.
    /// Also implements <see cref="IWeaponConfigurable"/> so a
    /// <see cref="TopDownShooter.Inventory.WeaponDataSO"/> can push damage
    /// at equip time without the weapon knowing about the SO directly.
    /// </summary>
    public sealed class RangedWeapon : MonoBehaviour, IWeapon, IWeaponConfigurable
    {
        // ----------------------------------------------------------
        // INSPECTOR FIELDS
        // ----------------------------------------------------------

        [Header("Firing")]
        [Tooltip("Empty child Transform at the enemy's muzzle/hands. " +
                 "Projectiles spawn at this position and inherit its rotation.")]
        [SerializeField] private Transform _firePoint;

        [Tooltip("Prefab that MUST have a Projectile component, a Kinematic Rigidbody, " +
                 "and a Collider with 'Is Trigger = true'.")]
        [SerializeField] private Projectile _projectilePrefab;

        [Header("Object Pool Settings")]
        [Tooltip("Number of Projectile instances pre-warmed into the pool at Awake. " +
                 "Set to the expected peak simultaneous projectiles for this enemy type.")]
        [SerializeField] private int _poolDefaultCapacity = 5;

        [Tooltip("Hard cap on how many instances the pool holds in reserve. " +
                 "Instances beyond this limit are Destroyed rather than returned.")]
        [SerializeField] private int _poolMaxSize = 20;

        [Header("Damage")]
        [Tooltip("Damage dealt per projectile hit. Overridden by WeaponDataSO.BaseDamage " +
                 "when Configure() is called. Inspector value is the safe fallback.")]
        [SerializeField] private int _damage = 10;

        [Header("Cooldown Settings")]
        [Tooltip("Fallback attack cooldown if no WeaponDataSO configures this weapon.")]
        [SerializeField] private float _defaultCooldown = 1f;

        // ----------------------------------------------------------
        // IWEAPON PROPERTY
        // ----------------------------------------------------------

        public float Cooldown => _baseCooldown * _cooldownMultiplier;

        // Base cooldown, before any multipliers are applied.
        private float _baseCooldown;

        // Dungeon Master multipliers
        private float _damageMultiplier = 1f;
        private float _cooldownMultiplier = 1f;

        // ----------------------------------------------------------
        // PRIVATE STATE
        // ----------------------------------------------------------

        // The managed object pool — lives for the lifetime of this component.
        // IObjectPool<T> is the interface; ObjectPool<T> is the implementation,
        // allowing the pool type to be swapped (e.g. StackPool) without changing
        // call sites.
        private IObjectPool<Projectile> _projectilePool;

        // ----------------------------------------------------------
        // UNITY LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            _baseCooldown = _defaultCooldown;
            ValidateReferences();
            InitialisePool();
        }

        private void OnDestroy()
        {
            // Clear() disposes all pooled instances and prevents the pool
            // from keeping GameObject references alive after this component
            // is destroyed (e.g. when the enemy prefab is unloaded).
            _projectilePool?.Clear();
        }

        // ----------------------------------------------------------
        // IWEAPON IMPLEMENTATION
        // ----------------------------------------------------------

        /// <summary>
        /// Called by <see cref="EnemyBrain.PerformAttack"/> on each
        /// completed attack-cooldown cycle.
        ///
        /// Retrieves one projectile from the pool.
        /// The pool calls <see cref="OnGetProjectile"/> synchronously,
        /// which positions, rotates, and activates the instance before
        /// returning control here.
        ///
        /// Fire-rate gating is handled upstream by <c>AttackState</c>
        /// via <c>EnemyStatsSO.AttackCooldown</c> — this method fires
        /// unconditionally when invoked.
        /// </summary>
        public void ExecuteAttack()
        {
            // Pool.Get() → CreateProjectile() if pool is empty,
            //            → OnGetProjectile() always (positions + activates).
            _projectilePool.Get();

            // ► VFX hook: VFXManager.Instance?.PlayMuzzleFlash(_firePoint.position, _firePoint.rotation);
            // ► Audio hook: _audioSource?.PlayOneShot(_fireSFX);
        }

        public void SetDungeonMultipliers(float damageMultiplier, float cooldownMultiplier)
        {
            _damageMultiplier = damageMultiplier;
            _cooldownMultiplier = cooldownMultiplier;
            Debug.Log($"[RangedWeapon] '{name}' multipliers set: Damagex{_damageMultiplier}, CDx{_cooldownMultiplier}");
        }

        // ----------------------------------------------------------
        // IWEAPONCONFIGURABLE IMPLEMENTATION
        // ----------------------------------------------------------

        /// <summary>
        /// Called once by the equip system immediately after this component
        /// is instantiated as a child of the owner.
        /// Overrides the Inspector's <see cref="_damage"/> with the value from
        /// the <see cref="TopDownShooter.Inventory.WeaponDataSO"/> so each
        /// asset can define a distinct damage value without requiring separate prefabs.
        /// </summary>
        /// <param name="stats">The SO of the weapon that was just picked up.</param>
        public void Configure(TopDownShooter.Inventory.WeaponDataSO stats)
        {
            if (stats == null)
            {
                Debug.LogWarning("[RangedWeapon] Configure called with null WeaponDataSO. " +
                                 "Keeping existing Inspector values.", this);
                return;
            }

            _damage = stats.BaseDamage;
            _baseCooldown = stats.AttackCooldown;
            Debug.Log($"[RangedWeapon] Configured via SO: damage={_damage}, cooldown={_baseCooldown}");
        }

        // ----------------------------------------------------------
        // POOL FACTORY DELEGATES
        // All four delegates are private methods rather than lambdas
        // so they do not capture 'this' into a new closure object
        // every time CreatePool() is called.
        // ----------------------------------------------------------

        /// <summary>
        /// Pool <c>createFunc</c>: allocates one new Projectile instance.
        /// Called by the pool only when its reserve is exhausted.
        /// This is the ONLY site where Instantiate() is called for
        /// this weapon's projectiles.
        /// </summary>
        private Projectile CreateProjectile()
        {
            // Spawn at the fire point — exact position/rotation is
            // overwritten each time in OnGetProjectile(), but we use
            // the fire point here to avoid placing it at world origin.
            Projectile instance = Instantiate(_projectilePrefab,
                                              _firePoint.position,
                                              _firePoint.rotation);

            // Inject the pool reference so the projectile can release
            // itself via _pool.Release(this) on collision or timeout.
            // This is the same pattern used in MagicWand.cs.
            instance.SetPool(_projectilePool);

            // Stamp the current damage value. Re-stamped in OnGetProjectile()
            // so recycled instances always carry the up-to-date value.
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * _damageMultiplier));
            instance.SetDamage(finalDamage);

            // Start deactivated; OnGetProjectile will activate it.
            instance.gameObject.SetActive(false);

            return instance;
        }

        /// <summary>
        /// Pool <c>actionOnGet</c>: called synchronously by <c>Pool.Get()</c>.
        /// Snaps the projectile to the current fire point and activates it.
        ///
        /// ROTATION NOTE:
        /// The firePoint is a child of the enemy root, which is rotated
        /// by AttackState.FacePlayer() before PerformAttack() is called.
        /// Inheriting firePoint.rotation means the projectile travels in
        /// exactly the direction the enemy is facing — no additional
        /// direction calculation needed.
        /// </summary>
        private void OnGetProjectile(Projectile projectile)
        {
            // Snap to muzzle position and inherit world-space rotation.
            projectile.transform.SetPositionAndRotation(
                _firePoint.position,
                _firePoint.rotation);

            // Re-stamp damage so recycled instances always reflect the
            // latest _damage value (e.g. after Configure() is called).
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * _damageMultiplier));
            projectile.SetDamage(finalDamage);

            // Reset internal state: timer, _isReturned flag, SetActive(true).
            projectile.OnGetFromPool();
        }

        /// <summary>
        /// Pool <c>actionOnRelease</c>: called when the projectile returns.
        /// Deactivates the GameObject; it remains in memory for reuse.
        /// </summary>
        private void OnReleaseProjectile(Projectile projectile)
        {
            projectile.OnReturnToPool();
        }

        /// <summary>
        /// Pool <c>actionOnDestroy</c>: called only when the pool is over
        /// its <see cref="_poolMaxSize"/> cap or is being disposed.
        /// This is the only site where Destroy() is called for projectiles.
        /// </summary>
        private void OnDestroyProjectile(Projectile projectile)
        {
            if (projectile != null)
                Destroy(projectile.gameObject);
        }

        // ----------------------------------------------------------
        // POOL INITIALISATION
        // ----------------------------------------------------------

        /// <summary>
        /// Constructs the <see cref="ObjectPool{T}"/> with the four
        /// lifecycle delegates. All heap allocation for the pool
        /// structure happens here, at Awake, not at runtime.
        /// </summary>
        private void InitialisePool()
        {
            _projectilePool = new ObjectPool<Projectile>(
                createFunc:      CreateProjectile,
                actionOnGet:     OnGetProjectile,
                actionOnRelease: OnReleaseProjectile,
                actionOnDestroy: OnDestroyProjectile,
                // collectionCheck catches double-release bugs in Editor.
                // Disabled in builds via the conditional below for performance.
#if UNITY_EDITOR
                collectionCheck: true,
#else
                collectionCheck: false,
#endif
                defaultCapacity: _poolDefaultCapacity,
                maxSize:         _poolMaxSize
            );
        }

        // ----------------------------------------------------------
        // VALIDATION
        // ----------------------------------------------------------

        /// <summary>
        /// Verifies that both required Inspector references are set.
        /// Disables the component with clear error messages if not,
        /// so iteration errors are immediately actionable.
        /// </summary>
        private void ValidateReferences()
        {
            bool valid = true;

            if (_firePoint == null)
            {
                Debug.LogError("[RangedWeapon] 'Fire Point' Transform is not assigned. " +
                               "Create an empty child GameObject at the muzzle position " +
                               "and assign it in the Inspector.", this);
                valid = false;
            }

            if (_projectilePrefab == null)
            {
                Debug.LogError("[RangedWeapon] 'Projectile Prefab' is not assigned. " +
                               "Assign a Prefab with a Projectile component, " +
                               "Kinematic Rigidbody, and Trigger Collider.", this);
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
            if (_firePoint == null) return;

            // Muzzle position dot.
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawSphere(_firePoint.position, 0.07f);

            // Fire direction ray.
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawRay(_firePoint.position, _firePoint.forward * 2f);

            UnityEditor.Handles.Label(
                _firePoint.position + Vector3.up * 0.2f,
                "Fire Point");
        }
#endif
    }
}
