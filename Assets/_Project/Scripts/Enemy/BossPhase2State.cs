// =============================================================================
//  BossPhase2State.cs
//  Project : TopDownShooter – Enemy AI
//
//  PURPOSE
//  -------
//  Distinct Phase 2 combat pattern for the boss fight.
//
//  BEHAVIOUR
//  ──────────
//  Enter  → Resume agent. Move to the room's anchor point (e.g., centre).
//  Tick   → While travelling: keep updating destination.
//           Once arrived:     switch to ranged bullet-hell mode.
//             • Face the player.
//             • Fire weapon index 1 (ranged) at a rapid cooldown.
//  Exit   → Stop the agent.
//
//  DESIGN NOTES
//  ─────────────
//  • Uses BossBrain.Phase2AnchorPoint for its rally position — set in
//    the Inspector on the BossBrain component.
//  • "Arrived" check uses NavMeshAgent.remainingDistance with the
//    stoppingDistance tolerance to avoid floating-point jitter.
//  • Weapon index 1 corresponds to the second element of _bossWeapons.
//    If no second weapon is assigned, calls are silently skipped.
// =============================================================================

using UnityEngine;

/// <summary>
/// Aggressive Phase 2 combat state. The boss repositions to a room anchor
/// point and unleashes a rapid bullet-hell barrage using weapon index 1.
/// </summary>
public class BossPhase2State : EnemyStateBase
{
    // ─────────────────────────────────────────────────────────────────────
    //  CONFIGURATION
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// How close (in world units) the boss must be to its anchor before
    /// it stops repositioning and starts attacking.
    /// </summary>
    private const float ArrivalThreshold = 1.5f;

    // ─────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────

    private BossBrain _bossBrain;
    private float _lastAttackTime = float.NegativeInfinity;
    private bool _hasArrived;

    // ─────────────────────────────────────────────────────────────────────
    //  FSM LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    public override void Enter()
    {
        // Cache the down-cast once — safe because BossPhase2State is only
        // ever registered and called from a BossBrain.
        _bossBrain = Brain as BossBrain;
        if (_bossBrain == null)
        {
            Debug.LogError("[BossPhase2State] Brain is not a BossBrain! This state requires BossBrain.", Brain);
            return;
        }

        _hasArrived = false;
        Brain.Agent.isStopped = false;
        Brain.Agent.SetDestination(_bossBrain.Phase2AnchorPoint);

        if (Brain.Anim != null)
            Brain.Anim.SetBool("IsMoving", true);

        Debug.Log($"[BossPhase2State] '{Brain.name}': Repositioning to anchor {_bossBrain.Phase2AnchorPoint}.");
    }

    public override void Tick()
    {
        if (_bossBrain == null) return;

        if (!_hasArrived)
        {
            TickRepositioning();
        }
        else
        {
            TickBulletHell();
        }
    }

    public override void Exit()
    {
        Brain.Agent.isStopped = true;
        Brain.Agent.ResetPath();

        if (Brain.Anim != null)
            Brain.Anim.SetBool("IsMoving", false);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PHASE PATTERN HELPERS
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves toward the anchor point and flips to attack mode once arrived.
    /// </summary>
    private void TickRepositioning()
    {
        // remainingDistance is only valid once a path has been computed.
        if (!Brain.Agent.pathPending &&
            Brain.Agent.remainingDistance <= ArrivalThreshold)
        {
            _hasArrived = true;
            Brain.Agent.isStopped = true;
            Brain.Agent.ResetPath();

            if (Brain.Anim != null)
                Brain.Anim.SetBool("IsMoving", false);

            Debug.Log($"[BossPhase2State] '{Brain.name}': Anchor reached — starting bullet hell.");
        }
    }

    /// <summary>
    /// Faces the player and fires weapon index 1 on a rapid cooldown.
    /// </summary>
    private void TickBulletHell()
    {
        // Always face the player while attacking.
        FacePlayer();

        if (Time.time >= _lastAttackTime + _bossBrain.GetBossWeaponCooldown(1))
        {
            if (Brain.Anim != null)
                Brain.Anim.SetTrigger("Attack");

            // Index 1 = Phase 2 ranged weapon in the boss arsenal.
            _bossBrain.ExecuteBossWeapon(1);

            _lastAttackTime = Time.time;
        }
    }

    /// <summary>
    /// Instantly snaps the boss to face the player on the Y axis.
    /// </summary>
    private void FacePlayer()
    {
        if (Brain.PlayerTransform == null) return;

        Vector3 direction = Brain.PlayerTransform.position - Brain.transform.position;
        direction.y = 0f;
        if (direction == Vector3.zero) return;

        Brain.transform.rotation = Quaternion.Slerp(
            Brain.transform.rotation,
            Quaternion.LookRotation(direction),
            Time.deltaTime * 12f);
    }
}
