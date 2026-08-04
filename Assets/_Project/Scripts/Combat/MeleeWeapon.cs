
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using TopDownShooter.Combat;
using TopDownShooter.Inventory;

namespace TopDownShooter.Enemy
{
    /// <summary>
    /// Estrategia concreta de <see cref="IWeapon"/> para ataques cuerpo a cuerpo.
    /// También implementa <see cref="IWeaponConfigurable"/> para que un Contexto
    /// (por ejemplo, <see cref="TopDownShooter.Combat.PlayerCombat"/> o
    /// <see cref="EnemyBrain"/>) pueda inyectar estadísticas de <see cref="WeaponDataSO"/>
    /// en tiempo de ejecución sin que esta clase importe el SO directamente.
    /// Utiliza una esfera de superposición de Physics combinada con una verificación de cono
    /// de producto punto para golpear solo a los objetivos frente al atacante dentro de
    /// un arco configurable, con cero asignaciones de heap por llamada.
    /// </summary>
    public sealed class MeleeWeapon : MonoBehaviour, IWeapon, IWeaponConfigurable
    {
        // ----------------------------------------------------------
        // INSPECTOR FIELDS
        // ----------------------------------------------------------

        [Header("Damage")]
        [Tooltip("Daño infligido por golpe de arma. Sobrescrito por WeaponDataSO.BaseDamage al llamar a Configure(). El valor del Inspector es la alternativa segura.")]
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

        [Header("Hit Detection")]
        [Tooltip("Radio de la OverlapSphere centrada en este transform. Debe coincidir con (o ser ligeramente mayor que) EnemyStatsSO.AttackRange.")]
        [SerializeField] private float _attackRadius = 2f;

        [Tooltip("Semiángulo del cono de ataque en grados. 90° = hemisferio completo en el frente, 45° = cono delantero enfocado.")]
        [Range(1f, 180f)]
        [SerializeField] private float _attackAngle = 45f;

        [Tooltip("LayerMask para objetivos potenciales. Asigne la capa 'Player' para que la esfera de superposición solo considere colisionadores de jugadores, omitiendo terreno, props y otros enemigos — sin llamadas desperdiciadas a TryGetComponent.")]
        [SerializeField] private LayerMask _targetMask;

        [Header("Buffer")]
        [Tooltip("Número máximo de colisionadores que la esfera de superposición registrará por swing. Incremente solo si múltiples objetivos dañables pueden superponerse simultáneamente.")]
        [SerializeField] private int _hitBufferSize = 8;

        [Header("Game Feel")]
        [Tooltip("CinemachineImpulseSource activado una vez por swing si se golpeó al menos un objetivo IDamageable. Dejar sin asignar para omitir (seguro contra nulos).")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        [Header("VFX")]
        [Tooltip("Prefab del efecto de slash que se instancia en cada ataque melee. " +
                 "Debe tener un MeleeSlashEffect en la raíz. " +
                 "Dejar vacío para omitir el efecto (null-safe).")]
        [SerializeField] private GameObject _slashEffectPrefab;

        // ----------------------------------------------------------
        // PRIVATE STATE
        // ----------------------------------------------------------

        // Buffer preasignado — llenado por OverlapSphereNonAlloc().
        // Asignado una vez en Awake() usando el valor del Inspector; nunca
        // se vuelve a asignar en tiempo de ejecución, eliminando la presión del GC por ataque.
        private Collider[] _hitBuffer;

        // Umbral de producto punto almacenado en caché.
        // cos(attackAngle) se calcula una vez en Awake() a partir del valor del Inspector
        // para que la ruta crítica de la tasa de ticks realice solo un único Dot().
        // Dot(forward, dir) > cosHalfAngle  ⟺  ángulo < attackAngle / 2
        private float _cosHalfAngle;

        // Referencia de transform almacenada en caché — evita la sobrecarga de propiedad al
        // acceder a UnityEngine.Object.transform en un bucle cerrado.
        private Transform _transform;

        // Simple inline pool para evitar instanciar efectos de slash sin fin.
        private List<GameObject> _slashPool = new List<GameObject>();

        // ----------------------------------------------------------
        // UNITY LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            _baseCooldown  = _defaultCooldown;
            _transform     = transform;
            _hitBuffer     = new Collider[_hitBufferSize];
            _cosHalfAngle  = Mathf.Cos(_attackAngle * 0.5f * Mathf.Deg2Rad);

            ValidateSetup();
        }

