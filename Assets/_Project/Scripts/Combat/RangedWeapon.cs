
using UnityEngine;
using UnityEngine.Pool;
using TopDownShooter.Combat;

namespace TopDownShooter.Enemy
{
    /// <summary>
    /// Estrategia concreta de <see cref="IWeapon"/> para ataques a distancia.
    /// Dispara instancias agrupadas en pool de <see cref="Projectile"/> desde un
    /// Transform <see cref="_firePoint"/> en línea recta.
    /// También implementa <see cref="IWeaponConfigurable"/> para que un
    /// <see cref="TopDownShooter.Inventory.WeaponDataSO"/> pueda inyectar el daño
    /// al equiparse sin que el arma conozca el SO directamente.
    /// </summary>
    public sealed class RangedWeapon : MonoBehaviour, IWeapon, IWeaponConfigurable
    {
        // ----------------------------------------------------------
        // INSPECTOR FIELDS
        // ----------------------------------------------------------

        [Header("Firing")]
        [Tooltip("Transform vacío hijo en la boca del arma/manos del enemigo. " +
                 "Los proyectiles se generan en esta posición y heredan su rotación.")]
        [SerializeField] private Transform _firePoint;

        [Tooltip("Prefab que DEBE tener un componente Projectile, un Rigidbody cinemático, " +
                 "y un Collider con 'Is Trigger = true'.")]
        [SerializeField] private Projectile _projectilePrefab;

        [Header("Object Pool Settings")]
        [Tooltip("Número de instancias de Projectile precalentadas en el pool al Awake. " +
                 "Establecer en el pico esperado de proyectiles simultáneos para este tipo de enemigo.")]
        [SerializeField] private int _poolDefaultCapacity = 5;

        [Tooltip("Límite absoluto de cuántas instancias mantiene el pool en reserva. " +
                 "Las instancias que superen este límite se destruyen en lugar de devolverse.")]
        [SerializeField] private int _poolMaxSize = 20;

        [Header("Damage")]
        [Tooltip("Daño infligido por impacto de proyectil. Sobrescrito por WeaponDataSO.BaseDamage " +
                 "cuando se llama a Configure(). El valor del Inspector es la alternativa segura.")]
        [SerializeField] private int _damage = 10;

        [Header("Cooldown Settings")]
        [Tooltip("Enfriamiento de ataque alternativo si ningún WeaponDataSO configura esta arma.")]
        [SerializeField] private float _defaultCooldown = 1f;

        // ----------------------------------------------------------
        // IWEAPON PROPERTY
        // ----------------------------------------------------------

        public float Cooldown => _baseCooldown * _cooldownMultiplier;

        // Enfriamiento base, antes de aplicar cualquier multiplicador.
        private float _baseCooldown;

        // Multiplicadores del Dungeon Master
        private float _damageMultiplier = 1f;
        private float _cooldownMultiplier = 1f;

        // ----------------------------------------------------------
        // PRIVATE STATE
        // ----------------------------------------------------------

        // El pool de objetos gestionado — vive durante el tiempo de vida de este componente.
        // IObjectPool<T> es la interfaz; ObjectPool<T> es la implementación,
        // lo que permite cambiar el tipo de pool (por ejemplo, StackPool) sin cambiar
        // los puntos de llamada.
        private IObjectPool<Projectile> _projectilePool;

        // ----------------------------------------------------------
        // VFX
        // ----------------------------------------------------------

        [Header("VFX")]
        [Tooltip("Color del sprite del proyectil y su estela. Para enemigos, configúrelo en el Inspector. " +
                 "Para el jugador, esto es sobrescrito por WeaponDataSO en tiempo de ejecución.")]
        [SerializeField] private Color _projectileColor = Color.white;

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
            // Clear() destruye todas las instancias del pool y evita que el pool
            // mantenga vivas las referencias a GameObjects después de que se destruya
            // este componente (por ejemplo, cuando se descarga el prefab del enemigo).
            _projectilePool?.Clear();
        }

        // ----------------------------------------------------------
        // IWEAPON IMPLEMENTATION
        // ----------------------------------------------------------

