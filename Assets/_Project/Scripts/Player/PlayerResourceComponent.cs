
using System;
using UnityEngine;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Gestiona los recursos de Maná y Energía del jugador.
    /// Expone <see cref="TryConsumeMana"/> y <see cref="TryConsumeEnergy"/>
    /// para los sistemas de combate/movimiento, y eventos normalizados para la interfaz de usuario (UI).
    /// </summary>
    public sealed class PlayerResourceComponent : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR — MANÁ
        // ─────────────────────────────────────────────────────────────────────

        [Header("Mana")]
        [Tooltip("Puntos de maná máximos. Consumidos por armas mágicas y hechizos.")]
        [Min(1)]
        [SerializeField] private int _maxMana = 100;

        [Tooltip("Puntos de maná regenerados por segundo mientras esté por debajo del máximo.")]
        [Min(0f)]
        [SerializeField] private float _manaRegenPerSecond = 5f;

        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR — ENERGÍA
        // ─────────────────────────────────────────────────────────────────────

        [Header("Energy")]
        [Tooltip("Puntos de energía máximos. Consumidos por armas físicas y el Desplazamiento (Dash).")]
        [Min(1)]
        [SerializeField] private int _maxEnergy = 100;

        [Tooltip("Puntos de energía regenerados por segundo mientras esté por debajo del máximo.")]
        [Min(0f)]
        [SerializeField] private float _energyRegenPerSecond = 15f;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO PRIVADO  (float para una regeneración suave; expuesto como int a través de propiedades)
        // ─────────────────────────────────────────────────────────────────────

        private float _currentMana;
        private float _currentEnergy;

        // Multiplicador del coste de mana inyectado por el sistema D20 Dungeon Master.
        // 1f = coste normal. 2f = coste doble (Fallo Crítico).
        // El llamador es responsable de resetearlo a 1f al despejar la sala.
        private float _manaCostMultiplier = 1f;

        // ─────────────────────────────────────────────────────────────────────
        //  EVENTOS  (Patrón Observador — pasa valores normalizados 0-1 a la interfaz de usuario (UI))
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Se activa cada vez que cambia el maná (consumo o regeneración).
        /// Pasa la fracción normalizada [0, 1] para controlar las barras de llenado y los shaders.
        /// </summary>
        public event Action<float> OnManaChanged;

        /// <summary>
        /// Se activa cada vez que cambia la energía (consumo o regeneración).
        /// Pasa la fracción normalizada [0, 1] para controlar las barras de llenado y los shaders.
        /// </summary>
        public event Action<float> OnEnergyChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  PROPIEDADES PÚBLICAS DE SÓLO LECTURA  (interfaz int sobre estado float)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Maná actual como un entero (truncado, no redondeado).</summary>
        public int CurrentMana   => (int)_currentMana;

        /// <summary>Maná máximo configurado en el Inspector.</summary>
        public int MaxMana       => _maxMana;

        /// <summary>Energía actual como un entero (truncado, no redondeado).</summary>
        public int CurrentEnergy => (int)_currentEnergy;

        /// <summary>Energía máxima configurada en el Inspector.</summary>
        public int MaxEnergy     => _maxEnergy;

        /// <summary>
        /// Multiplicador que se aplica a cada coste de mana antes de consumirlo.
        /// Establecido por el sistema Dungeon Master (Fallo Crítico: 2x).
        /// 1.0 es el valor normal; cualquier valor mayor aumenta el coste efectivo.
        /// </summary>
        public float ManaCostMultiplier => _manaCostMultiplier;

        /// <summary>
        /// Permite al sistema Dungeon Master escalar el coste de mana por ataque.
        /// Llamar con 1f para revertir al comportamiento normal.
        /// </summary>
        /// <param name="multiplier">Multiplicador positivo. 1f = normal, 2f = coste doble.</param>
        public void SetManaCostMultiplier(float multiplier)
        {
            _manaCostMultiplier = Mathf.Max(0.01f, multiplier);
            Debug.Log($"[PlayerResourceComponent] ManaCostMultiplier set to {_manaCostMultiplier:0.##}x.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CICLO DE VIDA DE UNITY
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Comenzar con los recursos llenos. La precisión de flotante le da a la regeneración todo el rango.
            _currentMana   = _maxMana;
            _currentEnergy = _maxEnergy;
        }

        private void Start()
        {
            // Enviar los valores iniciales normalizados para que cualquier UI que se haya suscrito en OnEnable
            // reciba el llenado inicial correcto sin esperar a un evento de cambio.
            OnManaChanged?.Invoke(GetNormalizedMana());
            OnEnergyChanged?.Invoke(GetNormalizedEnergy());
        }

        private void Update()
        {
            RegenerateMana();
            RegenerateEnergy();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  API PÚBLICA — CONSUMO
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Intenta gastar <paramref name="amount"/> de maná.
        /// </summary>
        /// <param name="amount">Costo entero positivo a deducir.</param>
        /// <returns>
        /// <c>true</c> si el maná fue suficiente y se ha deducido.
        /// <c>false</c> si es insuficiente — no se cambia ningún estado.
        /// </returns>
        public bool TryConsumeMana(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning("[PlayerResourceComponent] TryConsumeMana called " +
                                 $"with non-positive amount ({amount}). Ignored.");
                return false;
            }

            // Aplicar el multiplicador del sistema D20 y redondear hacia arriba
            // para garantizar que siempre se cobre al menos el coste base.
            int effectiveCost = Mathf.CeilToInt(amount * _manaCostMultiplier);

            if (_currentMana < effectiveCost) return false;

            _currentMana -= effectiveCost;
            OnManaChanged?.Invoke(GetNormalizedMana());
            return true;
        }

        /// <summary>
        /// Intenta gastar <paramref name="amount"/> de energía.
        /// </summary>
        /// <param name="amount">Costo entero positivo a deducir.</param>
        /// <returns>
        /// <c>true</c> si la energía fue suficiente y se ha deducido.
        /// <c>false</c> si es insuficiente — no se cambia ningún estado.
        /// </returns>
        public bool TryConsumeEnergy(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning("[PlayerResourceComponent] TryConsumeEnergy called " +
                                 $"with non-positive amount ({amount}). Ignored.");
                return false;
            }

            if (_currentEnergy < amount) return false;

            _currentEnergy -= amount;
            OnEnergyChanged?.Invoke(GetNormalizedEnergy());
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  API PÚBLICA — CONSULTAS NORMALIZADAS
        //  Expuesto para el sondeo de la UI al momento de la vinculación sin esperar a un evento.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Devuelve el maná actual como una fracción normalizada [0, 1].</summary>
        public float GetNormalizedMana()
        {
            if (_maxMana <= 0) return 0f;
            return Mathf.Clamp01(_currentMana / _maxMana);
        }

        /// <summary>Devuelve la energía actual como una fracción normalizada [0, 1].</summary>
        public float GetNormalizedEnergy()
        {
            if (_maxEnergy <= 0) return 0f;
            return Mathf.Clamp01(_currentEnergy / _maxEnergy);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  AYUDANTES PRIVADOS DE REGENERACIÓN
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Regenera el maná por <see cref="_manaRegenPerSecond"/> × deltaTime.
        /// Solo activa <see cref="OnManaChanged"/> cuando el valor realmente cambia
        /// (evita inundar la UI con eventos en cada frame cuando ya está lleno).
        /// </summary>
        private void RegenerateMana()
        {
            if (_currentMana >= _maxMana) return;

            float previous = _currentMana;
            _currentMana = Mathf.Clamp(_currentMana + _manaRegenPerSecond * Time.deltaTime,
                                        0f, _maxMana);

            // Solo activar el evento si el valor cambió significativamente.
            if (!Mathf.Approximately(_currentMana, previous))
            {
                OnManaChanged?.Invoke(GetNormalizedMana());
            }
        }

        /// <summary>
        /// Regenera la energía por <see cref="_energyRegenPerSecond"/> × deltaTime.
        /// Solo activa <see cref="OnEnergyChanged"/> cuando el valor realmente cambia.
        /// </summary>
        private void RegenerateEnergy()
        {
            if (_currentEnergy >= _maxEnergy) return;

            float previous = _currentEnergy;
            _currentEnergy = Mathf.Clamp(_currentEnergy + _energyRegenPerSecond * Time.deltaTime,
                                          0f, _maxEnergy);

            if (!Mathf.Approximately(_currentEnergy, previous))
            {
                OnEnergyChanged?.Invoke(GetNormalizedEnergy());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GIZMOS DE EDITOR / DEPURACIÓN
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        // Expone valores legibles en el Inspector en tiempo de ejecución sin romper
        // el encapsulamiento — los campos siguen siendo privados; estas son etiquetas de solo lectura.
        private void OnValidate()
        {
            if (_maxMana <= 0)
                Debug.LogWarning("[PlayerResourceComponent] MaxMana must be > 0.", this);

            if (_maxEnergy <= 0)
                Debug.LogWarning("[PlayerResourceComponent] MaxEnergy must be > 0.", this);
        }
#endif
    }
}
