
using System;
using UnityEngine;
using UnityEngine.Pool;
using Unity.Cinemachine;

namespace TopDownShooter.Combat
{
    /// <summary>
    /// Proyectil mágico autopropulsado gestionado en su totalidad por un
    /// <see cref="ObjectPool{T}"/> inyectado desde <see cref="MagicWand"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Projectile : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR-EXPOSED PARAMETERS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Motion")]
        [Tooltip("Velocidad de desplazamiento en unidades por segundo a lo largo de forward local.")]
        [SerializeField] private float projectileSpeed = 18f;

        [Tooltip("Tiempo máximo en segundos antes de que el proyectil regrese automáticamente " +
                 "al pool, incluso si no ha golpeado nada. Evita fugas.")]
        [SerializeField] private float lifetime = 4f;

        [Header("Layer Filtering")]
        [Tooltip("La máscara de capas de objetos ante los cuales este proyectil NO debe reaccionar. " +
                 "Asigne la capa 'Player' aquí para evitar la autocolisión.")]
        [SerializeField] private LayerMask ignoreLayers;

        [Header("Game Feel")]
        [Tooltip("CinemachineImpulseSource utilizado para activar la sacudida de la cámara al impactar. " +
                 "Dejar sin asignar para omitir (seguro contra nulos).")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // Inyectado por MagicWand inmediatamente después de pool.Get() — nunca nulo en vuelo.
        private IObjectPool<Projectile> _pool;

        // Daño inyectado por el arma emisora a través de SetDamage().
        // Por defecto es 0 para que una llamada faltante a SetDamage() no cause daño no deseado.
        private int _damage;

        // Realiza un seguimiento del tiempo transcurrido desde que este proyectil se recuperó del pool.
        private float _activeTimer;

        // Transform almacenado en caché para rendimiento (evita el acceso repetido a propiedades).
        private Transform _transform;

        // Bandera de guardia: evita que ReturnToPool() sea llamado dos veces en el
        // mismo frame (por ejemplo, si dos colisionadores se activan en el mismo paso de físicas).
        private bool _isReturned;

        // TrailRenderer cacheado — resuelto una sola vez para evitar
        // GetComponent en cada disparo. Puede ser null si el prefab no lo tiene.
        private TrailRenderer _trailRenderer;
        
        // SpriteRenderer cacheado — para colorear el sprite principal del proyectil.
        private SpriteRenderer _spriteRenderer;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _transform = transform;

            // Verificar que el Collider esté configurado como un Trigger.
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning("[Projectile] El Collider de este proyectil NO está configurado como un " +
                                 "Trigger. Establezca 'Is Trigger = true' en el componente Collider.", this);
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
        /// Inyecta la referencia del pool para que este proyectil pueda liberarse a sí mismo.
        /// Llamado por <see cref="MagicWand"/> inmediatamente después de <c>pool.Get()</c>.
        /// </summary>
        public void SetPool(IObjectPool<Projectile> pool)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool),
                "[Projectile] Pool reference must not be null.");
        }

        /// <summary>
        /// Establece el daño que este proyectil infligirá al impactar.
        /// Llamado por el arma emisora (MagicWand, RangedWeapon) inmediatamente
        /// después de recuperar la instancia del pool, antes de que se active.
        /// </summary>
        /// <param name="damage">Valor de daño entero positivo.</param>
        public void SetDamage(int damage)
        {
            _damage = damage;
        }

        /// <summary>
        /// Aplica un color al <see cref="SpriteRenderer"/> y un degradado al <see cref="TrailRenderer"/>.
        /// El degradado transiciona del color indicado (opaco) hasta completamente
        /// transparente, creando un efecto de estela que se desvanece al final.
        /// <para>
        /// Si el prefab no tiene SpriteRenderer o TrailRenderer, este método es un no-op
        /// parcial seguro — no lanza excepción ni genera GC extra.
        /// </para>
        /// </summary>
        /// <param name="color">
        /// Color base del proyectil y de la estela.
        /// </param>
        public void SetColor(Color color)
        {
            // Resolver el SpriteRenderer de forma perezosa
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (_spriteRenderer != null)
                _spriteRenderer.color = color;

            // Resolver el TrailRenderer de forma perezosa
            if (_trailRenderer == null)
                _trailRenderer = GetComponentInChildren<TrailRenderer>();

            if (_trailRenderer == null) return;   // Prefab sin TrailRenderer — no-op para la estela.

            // Construir el gradiente: color opaco al inicio, transparente al final.
            var gradient = new Gradient();

            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(color.a, 0f),   // Opaco al frente
                    new GradientAlphaKey(0f,            1f)   // Transparente al final
                }
            );

            _trailRenderer.colorGradient = gradient;
        }

        /// <summary>
        /// Llamado por el delegado <c>actionOnGet</c> del pool.
        /// Restablece todo el estado para que una instancia reciclada se comporte como una nueva.
        /// </summary>
        public void OnGetFromPool()
        {
            _isReturned  = false;
            _activeTimer = 0f;
            gameObject.SetActive(true);

            // Limpiar la estela de la vida anterior para que no aparezca
            // un artefacto al reutilizar el proyectil desde el pool.
            if (_trailRenderer == null)
                _trailRenderer = GetComponent<TrailRenderer>();

            if (_trailRenderer != null)
                _trailRenderer.Clear();
        }

        /// <summary>
        /// Llamado por el delegado <c>actionOnRelease</c> del pool.
        /// Oculta el GameObject; el pool lo mantiene vivo para futuras reutilizaciones.
        /// </summary>
        public void OnReturnToPool()
        {
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MOVEMENT
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Desplaza el proyectil a lo largo de su eje forward local en cada frame.
        ///
        /// ¿POR QUÉ EL FORWARD LOCAL?
        /// La rotación del proyectil se copia del jugador al hacer el spawn
        /// (ver MagicWand.FireProjectile). Desplazarse por el eje local +Z significa que la
        /// dirección está horneada en el transform — no se requiere un vector de velocidad
        /// independiente. Esto es más económico y simple que un enfoque basado en físicas.
        /// </summary>
        private void MoveForward()
        {
            _transform.Translate(
                Vector3.forward * (projectileSpeed * Time.deltaTime),
                Space.Self   // Crucial: espacio local = sigue la rotación horneada
            );
        }

        // ─────────────────────────────────────────────────────────────────────
        //  LIFETIME GUARD
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve el proyectil al pool después de <see cref="lifetime"/> segundos,
        /// actuando como una red de seguridad para los proyectiles que nunca golpean nada
        /// (por ejemplo, disparados al aire libre o a través de huecos en el terreno).
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
        /// Detecta cuándo el proyectil se superpone con un Trigger o un Collider sólido.
        ///
        /// FILTRADO DE CAPAS:
        /// Usamos una verificación bit a bit contra <see cref="ignoreLayers"/> para omitir los
        /// propios colisionadores del jugador (la capa CharacterController). Esto evita que
        /// el proyectil regrese inmediatamente al pool en el frame en que se genera
        /// dentro del volumen del colisionador del jugador.
        ///
        /// PUNTOS DE EXTENSIÓN FUTUROS:
        /// ► ICombat  : Llamar a other.GetComponent&lt;IDamageable&gt;()?.TakeDamage(damage)
        /// ► VFX      : Generar un efecto de partículas de impacto antes de regresar al pool.
        /// ► Audio    : AudioSource.PlayClipAtPoint(hitSFX, transform.position)
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & ignoreLayers.value) != 0) return;

            if (other.TryGetComponent<IDamageable>(out IDamageable target))
            {
                target.TakeDamage(_damage);

                // Camera shake on successful hit — skipped if source is unassigned.
                _impulseSource?.GenerateImpulse();
            }

            ReturnToPool();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  POOL RETURN
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve de forma segura este proyectil a su pool exactamente una vez.
        /// La guardia de <see cref="_isReturned"/> evita la doble liberación
        /// si dos colisionadores se activan en el mismo paso de físicas.
        /// </summary>
        private void ReturnToPool()
        {
            if (_isReturned) return;
            _isReturned = true;

            if (_pool == null)
            {
                Debug.LogError("[Projectile] La referencia del pool es nula. " +
                               "Recurriendo a Destroy() — compruebe MagicWand.FireProjectile().", this);
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
