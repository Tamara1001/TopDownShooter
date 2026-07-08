// =============================================================================
//  BossTransitionState.cs
//  Project : TopDownShooter – Enemy AI
//
//  PURPOSE
//  -------
//  A brief cinematic "breather" state the boss enters when its health
//  crosses the Phase 2 threshold.
//
//  BEHAVIOUR
//  ──────────
//  Enter  → Stop agent. Make boss invulnerable. Fire "PhaseTransition" anim.
//  Tick   → Count down a timer. On expiry → transition to BossPhase2State.
//  Exit   → Remove invulnerability so the boss can take damage again.
// =============================================================================

using UnityEngine;

/// <summary>
/// Played once when the boss crosses its Phase 2 health threshold.
/// Grants temporary invulnerability during the transition animation.
/// </summary>
public class BossTransitionState : EnemyStateBase
{
    // ─────────────────────────────────────────────────────────────────────
    //  CONFIGURATION
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>How long (seconds) the transition animation lasts.</summary>
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

        // Stop all movement — the boss stands still for its cinematic.
        Brain.Agent.isStopped = true;
        Brain.Agent.ResetPath();

        // Make the boss immune to damage during the animation.
        _health = Brain.GetComponent<HealthComponent>();
        if (_health != null) _health.IsInvulnerable = true;

        // Fire the transition animation trigger (designer sets this up in Animator).
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
        // Restore vulnerability so Phase 2 can be lethal.
        if (_health != null) _health.IsInvulnerable = false;

        Debug.Log($"[BossTransitionState] '{Brain.name}': Transition complete — Phase 2 active.");
    }
}
