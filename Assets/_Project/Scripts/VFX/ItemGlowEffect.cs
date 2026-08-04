


using UnityEngine;

namespace TopDownShooter.VFX
{
    /// <summary>
    /// Agrega y pulsa una luz puntual sobre un ítem del mundo para que
    /// sea fácilmente visible en el suelo del dungeon.
    /// Adjuntar al GameObject raíz de cualquier prefab de objeto recolectable.
    /// </summary>
    public sealed class ItemGlowEffect : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("Color y Brillo")]
        [Tooltip("Color del resplandor. Elige colores saturados y vibrantes para " +
                 "que el ítem destaque sobre la oscuridad del dungeon.")]
        [SerializeField] private Color _glowColor = new Color(0.4f, 0.9f, 1f, 1f);   // Cyan suave

        [Tooltip("Intensidad mínima de la luz durante el pulso.")]
        [Min(0f)]
        [SerializeField] private float _minIntensity = 0.6f;

        [Tooltip("Intensidad máxima de la luz durante el pulso.")]
        [Min(0f)]
        [SerializeField] private float _maxIntensity = 1.8f;

        [Tooltip("Velocidad de la oscilación en ciclos por segundo (Hz). " +
                 "Valor recomendado: 1.2 – 2.0 para que se perciba vivo sin marear.")]
        [Min(0.1f)]
        [SerializeField] private float _pulseSpeed = 1.4f;

        [Header("Geometría de la Luz")]
        [Tooltip("Radio de la luz puntual en unidades de mundo. " +
                 "Manténlo pequeño (1–3) para que no ilumine salas adyacentes.")]
        [Min(0.1f)]
        [SerializeField] private float _lightRange = 2f;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO PRIVADO
        // ─────────────────────────────────────────────────────────────────────

        // Referencia a la luz; puede ser encontrada en Awake o agregada en código.
        private Light _light;

        // Offset de fase aleatorio por instancia para que varios ítems
        // en la misma habitación no pulsen en sincronía (efecto "parpadeo grupal").
        private float _phaseOffset;

        // ─────────────────────────────────────────────────────────────────────
        //  CICLO DE VIDA DE UNITY
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Buscar un Light existente en el mismo GameObject o en sus hijos.
            _light = GetComponentInChildren<Light>();

            if (_light == null)
            {
                // Crear un GameObject hijo específicamente para la luz,
                // de manera que podamos elevarla sobre el suelo (WebGL friendly).
                GameObject lightGO = new GameObject("GlowLight");
                lightGO.transform.SetParent(transform, false);
                lightGO.transform.localPosition = Vector3.up * 0.5f; // Elevar para no intersectar el suelo
                
                _light = lightGO.AddComponent<Light>();
                Debug.Log($"[ItemGlowEffect] '{name}': Light component added as child in code.", this);
            }

            // Configurar los parámetros de la luz.
            ConfigureLight();

            // Offset aleatorio para que cada ítem pulse en su propia fase.
            _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            PulseIntensity();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CONFIGURACIÓN
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Aplica los parámetros de color, rango e intensidad inicial a la luz.
        /// Se llama una sola vez en Awake, no en Update, para no re-asignar
        /// propiedades estáticas cada frame.
        /// </summary>
        private void ConfigureLight()
        {
            _light.type      = LightType.Point;
            _light.color     = _glowColor;
            _light.range     = _lightRange;
            _light.intensity = _minIntensity;

            // Sin sombras en ítems del suelo para mantener el rendimiento.
            _light.shadows = LightShadows.None;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PULSO
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Oscila la intensidad de la luz entre <see cref="_minIntensity"/> y
        /// <see cref="_maxIntensity"/> usando una función seno para una
        /// transición suave y continua sin cambios abruptos.
        /// </summary>
        private void PulseIntensity()
        {
            // sin() devuelve [-1, 1] → se normaliza a [0, 1] → se mapea al rango [min, max].
            // El offset de fase garantiza que cada ítem tenga un ritmo único.
            float sinValue  = Mathf.Sin(Time.time * _pulseSpeed * Mathf.PI * 2f + _phaseOffset);
            float normalized = (sinValue + 1f) * 0.5f;   // [-1,1] → [0,1]

            _light.intensity = Mathf.Lerp(_minIntensity, _maxIntensity, normalized);
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        //  VALIDACIÓN EN EDITOR
        // ─────────────────────────────────────────────────────────────────────

        private void OnValidate()
        {
            // Advertir si el rango mínimo supera al máximo en el Inspector.
            if (_minIntensity > _maxIntensity)
            {
                Debug.LogWarning($"[ItemGlowEffect] '{name}': _minIntensity ({_minIntensity}) " +
                                 $"es mayor que _maxIntensity ({_maxIntensity}). " +
                                 "La luz no pulsará de forma visible.", this);
            }
        }
#endif
    }
}
