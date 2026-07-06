// =============================================================================
//  PlayerStatsComponent.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Aggregates passive stat multipliers from two distinct sources:
//    1. RELIC (permanent while equipped)   — driven by PlayerInventory.OnRelicChanged.
//    2. CONSUMABLE (temporary, timed buff) — driven by ApplyTemporarySpeedBoost().
//  Exposes the combined result as read-only properties consumed by other systems
//  (e.g. PlayerController3D reads MoveSpeedMultiplier every frame).
//
//  ARCHITECTURE
//  ─────────────
//  • Observer Pattern: subscribes to PlayerInventory.OnRelicChanged.
//  • Coroutine Pattern: temporary buffs are tracked via a single Coroutine handle
//    that is stopped and replaced if a new buff arrives before the old one expires,
//    preventing indefinite stacking from rapid consumable use.
//  • PlayerController3D only reads a float — it has zero knowledge of relics or
//    consumables. The separation of concerns is fully contained here.
//
//  OOP RULES ENFORCED
//  ───────────────────
//  • All mutable state is private.
//  • MoveSpeedMultiplier is a computed read-only property (no setter).
//  • OnEnable / OnDisable subscription pattern prevents memory leaks.
//  • RequireComponent enforces the dependency contract at the Unity level.
// =============================================================================

using System.Collections;
using UnityEngine;
using TopDownShooter.Inventory;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Listens to <see cref="PlayerInventory.OnRelicChanged"/> and recalculates
    /// passive stat multipliers from the equipped relic, and applies temporary
    /// speed buffs from consumables via <see cref="ApplyTemporarySpeedBoost"/>.
    /// Consumer systems (e.g. <see cref="PlayerController3D"/>) only read the
    /// combined <see cref="MoveSpeedMultiplier"/> float — they are fully decoupled
    /// from the underlying sources.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerStatsComponent : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC PROPERTIES  (computed; read-only from the outside)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Combined movement-speed multiplier: baseline 1.0 plus the relic bonus
        /// plus any active consumable bonus.<br/>
        /// Examples: 1.0 = no change, 1.2 = +20%, 1.5 = relic +20% and potion +30%.
        /// </summary>
        public float MoveSpeedMultiplier => 1f + _relicSpeedModifier + _consumableSpeedModifier;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // Cached reference to the sibling inventory — required by the attribute.
        private PlayerInventory _inventory;

        // Fractional speed bonus from the currently equipped relic (permanent while equipped).
        // 0f = no bonus. Mutated only by HandleRelicChanged().
        private float _relicSpeedModifier = 0f;

        // Fractional speed bonus from an active consumable buff (temporary, timed).
        // 0f = no active buff. Mutated only by SpeedBuffRoutine().
        private float _consumableSpeedModifier = 0f;

        // Handle to the running speed-buff coroutine, or null if none is active.
        // Stored so a new buff can cancel an in-progress one before starting fresh.
        private Coroutine _activeBuffCoroutine;

        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR-EXPOSED VFX
        // ─────────────────────────────────────────────────────────────────────

        [Header("VFX")]
        [Tooltip("Particle System played while a speed buff is active. " +
                 "Leave unassigned to skip (null-safe).")]
        [SerializeField] private ParticleSystem _speedAuraParticles;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // GetComponent is safe here: RequireComponent guarantees presence.
            _inventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable()
        {
            _inventory.OnRelicChanged += HandleRelicChanged;

            // Synchronise immediately in case a relic was already equipped
            // before this component was enabled (e.g. loaded from a save).
            HandleRelicChanged(_inventory.CurrentRelic);
        }

        private void OnDisable()
        {
            _inventory.OnRelicChanged -= HandleRelicChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EVENT HANDLER — RELIC
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates <see cref="_relicSpeedModifier"/> whenever the relic slot changes.
        /// Called by <see cref="PlayerInventory.OnRelicChanged"/> (<c>null</c> = cleared).
        /// The <see cref="MoveSpeedMultiplier"/> getter picks up the change automatically.
        /// </summary>
        /// <param name="relic">The newly equipped relic, or <c>null</c> if cleared.</param>
        private void HandleRelicChanged(RelicDataSO relic)
        {
            _relicSpeedModifier = relic != null ? relic.MoveSpeedModifier : 0f;

            Debug.Log(relic != null
                ? $"[PlayerStatsComponent] Relic '{relic.DisplayName}' equipped. " +
                  $"RelicSpeedModifier = {_relicSpeedModifier:+0.##;-0.##;0}"
                : "[PlayerStatsComponent] Relic unequipped. RelicSpeedModifier reset to 0.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API — CONSUMABLE BUFFS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies a temporary fractional speed boost that expires after
        /// <paramref name="duration"/> seconds.
        /// <para>
        /// If a buff is already running, it is cancelled and replaced — no stacking.
        /// The boost is a fractional additive bonus: 0.3 = +30% speed.
        /// </para>
        /// </summary>
        /// <param name="boostMultiplier">Fractional speed bonus (e.g. 0.3 for +30%).</param>
        /// <param name="duration">Seconds before the boost expires.</param>
        public void ApplyTemporarySpeedBoost(float boostMultiplier, float duration)
        {
            // Cancel any in-progress buff so the new one takes full effect immediately.
            if (_activeBuffCoroutine != null)
            {
                StopCoroutine(_activeBuffCoroutine);
                _activeBuffCoroutine = null;
            }

            _activeBuffCoroutine = StartCoroutine(SpeedBuffRoutine(boostMultiplier, duration));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  COROUTINES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets <see cref="_consumableSpeedModifier"/>, waits, then clears it.
        /// Managed exclusively through <see cref="ApplyTemporarySpeedBoost"/>.
        /// </summary>
        private IEnumerator SpeedBuffRoutine(float boostMultiplier, float duration)
        {
            _consumableSpeedModifier = boostMultiplier;

            // Start the speed aura VFX (null-safe — no error if not assigned).
            _speedAuraParticles?.Play();

            Debug.Log($"[PlayerStatsComponent] Speed buff active: +{boostMultiplier:P0} for {duration:0.#}s. " +
                      $"MoveSpeedMultiplier = {MoveSpeedMultiplier:0.##}x");

            yield return new WaitForSeconds(duration);

            // Stop the aura before clearing the modifier so the VFX ends cleanly.
            _speedAuraParticles?.Stop();

            _consumableSpeedModifier = 0f;
            _activeBuffCoroutine     = null;
            Debug.Log("[PlayerStatsComponent] Speed buff expired. MoveSpeedMultiplier = " +
                      $"{MoveSpeedMultiplier:0.##}x");
        }
    }
}
