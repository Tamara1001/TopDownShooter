
using UnityEngine;
using UnityEngine.Pool;
using TopDownShooter.Combat;

namespace TopDownShooter.Enemy
{
    /// <summary>
    /// Una estrategia de arma a distancia que dispara múltiples proyectiles agrupados en pool en un arco de dispersión.
    /// Excelente para ataques de jefes (ráfagas de escopeta, novas de 360 grados).
    /// </summary>
    public sealed class SpreadRangedWeapon : MonoBehaviour, IWeapon, IWeaponConfigurable
    {
        // ----------------------------------------------------------
        // INSPECTOR FIELDS
        // ----------------------------------------------------------

        [Header("Firing")]
        [Tooltip("Transform vacío hijo en la boca/centro del enemigo. " +
                 "Los proyectiles se generan en esta posición.")]
        [SerializeField] private Transform _firePoint;

        [Tooltip("Prefab que DEBE tener un componente Projectile, un Rigidbody cinemático, " +
                 "y un Collider con 'Is Trigger = true'.")]
        [SerializeField] private Projectile _projectilePrefab;

        [Header("Spread Settings")]
        [Tooltip("Cuántos proyectiles generar por ataque.")]
        [SerializeField] private int _projectileCount = 8;

        [Tooltip("Ángulo total del arco en grados. 360 = nova circular completa. " +
                 "90 = ráfaga de escopeta orientada hacia el frente.")]
        [SerializeField] private float _spreadAngle = 360f;

        [Header("Object Pool Settings")]
        [SerializeField] private int _poolDefaultCapacity = 20;
        [SerializeField] private int _poolMaxSize = 50;

        [Header("Damage")]
        [SerializeField] private int _damage = 10;

        [Header("Cooldown Settings")]
        [SerializeField] private float _defaultCooldown = 1f;

        // ----------------------------------------------------------
        // VFX
        // ----------------------------------------------------------

        [Header("VFX")]
        [Tooltip("Color del sprite del proyectil y su estela. Para enemigos, configúrelo en el Inspector. " +
                 "Para el jugador, esto es sobrescrito por WeaponDataSO en tiempo de ejecución.")]
        [SerializeField] private Color _projectileColor = Color.white;

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
        /// Rotación almacenada en caché mutada por iteración del bucle dentro de ExecuteAttack.
        /// Leída por OnGetProjectile (y CreateProjectile) para que la llamada de retorno del pool
        /// sepa en qué ángulo generar la bala.
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
            _projectileColor = stats.ProjectileTrailColor;
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
            
            projectile.SetColor(_projectileColor);
            
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