        /// <summary>
        /// Llamado por <see cref="EnemyBrain.PerformAttack"/> en cada
        /// ciclo de enfriamiento de ataque completado.
        ///
        /// Recupera un proyectil del pool.
        /// El pool llama a <see cref="OnGetProjectile"/> de forma síncrona,
        /// lo que posiciona, rota y activa la instancia antes de devolver el control aquí.
        ///
        /// El control de cadencia de fuego se gestiona antes por <c>AttackState</c>
        /// a través de <c>EnemyStatsSO.AttackCooldown</c> — este método se dispara
        /// incondicionalmente cuando se invoca.
        /// </summary>
        public void ExecuteAttack()
        {
            // Pool.Get() → CreateProjectile() si el pool está vacío,
            //            → OnGetProjectile() siempre (posiciona + activa).
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
        /// Llamado una vez por el sistema de equipo inmediatamente después de que este componente
        /// se instancie como hijo del propietario.
        /// Sobrescribe el <see cref="_damage"/> del Inspector con el valor del
        /// <see cref="TopDownShooter.Inventory.WeaponDataSO"/> para que cada
        /// recurso pueda definir un valor de daño distinto sin necesidad de prefabs separados.
        /// </summary>
        /// <param name="stats">El SO del arma que se acaba de recoger.</param>
        public void Configure(TopDownShooter.Inventory.WeaponDataSO stats)
        {
            if (stats == null)
            {
                Debug.LogWarning("[RangedWeapon] Configure llamado con WeaponDataSO nulo. " +
                                 "Manteniendo los valores del Inspector existentes.");
                return;
            }

            _damage = stats.BaseDamage;
            _baseCooldown = stats.AttackCooldown;

            // Cachear el color de la estela para usarlo en OnGetProjectile()
            // sin necesidad de guardar una referencia viva al SO.
            _projectileColor = stats.ProjectileTrailColor;

            Debug.Log($"[RangedWeapon] Configured via SO: damage={_damage}, cooldown={_baseCooldown}, trailColor={_projectileColor}");
        }

        // ----------------------------------------------------------
        // DELEGADOS DE FÁBRICA DE POOL
        // Los cuatro delegados son métodos privados en lugar de lambdas
        // para que no capturen 'this' en un nuevo objeto de clausura
        // cada vez que se llama a InitialisePool().
        // ----------------------------------------------------------

        /// <summary>
        /// Pool <c>createFunc</c>: asigna una nueva instancia de Projectile.
        /// Llamado por el pool solo cuando su reserva está agotada.
        /// Este es el ÚNICO lugar donde se llama a Instantiate() para
        /// los proyectiles de esta arma.
        /// </summary>
        private Projectile CreateProjectile()
        {
            // Generar en el punto de fuego — la posición/rotación exacta se
            // sobrescribe cada vez en OnGetProjectile(), pero usamos
            // el punto de fuego aquí para evitar colocarlo en el origen del mundo.
            Projectile instance = Instantiate(_projectilePrefab,
                                              _firePoint.position,
                                              _firePoint.rotation);

            // Inyectar la referencia del pool para que el proyectil pueda liberarse
            // a sí mismo mediante _pool.Release(this) al colisionar o por tiempo de espera.
            // Este es el mismo patrón utilizado en MagicWand.cs.
            instance.SetPool(_projectilePool);

            // Estampar el valor de daño actual. Se vuelve a estampar en OnGetProjectile()
            // para que las instancias recicladas siempre lleven el valor actualizado.
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * _damageMultiplier));
            instance.SetDamage(finalDamage);

            // Start deactivated; OnGetProjectile will activate it.
            instance.gameObject.SetActive(false);

            return instance;
        }

        /// <summary>
        /// Pool <c>actionOnGet</c>: llamado de forma síncrona por <c>Pool.Get()</c>.
        /// Ajusta el proyectil al punto de fuego actual y lo activa.
        ///
        /// NOTA SOBRE ROTACIÓN:
        /// El firePoint es un hijo del transform raíz del enemigo, que es rotado
        /// por AttackState.FacePlayer() antes de que se llame a PerformAttack().
        /// Heredar firePoint.rotation significa que el proyectil viaja en
        /// la dirección exacta a la que mira el enemigo — no se requiere
        /// cálculo de dirección adicional.
        /// </summary>
        private void OnGetProjectile(Projectile projectile)
        {
            // Ajustar a la posición de la boca del arma y heredar rotación en espacio del mundo.
            projectile.transform.SetPositionAndRotation(
                _firePoint.position,
                _firePoint.rotation);

            // Volver a estampar el daño para que las instancias recicladas siempre reflejen el
            // último valor de _damage (por ejemplo, después de que se llame a Configure()).
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * _damageMultiplier));
            projectile.SetDamage(finalDamage);

            // Aplicar el color de estela configurado en el WeaponDataSO (o Inspector para enemigos).
            // No-op seguro si el prefab no tiene TrailRenderer o SpriteRenderer.
            projectile.SetColor(_projectileColor);

            // Reset internal state: timer, _isReturned flag, SetActive(true).
            projectile.OnGetFromPool();
        }

        /// <summary>
        /// Pool <c>actionOnRelease</c>: llamado cuando el proyectil regresa.
        /// Desactiva el GameObject; permanece en memoria para su reutilización.
        /// </summary>
        private void OnReleaseProjectile(Projectile projectile)
        {
            projectile.OnReturnToPool();
        }

        /// <summary>
        /// Pool <c>actionOnDestroy</c>: llamado solo cuando el pool está por encima
        /// de su capacidad máxima <see cref="_poolMaxSize"/> o se está destruyendo.
        /// Este es el único lugar donde se llama a Destroy() para proyectiles.
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
        /// Construye el <see cref="ObjectPool{T}"/> con los cuatro
        /// delegados de ciclo de vida. Toda la asignación de heap para la estructura
        /// del pool ocurre aquí, en Awake, no en tiempo de ejecución.
        /// </summary>
        private void InitialisePool()
        {
            _projectilePool = new ObjectPool<Projectile>(
                createFunc:      CreateProjectile,
                actionOnGet:     OnGetProjectile,
                actionOnRelease: OnReleaseProjectile,
                actionOnDestroy: OnDestroyProjectile,
                // collectionCheck detecta errores de doble liberación en el Editor.
                // Deshabilitado en builds a través del condicional a continuación por rendimiento.
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
        /// Verifica que ambas referencias requeridas del Inspector estén configuradas.
        /// Deshabilita el componente con mensajes de error claros en caso contrario,
        /// para que los errores de iteración sean procesables de inmediato.
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

            // Punto de la boca del arma.
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawSphere(_firePoint.position, 0.07f);

            // Rayo de dirección de disparo.
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawRay(_firePoint.position, _firePoint.forward * 2f);

            UnityEditor.Handles.Label(
                _firePoint.position + Vector3.up * 0.2f,
                "Fire Point");
        }
#endif
    }
}
