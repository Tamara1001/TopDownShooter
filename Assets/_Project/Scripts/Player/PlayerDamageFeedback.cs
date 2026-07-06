// =============================================================================
//  PlayerDamageFeedback.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Triggers a Cinemachine Impulse camera shake whenever the owning entity's
//  health decreases (takes damage). Purely reactive — no game logic, no UI.
//
//  ARCHITECTURE
//  ─────────────
//  • Single Responsibility: only fires camera impulse on damage.
//    All health math stays in HealthComponent; all visual effects stay in
//    DamageFlasher / PlayerHUD.
//  • Observer Pattern: subscribes to HealthComponent.OnHealthChanged.
//  • RequireComponent guarantees both dependencies exist on the same
//    GameObject — no manual wiring required beyond attaching the script.
//  • Null-safe sentinel pattern for _previousHealth (-1 = uninitialised).
//
//  HOW TO USE
//  ──────────
//  1. Add a CinemachineImpulseSource component to the Player GameObject.
//  2. Attach PlayerDamageFeedback to the same GameObject.
//  3. Configure the impulse profile (shape, duration, force) on the
//     CinemachineImpulseSource in the Inspector.
//  4. Ensure the Virtual Camera has a CinemachineImpulseListener extension.
// =============================================================================

using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Fires a <see cref="CinemachineImpulseSource.GenerateImpulse()"/> whenever the
/// entity's health decreases. Attach alongside <see cref="HealthComponent"/> and a
/// <see cref="CinemachineImpulseSource"/> on the Player (or any damageable entity).
/// </summary>
[RequireComponent(typeof(HealthComponent))]
[RequireComponent(typeof(CinemachineImpulseSource))]
public sealed class PlayerDamageFeedback : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────────

    // Cached component references — guaranteed present by RequireComponent.
    private HealthComponent          _healthComponent;
    private CinemachineImpulseSource _impulseSource;

    // Sentinel: -1 means "not yet initialised" so the first OnHealthChanged
    // call establishes a baseline without triggering a false impulse.
    private float _previousHealth = -1f;

    // ─────────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _healthComponent = GetComponent<HealthComponent>();
        _impulseSource   = GetComponent<CinemachineImpulseSource>();
    }

    private void OnEnable()
    {
        _healthComponent.OnHealthChanged += HandleHealthChanged;

        // Establish the baseline so the first real change is detected correctly.
        _previousHealth = _healthComponent.GetNormalizedHealth();
    }

    private void OnDisable()
    {
        _healthComponent.OnHealthChanged -= HandleHealthChanged;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  EVENT HANDLER
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="HealthComponent.OnHealthChanged"/>.
    /// Compares against the previous value to detect damage (decrease) and
    /// fires a camera shake impulse only when health goes down.
    /// </summary>
    private void HandleHealthChanged(float normalized)
    {
        if (_previousHealth >= 0f && normalized < _previousHealth)
        {
            _impulseSource.GenerateImpulse();
        }

        _previousHealth = normalized;
    }
}
