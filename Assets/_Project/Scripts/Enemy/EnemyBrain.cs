// ==============================================================
// EnemyBrain.cs
// --------------------------------------------------------------
// PURPOSE:
//   The central "brain" MonoBehaviour for every enemy in the game.
//   It orchestrates a modular, class-based Finite State Machine
//   (FSM) and wires together all sub-systems:
//     • NavMeshAgent  — pathfinding / movement
//     • HealthComponent — damage & death events
//     • Animator        — animation triggers
//     • EnemyStatsSO    — data-driven configuration
//
// ARCHITECTURE (Composition over Inheritance):
//   Instead of one giant switch/case block, each FSM state is its
//   own self-contained C# class derived from a common abstract base
//   (EnemyStateBase). This means:
//     • Adding a NEW state (e.g., DashState) = adding ONE new class.
//     • EnemyBrain only stores a reference to the CURRENT state and
//       calls Enter/Tick/Exit on it — it never hard-codes behaviour.
//
// EXTENSIBILITY:
//   To add a custom state for a new enemy type (e.g., MummyBrain):
//     1. Create MummyDashState : EnemyStateBase (or : ChaseState).
//     2. In MummyBrain (which derives from EnemyBrain), override
//        BuildStates() and insert MummyDashState into the table.
//     3. Nothing in this file needs to change.
//
// OOP RULES ENFORCED:
//   • No public mutable state; all fields are private/protected.
//   • EnemyBrain does NOT deal damage or know about colliders.
//     Attack logic lives entirely inside PerformAttack(), expanded
//     in Part 2 via an IWeapon strategy (dependency injection).
//   • Player is resolved via GameManager first; tag and coroutine
//     are fallback tiers so spawn order never causes breakage.
//
// FIX LOG (Part 3):
//   [FIX-1] AttackState cooldown exploit closed — timestamp replaces countdown.
//   [FIX-4] Player resolution decoupled from scene load order via GameManager.
// ==============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TopDownShooter.Combat; // IWeapon — Strategy Pattern contract

// ==============================================================
// FSM INFRASTRUCTURE
// The abstract base and concrete state classes are defined here
// in the same file for Part 1 clarity. In Part 2 they can be
// split into separate files under an Enemy/States/ folder.
// ==============================================================

#region ── FSM Base ────────────────────────────────────────────

/// <summary>
/// Abstract base for every enemy FSM state.
/// Each concrete state receives a reference to its owner
/// (<see cref="EnemyBrain"/>) so it can read stats and steer
/// the NavMeshAgent without being tightly coupled to any specific
/// enemy subclass.
/// </summary>
public abstract class EnemyStateBase
{
    // ---- protected reference to the owning brain ----
    // States read stats and call brain helpers but NEVER
    // access private fields directly — only through protected
    // or public API exposed by EnemyBrain.
    protected EnemyBrain Brain { get; private set; }

    /// <summary>
    /// Injects the owning <see cref="EnemyBrain"/> reference.
    /// Called once by EnemyBrain when the state is constructed.
    /// </summary>
    public void Initialise(EnemyBrain brain) => Brain = brain;

    // ---- Lifecycle hooks called by the FSM driver ----

    /// <summary>Called once when this state becomes active.</summary>
    public abstract void Enter();

    /// <summary>Called every frame while this state is active (from Update).</summary>
    public abstract void Tick();

    /// <summary>Called once just before transitioning to another state.</summary>
    public abstract void Exit();
}

#endregion

// ==============================================================
#region ── Concrete States ─────────────────────────────────────

// ──────────────────────────────────────────────────────────────
/// <summary>
/// IDLE STATE — the enemy stands still and scans for the player.
/// <para>
/// Transition OUT: Player enters <see cref="EnemyStatsSO.DetectionRange"/>
///                 → transitions to <see cref="ChaseState"/>.
/// </para>
/// </summary>
public class IdleState : EnemyStateBase
{
    public override void Enter()
    {
        // Stop the agent completely when entering Idle.
        Brain.Agent.isStopped = true;
        Brain.Agent.ResetPath();
        Debug.Log($"[{Brain.name}] → Idle");
    }

