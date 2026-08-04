


using System.Collections;
using UnityEngine;

namespace TopDownShooter.VFX
{
    /// <summary>
    /// Efecto de slash melee: anima escala (pequeño → grande) y alpha
    /// (opaco → transparente) durante <see cref="_duration"/> segundos,
    /// luego desactiva el GameObject para posible reutilización en pool.
    /// Adjuntar al prefab raíz del efecto de slash.
    /// </summary>
    public sealed class MeleeSlashEffect : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Animación")]
        [Tooltip("Duración total del efecto en segundos.")]
        [SerializeField] private float _duration = 0.15f;

        [Tooltip("Escala máxima que alcanza el sprite al final de la animación. " +
                 "La escala inicial siempre comienza en Vector3.zero.")]
        [SerializeField] private Vector3 _targetScale = new Vector3(1.5f, 1.5f, 1f);

        [Tooltip("Curva de evaluación de la escala. " +
                 "Por defecto: lineal (0→1). " +
                 "Usar EaseOut para un look más orgánico.")]
        [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Curva de evaluación del alpha. " +
                 "Por defecto: de 1 a 0 (desvanecimiento completo).")]
        [SerializeField] private AnimationCurve _alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Tooltip("Offset de rotación local aplicado al inicio de cada reproducción del efecto. " +
                 "Permite corregir la orientación del sprite sin modificar el Transform padre. " +
                 "Ejemplo: X=90 para aplanar el sprite en el plano horizontal XZ de un dungeon 3D.")]
        [SerializeField] private Vector3 _rotationOffset = Vector3.zero;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // Referencia cacheada al SpriteRenderer; resuelta en Awake para
        // evitar GetComponent por cada activación en pool.
        private SpriteRenderer _spriteRenderer;

        // Referencia cacheada al Transform.
        private Transform _transform;

        // Coroutine activa — guardada para cancelarla si el objeto se
        // desactiva externamente antes de que termine el efecto.
        private Coroutine _activeCoroutine;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _transform = transform;

            // Buscar en hijos para que el SpriteRenderer pueda vivir en un
            // GameObject hijo (p.ej. un "Visual" con rotación propia) sin
            // romper el script. Sigue funcionando si está en la raíz.
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (_spriteRenderer == null)
            {
                Debug.LogError("[MeleeSlashEffect] No SpriteRenderer found on this GameObject " +
                               "or any of its children. Add a SpriteRenderer to the root or " +
                               "to a child Visual GameObject.", this);
            }
        }

        /// <summary>
        /// Arranca la animación cada vez que el objeto se activa.
        /// Permite reutilización con pool: basta con activar el objeto
        /// en lugar de instanciarlo de nuevo.
        /// </summary>
        private void OnEnable()
        {
            // Cancelar coroutine previa si el efecto se re-activa antes de
            // terminar (p.ej. golpe muy rápido con un pool de un solo elemento).
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }

            _activeCoroutine = StartCoroutine(PlayEffect());
        }

        private void OnDisable()
        {
            // Limpiar la referencia al detener el objeto, para evitar
            // que StopCoroutine intente parar una ya finalizada.
            _activeCoroutine = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EFECTO PRINCIPAL
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Coroutine que anima escala y alpha durante <see cref="_duration"/>
        /// segundos y luego desactiva el GameObject.
        /// </summary>
        private IEnumerator PlayEffect()
        {
            if (_spriteRenderer == null)
            {
                gameObject.SetActive(false);
                yield break;
            }

            // Aplicar el offset de rotación al inicio de cada reproducción.
            // Se suma a la rotación local actual para no sobreescribir la
            // orientación heredada del atacante (dirección del swing).
            _transform.localRotation *= Quaternion.Euler(_rotationOffset);

            float elapsed = 0f;
            Color baseColor = _spriteRenderer.color;

            while (elapsed < _duration)
            {
                // Progreso normalizado [0, 1]
                float t = elapsed / _duration;

                // Animar escala usando la curva configurada en el Inspector.
                _transform.localScale = Vector3.LerpUnclamped(
                    Vector3.zero,
                    _targetScale,
                    _scaleCurve.Evaluate(t));

                // Animar alpha del SpriteRenderer preservando los canales RGB.
                float alpha = _alphaCurve.Evaluate(t);
                _spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

                elapsed += Time.deltaTime;
                yield return null; // Esperar al próximo frame
            }

            // Asegurar estado final limpio antes de desactivar.
            _transform.localScale = _targetScale;
            _spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

            // Desactivar en lugar de Destroy para facilitar el re-uso en pool.
            gameObject.SetActive(false);
        }
    }
}
