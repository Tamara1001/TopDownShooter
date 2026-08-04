
using UnityEngine;

/// <summary>
/// Se reproduce una vez cuando el jefe cruza su umbral de salud para la Fase 2.
/// Otorga invulnerabilidad temporal durante la animación de transición.
/// </summary>
public class BossTransitionState : EnemyStateBase
{
    // ─────────────────────────────────────────────────────────────────────
    //  CONFIGURATION
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Cuánto tiempo (en segundos) dura la animación de transición.</summary>
    private const float TransitionDuration = 2.5f;

    // ─────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────

    private float _timer;
    private HealthComponent _health;

    // ─────────────────────────────────────────────────────────────────────
    //  FSM LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    public override void Enter()
    {
        Debug.Log($"[BossTransitionState] '{Brain.name}': Phase 2 transition started.");

        // Detener todo el movimiento — el jefe permanece inmóvil para su cinemática.
        Brain.Agent.isStopped = true;
        Brain.Agent.ResetPath();

        // Hacer al jefe inmune al daño durante la animación.
        _health = Brain.GetComponent<HealthComponent>();
        if (_health != null) _health.IsInvulnerable = true;

        // Disparar el trigger de animación de transición (el diseñador configura esto en el Animator).
        if (Brain.Anim != null)
            Brain.Anim.SetTrigger("PhaseTransition");

        _timer = 0f;
    }

    public override void Tick()
    {
        _timer += Time.deltaTime;

        if (_timer >= TransitionDuration)
        {
            Brain.ChangeState(Brain.GetState<BossPhase2State>());
        }
    }

    public override void Exit()
    {
        // Restaurar la vulnerabilidad para que la Fase 2 pueda ser letal.
        if (_health != null) _health.IsInvulnerable = false;

        Debug.Log($"[BossTransitionState] '{Brain.name}': Transition complete — Phase 2 active.");
    }
}