    public override void Tick()
    {
        // Check whether the player has walked into detection range.
        if (Brain.IsPlayerInRange(Brain.Stats.DetectionRange))
        {
            Brain.ChangeState(Brain.GetState<ChaseState>());
        }
    }

    public override void Exit()
    {
        // Resume agent movement when leaving Idle.
        Brain.Agent.isStopped = false;
    }
}

// ──────────────────────────────────────────────────────────────
/// <summary>
/// CHASE STATE — the NavMeshAgent actively pursues the player
/// each frame by setting its destination to the player's position.
/// <para>
/// Transition OUT (attack): Player enters <see cref="EnemyStatsSO.AttackRange"/>
///                          → transitions to <see cref="AttackState"/>.
/// </para>
/// <para>
/// Transition OUT (lost):   Player leaves <see cref="EnemyStatsSO.DetectionRange"/>
///                          → returns to <see cref="IdleState"/>.
/// </para>
/// </para>
/// </summary>
public class ChaseState : EnemyStateBase
{
    public override void Enter()
    {
        Brain.Agent.isStopped = false;

        if (Brain.Anim != null)
            Brain.Anim.SetBool("IsMoving", true);

        Debug.Log($"[{Brain.name}] → Chase");
    }

    public override void Tick()
    {
        // Always steer toward the player's current position.
        Brain.Agent.SetDestination(Brain.PlayerTransform.position);

        // ── Attack transition ────────────────────────────────
        if (Brain.IsPlayerInRange(Brain.Stats.AttackRange))
        {
            Brain.ChangeState(Brain.GetState<AttackState>());
            return;
        }

        // ── Lost-player transition ───────────────────────────
        if (!Brain.IsPlayerInRange(Brain.Stats.DetectionRange))
        {
            Brain.ChangeState(Brain.GetState<IdleState>());
        }
    }

    public override void Exit()
    {
        // Halt the agent before transferring control to the next state.
        Brain.Agent.isStopped = true;
        Brain.Agent.ResetPath();

        if (Brain.Anim != null)
            Brain.Anim.SetBool("IsMoving", false);
    }
}

// ──────────────────────────────────────────────────────────────
/// <summary>
/// ATTACK STATE — the enemy stops moving, rotates to face the
/// player, and repeatedly calls <see cref="EnemyBrain.PerformAttack"/>
/// on a wall-clock timestamp cooldown.
/// <para>
/// Transition OUT: Player moves outside <see cref="EnemyStatsSO.AttackRange"/>
///                 → returns to <see cref="ChaseState"/>.
/// </para>
///
/// FIX-1 — Cooldown exploit closed:
///   The old implementation stored a countdown timer and reset it to 0
///   in Enter(). If the player stepped in and out of attack range rapidly,
///   each re-entry would restart the countdown at 0, letting the enemy
///   attack at will regardless of AttackCooldown.
///
///   The fix uses a wall-clock timestamp (<c>_lastAttackTime</c>) that is
///   set to <c>float.NegativeInfinity</c> at field initialisation and is
///   NEVER written in Enter(). Cooldown enforcement is:
///     <c>Time.time >= _lastAttackTime + AttackCooldown</c>
///   Because <c>Time.time</c> is monotonically increasing and
///   <c>_lastAttackTime</c> is only overwritten when an attack fires,
///   no amount of state re-entries can cheat the cooldown window.
/// </summary>
public class AttackState : EnemyStateBase
{
    // ── FIX-1: timestamp replaces countdown ──────────────────
    // Initialised to negative infinity so the very first attack fires
    // immediately on Enter() without any artificial delay.
    // Written ONLY when Brain.PerformAttack() succeeds; never in Enter().
    private float _lastAttackTime = float.NegativeInfinity;

