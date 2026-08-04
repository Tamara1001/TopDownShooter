

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
