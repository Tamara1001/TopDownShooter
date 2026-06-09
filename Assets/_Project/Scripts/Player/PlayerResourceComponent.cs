// =============================================================================
//  PlayerResourceComponent.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Owns and manages the player's two secondary resources: Mana and Energy.
//  • Mana     — consumed by magical weapons (staves, grimoires).
//  • Energy   — consumed by physical weapons (daggers) and by Dashing.
//  Both regenerate passively every frame at configurable rates.
//
//  ARCHITECTURE (Single Responsibility)
//  ─────────────────────────────────────
//  This component is the single source of truth for resource values.
//  It does NOT know about:
//    - Weapons or combat logic  (those TryConsumeMana / TryConsumeEnergy)
//    - Movement or dash logic   (those TryConsumeEnergy)
//    - UI rendering              (UI subscribes to the normalized events)
//  Its only job: track floats, regen over time, expose a clean API, fire events.
//
//  FLOAT INTERNALS, INT SURFACE
//  ─────────────────────────────
//  Resources are stored as float internally so fractional regen accumulates
//  smoothly across frames. The public properties expose them as int, matching
//  the integer cost contracts used by weapons and abilities.
//
//  ATTACH TO
//  ─────────
//  The Player root GameObject (alongside HealthComponent, PlayerInventory, etc.)
// =============================================================================

using System;
using UnityEngine;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Manages the player's Mana and Energy resources.
    /// Exposes <see cref="TryConsumeMana"/> and <see cref="TryConsumeEnergy"/>
    /// for combat/movement systems, and normalized events for the UI.
    /// </summary>
    public sealed class PlayerResourceComponent : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS — MANA
        // ─────────────────────────────────────────────────────────────────────

        [Header("Mana")]
        [Tooltip("Maximum mana points. Consumed by magical weapons and spells.")]
        [Min(1)]
        [SerializeField] private int _maxMana = 100;

        [Tooltip("Mana points regenerated per second while below maximum.")]
        [Min(0f)]
        [SerializeField] private float _manaRegenPerSecond = 5f;

        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS — ENERGY
        // ─────────────────────────────────────────────────────────────────────

        [Header("Energy")]
        [Tooltip("Maximum energy points. Consumed by physical weapons and Dash.")]
        [Min(1)]
        [SerializeField] private int _maxEnergy = 100;

        [Tooltip("Energy points regenerated per second while below maximum.")]
        [Min(0f)]
        [SerializeField] private float _energyRegenPerSecond = 15f;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE  (float for smooth regen; exposed as int via properties)
        // ─────────────────────────────────────────────────────────────────────

        private float _currentMana;
        private float _currentEnergy;

        // ─────────────────────────────────────────────────────────────────────
        //  EVENTS  (Observer Pattern — pass normalized 0-1 values to the UI)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired whenever mana changes (consumption or regen).
        /// Passes the normalized fraction [0, 1] for driving fill-bars and shaders.
        /// </summary>
        public event Action<float> OnManaChanged;

        /// <summary>
        /// Fired whenever energy changes (consumption or regen).
        /// Passes the normalized fraction [0, 1] for driving fill-bars and shaders.
        /// </summary>
        public event Action<float> OnEnergyChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC READ-ONLY PROPERTIES  (int surface over float state)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Current mana as an integer (truncated, not rounded).</summary>
        public int CurrentMana   => (int)_currentMana;

        /// <summary>Maximum mana configured in the Inspector.</summary>
        public int MaxMana       => _maxMana;

        /// <summary>Current energy as an integer (truncated, not rounded).</summary>
        public int CurrentEnergy => (int)_currentEnergy;

        /// <summary>Maximum energy configured in the Inspector.</summary>
        public int MaxEnergy     => _maxEnergy;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Start at full resources. Float precision gives regen the full range.
            _currentMana   = _maxMana;
            _currentEnergy = _maxEnergy;
        }

        private void Start()
        {
            // Push initial normalized values so any UI that subscribed in OnEnable
            // receives the correct starting fill without waiting for a change event.
            OnManaChanged?.Invoke(GetNormalizedMana());
            OnEnergyChanged?.Invoke(GetNormalizedEnergy());
        }

        private void Update()
        {
            RegenerateMana();
            RegenerateEnergy();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API — CONSUMPTION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to spend <paramref name="amount"/> mana.
        /// </summary>
        /// <param name="amount">Positive integer cost to deduct.</param>
        /// <returns>
        /// <c>true</c> if mana was sufficient and has been deducted.
        /// <c>false</c> if insufficient — no state is changed.
        /// </returns>
        public bool TryConsumeMana(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning("[PlayerResourceComponent] TryConsumeMana called " +
                                 $"with non-positive amount ({amount}). Ignored.");
                return false;
            }

            if (_currentMana < amount) return false;

            _currentMana -= amount;
            OnManaChanged?.Invoke(GetNormalizedMana());
            return true;
        }

        /// <summary>
        /// Attempts to spend <paramref name="amount"/> energy.
        /// </summary>
        /// <param name="amount">Positive integer cost to deduct.</param>
        /// <returns>
        /// <c>true</c> if energy was sufficient and has been deducted.
        /// <c>false</c> if insufficient — no state is changed.
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
        //  PUBLIC API — NORMALIZED QUERIES
        //  Exposed for UI polling at bind time without waiting for an event.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns current mana as a normalized fraction [0, 1].</summary>
        public float GetNormalizedMana()
        {
            if (_maxMana <= 0) return 0f;
            return Mathf.Clamp01(_currentMana / _maxMana);
        }

        /// <summary>Returns current energy as a normalized fraction [0, 1].</summary>
        public float GetNormalizedEnergy()
        {
            if (_maxEnergy <= 0) return 0f;
            return Mathf.Clamp01(_currentEnergy / _maxEnergy);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE REGEN HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Regenerates mana by <see cref="_manaRegenPerSecond"/> × deltaTime.
        /// Only fires <see cref="OnManaChanged"/> when the value actually changes
        /// (avoids flooding the UI with events every frame when already full).
        /// </summary>
        private void RegenerateMana()
        {
            if (_currentMana >= _maxMana) return;

            float previous = _currentMana;
            _currentMana = Mathf.Clamp(_currentMana + _manaRegenPerSecond * Time.deltaTime,
                                        0f, _maxMana);

            // Only fire the event if the value meaningfully changed.
            if (!Mathf.Approximately(_currentMana, previous))
            {
                OnManaChanged?.Invoke(GetNormalizedMana());
            }
        }

        /// <summary>
        /// Regenerates energy by <see cref="_energyRegenPerSecond"/> × deltaTime.
        /// Only fires <see cref="OnEnergyChanged"/> when the value actually changes.
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
        //  EDITOR GIZMOS / DEBUG
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        // Expose readable values in the Inspector at runtime without breaking
        // encapsulation — the fields are still private; these are read-only labels.
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