    public override void Enter()
    {
        // Stop the agent — the enemy stands still while attacking.
        // NOTE: _lastAttackTime is intentionally NOT reset here.
        //       Resetting it would allow rapid in/out cycling to
        //       bypass the cooldown (the exploit this fix closes).
        Brain.Agent.isStopped = true;
        Brain.Agent.ResetPath();
        Debug.Log($"[{Brain.name}] → Attack");
    }

    public override void Tick()
    {
        // ── Chase transition (player stepped back) ───────────
        if (!Brain.IsPlayerInRange(Brain.Stats.AttackRange))
        {
            Brain.ChangeState(Brain.GetState<ChaseState>());
            return;
        }

        // ── Face the player ──────────────────────────────────
        FacePlayer();

        // ── Timestamp cooldown check (FIX-1) ─────────────────
        // Compare the monotonic clock against the last recorded
        // attack time. This check is immune to state re-entries
        // because _lastAttackTime is never touched in Enter().
        if (Time.time >= _lastAttackTime + Brain.GetCurrentWeaponCooldown())
        {
            if (Brain.Anim != null)
                Brain.Anim.SetTrigger("Attack");

            Brain.PerformAttack();

            // Record the wall-clock time of THIS attack.
            // The next attack cannot fire until at least
            // AttackCooldown seconds have elapsed.
            _lastAttackTime = Time.time;
        }
    }

    public override void Exit()
    {
        Brain.Agent.isStopped = false;
    }

    // ──────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Smoothly rotates the enemy on the Y axis to face the player,
    /// keeping the model upright regardless of height differences.
    /// </summary>
    private void FacePlayer()
    {
        Vector3 direction = Brain.PlayerTransform.position - Brain.transform.position;
        direction.y = 0f; // Ignore vertical offset to prevent tilting.

        if (direction == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        Brain.transform.rotation = Quaternion.Slerp(
            Brain.transform.rotation,
            targetRotation,
            Time.deltaTime * 10f);
    }
}

#endregion

// ==============================================================
// MAIN BRAIN
// ==============================================================

/// <summary>
/// Central controller MonoBehaviour for every enemy entity.
/// It owns and drives the FSM, reads configuration from an
/// <see cref="EnemyStatsSO"/>, and reacts to the
/// <see cref="HealthComponent.OnDied"/> event.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HealthComponent))]
public class EnemyBrain : MonoBehaviour
{
    // ----------------------------------------------------------
    // INSPECTOR — configuration
    // ----------------------------------------------------------

    [Header("Configuration")]
    [Tooltip("ScriptableObject asset that defines all stats for this enemy archetype.")]
    [SerializeField] private EnemyStatsSO _stats;

    // ----------------------------------------------------------
    // WEAPON STRATEGY — Part 2
    // ----------------------------------------------------------
    // We accept a MonoBehaviour in the Inspector (Unity cannot
    // serialize interface types directly), then cast to IWeapon
    // once in Awake. The FSM calls PerformAttack() → ExecuteAttack()
    // and never knows whether the equipped weapon is melee or ranged.
    // ----------------------------------------------------------

    [Header("Weapon (Part 2)")]
    [Tooltip("Assign a MeleeWeapon or RangedWeapon component (on this GameObject " +
             "or a child). Must implement IWeapon.")]
    [SerializeField] private MonoBehaviour _weaponComponent;

    /// <summary>
    /// The active weapon strategy resolved at Awake.
    /// Null-safe: if no weapon is assigned the enemy fights without dealing damage.
    /// </summary>
    private IWeapon _equippedWeapon;

    // ----------------------------------------------------------
    // COMPONENT REFERENCES (resolved in Awake)
    // Exposed as read-only properties so States can access them
    // without reflection or FindComponent calls every frame.
    // ----------------------------------------------------------

    /// <summary>This enemy's NavMeshAgent component.</summary>
    public NavMeshAgent Agent { get; private set; }

    /// <summary>Read-only access to the stats asset.</summary>
    public EnemyStatsSO Stats { get; private set; }