        // ----------------------------------------------------------
        // IWEAPON IMPLEMENTATION
        // ----------------------------------------------------------

        /// <summary>
        /// Llamado por <see cref="EnemyBrain.PerformAttack"/> en cada
        /// ciclo de enfriamiento de ataque completado.
        ///
        /// ALGORITMO:
        ///   1. OverlapSphereNonAlloc llena _hitBuffer con colisionadores
        ///      en la capa _targetMask — sin asignación.
        ///   2. Para cada colisionador, calcula la dirección normalizada desde
        ///      el enemigo hacia el objetivo.
        ///   3. El producto punto contra el vector forward del enemigo da
        ///      cos(θ). Si cos(θ) ≥ cos(halfAngle), el objetivo está dentro
        ///      del cono.
        ///   4. TryGetComponent<IDamageable> y aplica daño. El uso de
        ///      TryGetComponent evita una ruta de asignación de verificación de nulos.
        /// </summary>
        public void ExecuteAttack()
        {
            // ── Paso 1: cast de esfera en fase ancha ─────────────────
            // La versión NonAlloc escribe en el buffer preasignado.
            // Devuelve el número de colisionadores encontrados (≤ _hitBufferSize).
            int hitCount = Physics.OverlapSphereNonAlloc(
                _transform.position,
                _attackRadius,
                _hitBuffer,
                _targetMask);

            // Realizar un seguimiento de si al menos un objetivo resultó dañado en este swing
            // para que disparemos el impulso de la cámara exactamente una vez (sin sacudida múltiple).
            bool hitAnything = false;

            // ── Paso 2 y 3: filtro de cono de fase estrecha ────────────
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hitBuffer[i];

                // Dirección desde el centro del enemigo hacia el centro del colisionador.
                // Ponemos en cero el componente Y para permanecer en el plano horizontal,
                // coincidiendo con la rotación de orientación XZ únicamente utilizada en AttackState.
                Vector3 toTarget = hit.transform.position - _transform.position;
                toTarget.y = 0f;

                // Omitir el objetivo si está exactamente en la misma posición
                // (caso degenerado — Normalize produciría un vector cero).
                if (toTarget == Vector3.zero) continue;

                // dot(forward, dir_normalized) ≥ cos(halfAngle)
                // ⟹ el objetivo está dentro del cono delantero.
                // sqrMagnitude se utiliza para normalizar sin Sqrt hasta que sea necesario.
                float dot = Vector3.Dot(_transform.forward, toTarget.normalized);

                if (dot < _cosHalfAngle) continue; // Fuera del cono — omitir.

                // ── Paso 4: aplicar daño ─────────────────────────
                // TryGetComponent<T> está libre de asignación en el Unity moderno.
                if (hit.TryGetComponent<IDamageable>(out IDamageable target))
                {
                    int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * _damageMultiplier));
                    target.TakeDamage(finalDamage);
                    hitAnything = true;

