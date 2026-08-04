

using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Dispara un <see cref="CinemachineImpulseSource.GenerateImpulse()"/> cada vez que la
/// salud de la entidad disminuye. Conéctelo junto a <see cref="HealthComponent"/> y un
/// <see cref="CinemachineImpulseSource"/> en el Jugador (o cualquier entidad dañable).
/// </summary>
[RequireComponent(typeof(HealthComponent))]
[RequireComponent(typeof(CinemachineImpulseSource))]
public sealed class PlayerDamageFeedback : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────────

    // Referencias de componentes almacenadas en caché — presencia garantizada por RequireComponent.
    private HealthComponent          _healthComponent;
    private CinemachineImpulseSource _impulseSource;

    // Centinela: -1 significa "aún no inicializado" para que la primera llamada a OnHealthChanged
    // establezca una línea de base sin activar un impulso falso.
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

        // Establecer la línea de base para que el primer cambio real sea detectado correctamente.
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
    /// Llamado por <see cref="HealthComponent.OnHealthChanged"/>.
    /// Compara contra el valor anterior para detectar daño (disminución) y
    /// dispara un impulso de sacudida de cámara solo cuando la salud disminuye.
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