    /// <summary>The player's Transform, found via tag at startup.</summary>
    public Transform PlayerTransform { get; private set; }

    /// <summary>Exposes safe read-only access to the Animator for state controllers.</summary>
    public Animator Anim => _animator;

    // Private references that only the brain itself needs.
    private HealthComponent _health;
    private Animator _animator;

    // ----------------------------------------------------------
    // FSM STORAGE
    // A dictionary maps each State TYPE to its singleton instance
    // so states can cross-reference each other by type without
    // string comparisons or enum casts.
    // ----------------------------------------------------------

    /// <summary>
    /// All available states, keyed by their concrete type.
    /// Populated in <see cref="BuildStates"/>; override in subclasses
    /// to inject custom states.
    /// </summary>
    private readonly Dictionary<System.Type, EnemyStateBase> _stateMap =
        new Dictionary<System.Type, EnemyStateBase>();

    /// <summary>The currently executing FSM state. Null when the FSM is halted.</summary>
    private EnemyStateBase _currentState;

    /// <summary>
    /// Guard flag set by <see cref="OnDeath"/>. When true, Update()
    /// will not tick the FSM and no further state transitions occur.
    /// </summary>
    private bool _isFSMStopped;

    // ----------------------------------------------------------
    // UNITY LIFECYCLE
    // ----------------------------------------------------------

    protected virtual void Awake()
    {
        // ── Resolve required components ──────────────────────
        Agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<HealthComponent>();
        _animator = GetComponentInChildren<Animator>();

        if (_animator == null)
        {
            Debug.LogWarning($"[EnemyBrain] '{name}': No Animator component found in child meshes.", this);
        }

        // ── Validate the SO reference ────────────────────────
        if (_stats == null)
        {
            Debug.LogError($"[EnemyBrain] '{name}': No EnemyStatsSO assigned! Disabling.", this);
            enabled = false;
            return;
        }

        Stats = _stats; // expose via public getter

        // ── Apply SO data to the agent ───────────────────────
        Agent.speed = Stats.MoveSpeed;
        Agent.stoppingDistance = Mathf.Max(0f, Stats.AttackRange - 0.5f);

        // ── Resolve and validate the IWeapon strategy ────────
        if (_weaponComponent != null)
        {
            _equippedWeapon = _weaponComponent as IWeapon;

            if (_equippedWeapon == null)
            {
                Debug.LogWarning($"[EnemyBrain] '{name}': The assigned _weaponComponent " +
                                 $"('{_weaponComponent.GetType().Name}') does not implement " +
                                 $"IWeapon. No damage will be dealt.", this);
            }
        }
        else
        {
            Debug.LogWarning($"[EnemyBrain] '{name}': No weapon component assigned. " +
                             "The enemy will enter Attack state but deal no damage.", this);
        }

        // ── Resolve the Player reference (FIX-4) ────────────
        PlayerTransform = ResolvePlayerTransform();

        // ── Subscribe to the death event ─────────────────────
        _health.OnDied += OnDeath;

        // ── Build & register FSM states ──────────────────────
        BuildStates();

        // ── Start the FSM ────────────────────────────────────
        ChangeState(GetState<IdleState>());

        // ── Tier 3: launch the coroutine if still unresolved ─
        if (PlayerTransform == null)
            StartCoroutine(WaitForPlayer());
    }

    /// <summary>
    /// Inyecta multiplicadores de combate directamente al arma equipada
    /// (invocado por modificadores del Dungeon Master).
    /// </summary>
    public void SetWeaponDungeonMultipliers(float damageMultiplier, float cooldownMultiplier)
    {
        if (_equippedWeapon != null)
        {
            _equippedWeapon.SetDungeonMultipliers(damageMultiplier, cooldownMultiplier);
        }
    }

    protected virtual void OnDisable()
    {
        if (_health != null)
            _health.OnDied -= OnDeath;

        StopAllCoroutines();
    }

