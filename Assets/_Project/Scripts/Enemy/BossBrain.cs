// =============================================================================
//  BossBrain.cs
//  Project : TopDownShooter – Enemy AI
//
//  PURPOSE
//  -------
//  Extends EnemyBrain with a multi-phase boss fight architecture.
//  • Supports a full weapon arsenal (multiple IWeapons).
//  • Monitors its own health via HealthComponent.OnHealthChanged.
//  • Forces a forced state transition to BossTransitionState when health
//    drops to or below the Phase 2 threshold.
//  • Exposes ExecuteBossWeapon(int index) for Phase 2 bullet-hell patterns.
//
//  DESIGN NOTES
//  ─────────────
//  • Inherits all FSM machinery from EnemyBrain — no duplication.
//  • Overrides Awake() to call base.Awake() first, then layer boss setup.
//  • Overrides BuildStates() to append boss-specific states to the
//    standard Idle / Chase / Attack set.
// =============================================================================

using UnityEngine;
using TopDownShooter.Combat;

/// <summary>
/// Multi-phase boss controller. Inherits EnemyBrain's FSM and adds
/// a weapon arsenal, a health-threshold phase trigger, and a
/// BossTransitionState / BossPhase2State pair.
/// </summary>
public class BossBrain : EnemyBrain
{
    // ─────────────────────────────────────────────────────────────────────
    //  INSPECTOR FIELDS
    // ─────────────────────────────────────────────────────────────────────

    [Header("Boss Arsenal")]
    [Tooltip("All weapons available to the boss. Must implement IWeapon. " +
             "Index 0 = Phase 1 primary, Index 1 = Phase 2 ranged, etc.")]
    [SerializeField] private MonoBehaviour[] _bossWeapons;

    [Header("Phases")]
    [Tooltip("Normalised health (0–1) at which Phase 2 triggers. " +
             "Default 0.5 = triggers at 50 % HP.")]
    [SerializeField] private float _phase2HealthThreshold = 0.5f;

    [Tooltip("World-space position the boss retreats to at the start of Phase 2. " +
             "Defaults to scene origin if left at zero.")]
    [SerializeField] private Vector3 _phase2AnchorPoint = Vector3.zero;

    // ─────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────

    private IWeapon[] _equippedBossWeapons;
    private HealthComponent _bossHealth;

    // ─────────────────────────────────────────────────────────────────────
    //  PUBLIC STATE
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>True once Phase 2 has been triggered (never resets).</summary>
    public bool IsInPhase2 { get; private set; }

    /// <summary>Anchor position the boss moves to in Phase 2.</summary>
    public Vector3 Phase2AnchorPoint => _phase2AnchorPoint;

    // ─────────────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        // Run all base-class wiring first (agent, health, FSM, etc.)
        base.Awake();

        // ── Parse the inspector array into IWeapon[] ─────────────────────
        _equippedBossWeapons = new IWeapon[_bossWeapons != null ? _bossWeapons.Length : 0];
        for (int i = 0; i < _equippedBossWeapons.Length; i++)
        {
            if (_bossWeapons[i] == null) continue;

            IWeapon weapon = _bossWeapons[i] as IWeapon;
            if (weapon != null)
            {
                _equippedBossWeapons[i] = weapon;
            }
            else
            {
                Debug.LogWarning($"[BossBrain] '{name}': _bossWeapons[{i}] " +
                                 $"('{_bossWeapons[i].GetType().Name}') does not implement IWeapon.", this);
            }
        }

        // ── Subscribe to health for phase-change detection ───────────────
        _bossHealth = GetComponent<HealthComponent>();
        if (_bossHealth != null)
        {
            _bossHealth.OnHealthChanged += HandleBossHealth;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_bossHealth != null)
            _bossHealth.OnHealthChanged -= HandleBossHealth;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  FSM OVERRIDE
    // ─────────────────────────────────────────────────────────────────────

    protected override void BuildStates()
    {
        // Register base states first so GetState<IdleState> etc. still work.
        base.BuildStates();

        // Append boss-exclusive states.
        RegisterState(new BossTransitionState());
        RegisterState(new BossPhase2State());
    }

    // ─────────────────────────────────────────────────────────────────────
    //  PHASE LOGIC
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Receives normalised health from HealthComponent.OnHealthChanged.
    /// Triggers phase 2 exactly once when the threshold is crossed.
    /// </summary>
    private void HandleBossHealth(float normalized)
    {
        if (IsInPhase2) return;
        if (normalized > _phase2HealthThreshold) return;

        IsInPhase2 = true;
        Debug.Log($"[BossBrain] '{name}': Phase 2 triggered at {normalized:P0} HP!");
        ChangeState(GetState<BossTransitionState>());
    }

    // ─────────────────────────────────────────────────────────────────────
    //  WEAPON API — called by Phase 2 state
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires the weapon at the given index in the boss arsenal.
    /// Null-safe: out-of-range or unassigned weapons are silently skipped.
    /// </summary>
    public void ExecuteBossWeapon(int index)
    {
        if (_equippedBossWeapons == null || index < 0 || index >= _equippedBossWeapons.Length)
        {
            Debug.LogWarning($"[BossBrain] '{name}': ExecuteBossWeapon index {index} is out of range.", this);
            return;
        }

        IWeapon weapon = _equippedBossWeapons[index];
        if (weapon == null)
        {
            Debug.LogWarning($"[BossBrain] '{name}': No valid IWeapon at boss arsenal index {index}.", this);
            return;
        }

        weapon.ExecuteAttack();
    }
}
