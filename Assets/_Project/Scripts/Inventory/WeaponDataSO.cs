


using UnityEngine;

namespace TopDownShooter.Inventory
{
    // ─────────────────────────────────────────────────────────────────────────
    //  RESOURCE TYPE ENUM  (Part 4)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Determina qué recurso del jugador (si existe alguno) se consume por ataque.
    /// Configurado en el asset <see cref="WeaponDataSO"/> y leído por
    /// <see cref="TopDownShooter.Combat.PlayerCombat"/> antes de cada disparo.
    /// </summary>
    public enum WeaponResourceType
    {
        /// <summary>Esta arma no tiene costo de recursos. Siempre se dispara libremente.</summary>
        None,
        /// <summary>Consume Maná. Usado por armas mágicas (báculos, grimorios).</summary>
        Mana,
        /// <summary>Consume Energía. Usado por armas físicas (dagas, arcos).</summary>
        Energy
    }

    /// <summary>
    /// Plantilla ScriptableObject para objetos de arma equipables.
    /// Colóquela en una ranura y el jugador obtendrá acceso a un nuevo patrón de ataque.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewWeaponData",
        menuName = "TopDownShooter/Inventory/Weapon Data")]
    public sealed class WeaponDataSO : ItemDataSO
    {
        // ─────────────────────────────────────────────────────────────────────
        //  WEAPON STATS  (Part 2 expansion stubs)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Weapon Stats")]
        [Tooltip("Daño base infligido por impacto. Leído por la estrategia IWeapon.")]
        [Min(1)]
        [SerializeField] private int _baseDamage = 10;

        [Tooltip("Segundos mínimos entre disparos / golpes consecutivos.")]
        [Min(0.05f)]
        [SerializeField] private float _attackCooldown = 0.25f;

        // ─────────────────────────────────────────────────────────────────────
        //  RESOURCE COST  (Part 4)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Resource Cost")]
        [Tooltip("Qué recurso del jugador consume esta arma en cada ataque. None = de uso gratuito, Mana = mágico, Energy = físico.")]
        [SerializeField] private WeaponResourceType _resourceType = WeaponResourceType.None;

        [Tooltip("Cantidad del recurso elegido consumido por ataque. Se ignora cuando ResourceType es None.")]
        [Min(0)]
        [SerializeField] private int _resourceCost = 0;

        // ─────────────────────────────────────────────────────────────────────
        //  WEAPON LOGIC PREFAB  (Part 2)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Weapon Logic")]
        [Tooltip("Un prefab cuyo MonoBehaviour raíz implementa IWeapon (ej. MagicWand, MeleeWeapon, RangedWeapon). PlayerCombat instanciará esto como hijo del Player y llamará a IWeaponConfigurable.Configure() en él. El prefab DEBE tener exactamente un componente IWeapon en su raíz.")]
        [SerializeField] private MonoBehaviour _weaponLogicPrefab;

        // ─────────────────────────────────────────────────────────────────────
        //  VFX
        // ─────────────────────────────────────────────────────────────────────

        [Header("VFX")]
        [Tooltip("Color del rastro (TrailRenderer) de los proyectiles de esta arma. " +
                 "Se inyecta en el proyectil via Projectile.SetTrailColor() justo " +
                 "después de obtenerlo del pool. Usa el alpha del color para controlar " +
                 "la opacidad inicial; el trail se desvanece a transparente automáticamente.")]
        [SerializeField] public Color projectileTrailColor = Color.white;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC GETTERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Daño base por ataque, utilizado por la estrategia IWeapon.</summary>
        public int                BaseDamage        => _baseDamage;

        /// <summary>Intervalo mínimo en segundos entre ataques consecutivos.</summary>
        public float              AttackCooldown     => _attackCooldown;

        /// <summary>
        /// Qué recurso (Maná, Energía o Ninguno) gasta esta arma por disparo.
        /// Leído por <see cref="TopDownShooter.Combat.PlayerCombat"/> antes de cada ataque.
        /// </summary>
        public WeaponResourceType ResourceType       => _resourceType;

        /// <summary>
        /// Cuánto de <see cref="ResourceType"/> se consume por ataque.
        /// Se ignora cuando <see cref="ResourceType"/> es <see cref="WeaponResourceType.None"/>.
        /// </summary>
        public int                ResourceCost       => _resourceCost;

        /// <summary>
        /// El MonoBehaviour prefab PlayerCombat instancia como hijo del Player
        /// al equipar esta arma. Debe implementar <see cref="TopDownShooter.Combat.IWeapon"/>.
        /// </summary>
        public MonoBehaviour      WeaponLogicPrefab  => _weaponLogicPrefab;

        /// <summary>
        /// Color del rastro del proyectil. Inyectado en cada proyectil en el momento
        /// del disparo vía <see cref="TopDownShooter.Combat.Projectile.SetTrailColor"/>.
        /// El gradiente va de este color (opaco) hasta transparente automáticamente.
        /// </summary>
        public Color              ProjectileTrailColor => projectileTrailColor;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (_weaponLogicPrefab != null &&
                !(_weaponLogicPrefab is TopDownShooter.Combat.IWeapon))
            {
                UnityEngine.Debug.LogWarning(
                    $"[WeaponDataSO] '{name}': The assigned WeaponLogicPrefab " +
                    $"('{_weaponLogicPrefab.GetType().Name}') does not implement IWeapon. " +
                    "PlayerCombat will log an error at runtime.", this);
            }
        }
#endif
    }
}