    private void Update()
    {
        if (_isFSMStopped || _currentState == null) return;
        _currentState.Tick();
    }

    // ----------------------------------------------------------
    // FSM MANAGEMENT
    // ----------------------------------------------------------

    protected virtual void BuildStates()
    {
        RegisterState(new IdleState());
        RegisterState(new ChaseState());
        RegisterState(new AttackState());
    }

    protected void RegisterState(EnemyStateBase state)
    {
        state.Initialise(this);
        _stateMap[state.GetType()] = state;
    }

    public T GetState<T>() where T : EnemyStateBase
    {
        if (_stateMap.TryGetValue(typeof(T), out EnemyStateBase state))
            return (T)state;

        throw new System.InvalidOperationException(
            $"[EnemyBrain] '{name}': State '{typeof(T).Name}' was not registered. " +
            $"Call RegisterState() in BuildStates().");
    }

    public void ChangeState(EnemyStateBase nextState)
    {
        if (nextState == null)
        {
            Debug.LogError($"[EnemyBrain] '{name}': Attempted to transition to a null state.");
            return;
        }

        _currentState?.Exit();
        _currentState = nextState;
        _currentState.Enter();
    }

    // ----------------------------------------------------------
    // PLAYER RESOLUTION HELPERS (FIX-4)
    // ----------------------------------------------------------

    private Transform ResolvePlayerTransform()
    {
        if (GameManager.Instance != null && GameManager.Instance.PlayerTransform != null)
        {
            Debug.Log($"[EnemyBrain] '{name}': Player resolved via GameManager (Tier 1).");
            return GameManager.Instance.PlayerTransform;
        }

        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
        {
            Debug.Log($"[EnemyBrain] '{name}': Player resolved via tag search (Tier 2).");
            return found.transform;
        }

        Debug.LogWarning($"[EnemyBrain] '{name}': Player not found at Awake time. " +
                         "Starting WaitForPlayer coroutine (Tier 3). " +
                         "Enemy will idle safely until the player spawns.");
        return null;
    }

    private IEnumerator WaitForPlayer()
    {
        Debug.Log($"[EnemyBrain] '{name}': WaitForPlayer coroutine started.");

        while (PlayerTransform == null)
        {
            PlayerTransform = ResolvePlayerTransform();

            if (PlayerTransform == null)
                yield return null;
        }

        Debug.Log($"[EnemyBrain] '{name}': WaitForPlayer resolved. Enemy is now active.");
    }

    // ----------------------------------------------------------
    // HELPERS EXPOSED TO STATES
    // ----------------------------------------------------------

    public bool IsPlayerInRange(float range)
    {
        if (PlayerTransform == null) return false;

        Vector3 selfFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerFlat = new Vector3(PlayerTransform.position.x, 0f, PlayerTransform.position.z);

        return Vector3.Distance(selfFlat, playerFlat) <= range;
    }

    // ----------------------------------------------------------
    // DEATH HANDLER
    // ----------------------------------------------------------

    private void OnDeath()
    {
        _isFSMStopped = true;
        _currentState?.Exit();
        _currentState = null;

        Agent.isStopped = true;
        Agent.enabled = false;

        if (_animator != null)
            _animator.SetTrigger("Death");

        Debug.Log($"[EnemyBrain] '{name}' has died.");
    }

    // ----------------------------------------------------------
    // ATTACK
    // ----------------------------------------------------------

    public virtual float GetCurrentWeaponCooldown()
    {
        return _equippedWeapon != null ? _equippedWeapon.Cooldown : Stats.AttackCooldown;
    }

    public virtual void PerformAttack()
    {
        _equippedWeapon?.ExecuteAttack();
    }

    // ----------------------------------------------------------
    // EDITOR GIZMOS
    // ----------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_stats == null) return;

        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, _stats.DetectionRange);
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, _stats.DetectionRange);

        UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, _stats.AttackRange);
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, _stats.AttackRange);
    }
#endif
}