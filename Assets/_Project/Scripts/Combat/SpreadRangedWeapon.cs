// =============================================================================
// SpreadRangedWeapon.cs
// -----------------------------------------------------------------------------
// PURPOSE:
//   Concrete Strategy implementation of IWeapon for a "Bullet Hell" ranged attack.
//   Fires multiple Projectile instances simultaneously in a radial spread arc
//   or a full 360-degree nova.
//
// POOLING WORKAROUND:
//   Since ObjectPool.Get() does not accept arbitrary arguments to pass down to
//   OnGetProjectile(), this script calculates the rotation for each bullet in
//   the loop, caches it in _currentSpawnRotation, and then calls Get(). 
//   OnGetProjectile() reads that cached field to instantly snap the projectile
//   to the correct trajectory angle.
// =============================================================================

using UnityEngine;
using UnityEngine.Pool;
using TopDownShooter.Combat;

namespace TopDownShooter.Enemy
{
    /// <summary>
    /// A ranged weapon strategy that fires multiple pooled projectiles in a spread arc.
    /// Perfect for boss attacks (shotgun blasts, 360-novas).
    /// </summary>
    public sealed class SpreadRangedWeapon : MonoBehaviour, IWeapon, IWeaponConfigurable
    {
        // ----------------------------------------------------------
        // INSPECTOR FIELDS
        // ----------------------------------------------------------

        [Header("Firing")]
        [Tooltip("Empty child Transform at the enemy's muzzle/center. " +
                 "Projectiles spawn at this position.")]
        [SerializeField] private Transform _firePoint;

        [Tooltip("Prefab that MUST have a Projectile component, a Kinematic Rigidbody, " +
                 "and a Collider with 'Is Trigger = true'.")]
        [SerializeField] private Projectile _projectilePrefab;

        [Header("Spread Settings")]
        [Tooltip("How many projectiles to spawn per attack.")]
        [SerializeField] private int _projectileCount = 8;

        [Tooltip("Total arc angle in degrees. 360 = full circle nova. " +
                 "90 = front-facing shotgun blast.")]
        [SerializeField] private float _spreadAngle = 360f;

        [Header("Object Pool Settings")]
        [SerializeField] private int _poolDefaultCapacity = 20;
        [SerializeField] private int _poolMaxSize = 50;

        [Header("Damage")]
        [SerializeField] private int _damage = 10;

        [Header("Cooldown Settings")]
        [SerializeField] private float _defaultCooldown = 1f;

        // ----------------------------------------------------------
        // IWEAPON PROPERTY
        // ----------------------------------------------------------

        public float Cooldown => _baseCooldown * _cooldownMultiplier;

        private float _baseCooldown;
        private float _damageMultiplier = 1f;
        private float _cooldownMultiplier = 1f;

        // ----------------------------------------------------------
        // PRIVATE STATE
        // ----------------------------------------------------------

        private IObjectPool<Projectile> _projectilePool;
        
        /// <summary>
        /// Cached rotation mutated per-loop iteration inside ExecuteAttack.
        /// Read by OnGetProjectile (and CreateProjectile) so the pool callback
        /// knows which angle to spawn the bullet at.
        /// </summary>
        private Quaternion _currentSpawnRotation;

        // ----------------------------------------------------------
        // UNITY LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            _baseCooldown = _defaultCooldown;
            _currentSpawnRotation = Quaternion.identity;
            ValidateReferences();
            InitialisePool();
        }

        private void OnDestroy()
        {
            _projectilePool?.Clear();
        }

        // ----------------------------------------------------------
        // IWEAPON IMPLEMENTATION
        // ----------------------------------------------------------