                    // Logging exclusivo del Editor; eliminado de las builds.
#if UNITY_EDITOR
                    Debug.Log($"[MeleeWeapon] '{name}' hit '{hit.name}' for {finalDamage} damage.");
#endif
                }
            }

            // ── Paso 5: impulso de cámara al golpear con éxito ────────────────────
            // Se dispara una vez por swing independientemente de cuántos objetivos hayan sido golpeados,
            // evitando sacudidas múltiples molestas al tajar a través de una multitud.
            if (hitAnything)
                _impulseSource?.GenerateImpulse();

            // ── Step 6: instanciar o reciclar efecto visual de slash ────────────
            // Reutiliza instancias desactivadas en el pool para no saturar
            // la memoria ni ensuciar la Jerarquía.
            if (_slashEffectPrefab != null)
            {
                GameObject slashInstance = null;

                // Buscar una instancia inactiva en el pool
                for (int i = 0; i < _slashPool.Count; i++)
                {
                    if (!_slashPool[i].activeSelf)
                    {
                        slashInstance = _slashPool[i];
                        break;
                    }
                }

                if (slashInstance == null)
                {
                    // No hay disponibles: instanciar uno nuevo y agregarlo al pool
                    slashInstance = Instantiate(_slashEffectPrefab,
                                                _transform.position,
                                                _transform.rotation);
                    _slashPool.Add(slashInstance);
                }
                else
                {
                    // Reciclar el existente: actualizar posición/rotación ANTES de activar
                    // para que OnEnable evalúe el localRotation correctamente.
                    slashInstance.transform.SetPositionAndRotation(_transform.position, _transform.rotation);
                    slashInstance.SetActive(true);
                }
            }

            // ── Paso 7: limpiar las referencias del buffer ─────────────────────────
            // Anular las ranuras usadas evita que el GC mantenga
            // vivos los colisionadores destruidos después de reutilizar el buffer.
            for (int i = 0; i < hitCount; i++)
                _hitBuffer[i] = null;
        }

        public void SetDungeonMultipliers(float damageMultiplier, float cooldownMultiplier)
        {
            _damageMultiplier = damageMultiplier;
            _cooldownMultiplier = cooldownMultiplier;
            Debug.Log($"[MeleeWeapon] '{name}' multipliers set: Damagex{_damageMultiplier}, CDx{_cooldownMultiplier}");
        }

        // ----------------------------------------------------------
        // IWEAPONCONFIGURABLE IMPLEMENTATION
        // ----------------------------------------------------------

        /// <summary>
        /// Llamado una vez por el Contexto (por ejemplo, <see cref="TopDownShooter.Combat.PlayerCombat"/>)
        /// inmediatamente después de instanciarse o activarse este componente.
        /// Sobrescribe la <see cref="_damage"/> predeterminada del Inspector con
        /// <see cref="WeaponDataSO.BaseDamage"/> para que un solo prefab pueda servir
        /// a múltiples arquetipos de armas con diferentes niveles de potencia.
        ///
        /// <para>
        /// Solo se inyecta <c>_damage</c> aquí. Los campos geométricos
        /// (<c>_attackRadius</c>, <c>_attackAngle</c>, <c>_cosHalfAngle</c>)
        /// permanecen bajo el control del Inspector porque definen la forma del
        /// hitbox — un asunto del prefab compartido, no de los datos individuales del SO.
        /// </para>
        /// </summary>
        /// <param name="stats">El <see cref="WeaponDataSO"/> del arma equipada.
        /// Pasar <c>null</c> no tiene efecto: se mantienen los valores del Inspector existentes.</param>
        public void Configure(WeaponDataSO stats)
        {
            if (stats == null)
            {
                Debug.LogWarning($"[MeleeWeapon] '{name}': Configure called with null " +
                                 "WeaponDataSO. Keeping existing Inspector _damage value.", this);
                return;
            }

            _damage = stats.BaseDamage;
            _baseCooldown = stats.AttackCooldown;

            // ► Parte 3: inyectar stats.AttackRange en _attackRadius aquí
            //             una vez que WeaponDataSO tenga un campo dedicado de rango.

#if UNITY_EDITOR
            Debug.Log($"[MeleeWeapon] '{name}': Configured via SO — " +
                      $"_damage overridden to {_damage}.");
#endif
        }

        // ----------------------------------------------------------
        // VALIDATION
        // ----------------------------------------------------------

        /// <summary>
        /// Verifica que el Inspector esté configurado correctamente y registra
        /// mensajes de error procesables si no es así. Deshabilita el componente
        /// en lugar de lanzar una excepción para que el resto de la escena
        /// pueda seguir ejecutándose durante la iteración.
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
            // Dibujar el radio completo de la esfera en rojo transparente.
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.1f);
            Gizmos.DrawSphere(transform.position, _attackRadius);

            // Dibujar los rayos límite del cono de ataque en el plano horizontal.
            // Visualiza el semiángulo en ambos lados del vector forward.
            float halfAngleRad = _attackAngle * 0.5f * Mathf.Deg2Rad;
            Vector3 forward    = transform.forward;

            // Bordes izquierdo y derecho del cono (plano XZ).
            Vector3 leftEdge  = Quaternion.Euler(0f,  _attackAngle * 0.5f, 0f) * forward;
            Vector3 rightEdge = Quaternion.Euler(0f, -_attackAngle * 0.5f, 0f) * forward;

            Gizmos.color = new Color(1f, 0.3f, 0.0f, 0.8f);
            Gizmos.DrawRay(transform.position, forward   * _attackRadius);
            Gizmos.DrawRay(transform.position, leftEdge  * _attackRadius);
            Gizmos.DrawRay(transform.position, rightEdge * _attackRadius);

            // Etiqueta en la vista de Scene.
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.3f,
                $"Melee: {_attackAngle}° / {_attackRadius}m");
        }
#endif
    }
}