        public void ExecuteAttack()
        {
            if (_projectileCount <= 0) return;

            float angleStep = 0f;
            float startAngle = 0f;

            if (_projectileCount > 1)
            {
                if (Mathf.Approximately(_spreadAngle, 360f))
                {
                    // Full circle: space them evenly around the 360 degrees
                    angleStep = 360f / _projectileCount;
                    startAngle = 0f; // Alternatively -180f, results are identical
                }
                else
                {
                    // Arc: split the angle over the gaps between projectiles
                    angleStep = _spreadAngle / (_projectileCount - 1);
                    // Shift the starting point so the cone is centered on the forward vector
                    startAngle = -_spreadAngle / 2f;
                }
            }

            // Fire loop
            for (int i = 0; i < _projectileCount; i++)
            {
                float currentAngleOffset = startAngle + (angleStep * i);
                
                // Calculate rotation strictly on the Y axis (Top-Down perspective)
                Quaternion offsetRotation = Quaternion.Euler(0f, currentAngleOffset, 0f);
                
                // Combine the firePoint's base facing direction with the offset
                _currentSpawnRotation = _firePoint.rotation * offsetRotation;

                // Grab a projectile from the pool.
                // This synchronously triggers OnGetProjectile() which will read _currentSpawnRotation.
                _projectilePool.Get();
            }
        }

        // ----------------------------------------------------------
        // IWEAPONCONFIGURABLE IMPLEMENTATION
        // ----------------------------------------------------------

        public void SetDungeonMultipliers(float damageMultiplier, float cooldownMultiplier)
        {
            _damageMultiplier = damageMultiplier;
            _cooldownMultiplier = cooldownMultiplier;
            Debug.Log($"[SpreadRangedWeapon] '{name}' multipliers set: Damagex{_damageMultiplier}, CDx{_cooldownMultiplier}");
        }

        public void Configure(TopDownShooter.Inventory.WeaponDataSO stats)
        {
            if (stats == null) return;
            _damage = stats.BaseDamage;
            _baseCooldown = stats.AttackCooldown;
        }

        // ----------------------------------------------------------
        // POOL FACTORY DELEGATES
        // ----------------------------------------------------------

        private Projectile CreateProjectile()
        {
            // Instantiate at the base firePoint position and rotation.
            // OnGetProjectile will snap it to the exact calculated _currentSpawnRotation.
            Projectile instance = Instantiate(_projectilePrefab, _firePoint.position, _firePoint.rotation);
            
            instance.SetPool(_projectilePool);
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * _damageMultiplier));
            instance.SetDamage(finalDamage);
            instance.gameObject.SetActive(false);
            
            return instance;
        }

        private void OnGetProjectile(Projectile projectile)
        {
            // Apply position and the explicitly calculated rotation for this specific bullet
            projectile.transform.SetPositionAndRotation(_firePoint.position, _currentSpawnRotation);
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * _damageMultiplier));
            projectile.SetDamage(finalDamage);
            projectile.OnGetFromPool();
        }

        private void OnReleaseProjectile(Projectile projectile)
        {
            projectile.OnReturnToPool();
        }

        private void OnDestroyProjectile(Projectile projectile)
        {
            if (projectile != null)
                Destroy(projectile.gameObject);
        }

        // ----------------------------------------------------------
        // POOL INITIALISATION
        // ----------------------------------------------------------

        private void InitialisePool()
        {
            _projectilePool = new ObjectPool<Projectile>(
                createFunc:      CreateProjectile,
                actionOnGet:     OnGetProjectile,
                actionOnRelease: OnReleaseProjectile,
                actionOnDestroy: OnDestroyProjectile,
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

        private void ValidateReferences()
        {
            bool valid = true;
            if (_firePoint == null)
            {
                Debug.LogError("[SpreadRangedWeapon] 'Fire Point' Transform is not assigned.", this);
                valid = false;
            }
            if (_projectilePrefab == null)
            {
                Debug.LogError("[SpreadRangedWeapon] 'Projectile Prefab' is not assigned.", this);
                valid = false;
            }
            if (!valid) enabled = false;
        }
    }
}
