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
        if (Time.time >= _lastAttackTime + Brain.Stats.AttackCooldown)
        {
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
[RequireComponent(typeof(Animator))]
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
    public NavMeshAgent Agent      { get; private set; }

    /// <summary>Read-only access to the stats asset.</summary>
    public EnemyStatsSO Stats      { get; private set; }

    /// <summary>The player's Transform, found via tag at startup.</summary>
    public Transform    PlayerTransform { get; private set; }

    // Private references that only the brain itself needs.
    private HealthComponent _health;
    private Animator        _animator;

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

    private void Awake()
    {
        // ── Resolve required components ──────────────────────
        Agent    = GetComponent<NavMeshAgent>();
        _health  = GetComponent<HealthComponent>();
        _animator = GetComponent<Animator>();

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

        // ── Resolve and validate the IWeapon strategy ────────
        // Cast the MonoBehaviour slot to IWeapon. A warning (not an
        // error) is used so the enemy still moves and chases even if
        // no weapon is configured — useful during iteration.
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
        // Three-tier strategy — order matters:
        //
        //  Tier 1: GameManager.PlayerTransform
        //    The canonical, spawn-order-independent source of truth.
        //    PlayerRegistration.cs calls GameManager.RegisterPlayer()
        //    in Awake/Start, which may run before OR after this Awake.
        //
        //  Tier 2: GameObject.FindGameObjectWithTag (synchronous fallback)
        //    Covers scenes where GameManager is not present (e.g. isolated
        //    test scenes) or where the player was already in the scene
        //    before this enemy spawned.
        //
        //  Tier 3: WaitForPlayer coroutine (async fallback)
        //    Handles the race condition where this enemy spawns BEFORE
        //    the player. The coroutine polls every frame until a valid
        //    reference appears, then finishes initialisation.
        //    The FSM starts in Idle immediately — the enemy won't act
        //    until PlayerTransform is non-null (IsPlayerInRange guards it).
        PlayerTransform = ResolvePlayerTransform();

        // ── Subscribe to the death event ─────────────────────
        // Paired with OnDisable unsubscription to prevent stale
        // delegate leaks when the enemy is pooled or reused.
        _health.OnDied += OnDeath;

        // ── Build & register FSM states ──────────────────────
        BuildStates();

        // ── Start the FSM ────────────────────────────────────
        // The FSM starts immediately. If PlayerTransform is still null
        // (Tier 3 case), all range checks return false and the enemy
        // stays idle safely while the coroutine resolves the reference.
        ChangeState(GetState<IdleState>());

        // ── Tier 3: launch the coroutine if still unresolved ─
        if (PlayerTransform == null)
            StartCoroutine(WaitForPlayer());
    }

    private void OnDisable()
    {
        // Always unsubscribe to avoid null-delegate calls when the
        // enemy is disabled (e.g. returned to an object pool).
        if (_health != null)
            _health.OnDied -= OnDeath;

        // Cancel the WaitForPlayer coroutine if the enemy is disabled
        // before the player has spawned (e.g. returned to pool early).
        StopAllCoroutines();
    }

    private void Update()
    {
        // FSM tick — nothing runs after death.
        if (_isFSMStopped || _currentState == null) return;
        _currentState.Tick();
    }

    // ----------------------------------------------------------
    // FSM MANAGEMENT
    // ----------------------------------------------------------

    /// <summary>
    /// Instantiates and registers all states available to THIS brain.
    /// Override this method in subclasses (e.g., MummyBrain) to
    /// register additional or replacement states without modifying
    /// this base class.
    /// </summary>
    protected virtual void BuildStates()
    {
        RegisterState(new IdleState());
        RegisterState(new ChaseState());
        RegisterState(new AttackState());
        // Part 2: register weapon-aware states here, e.g.:
        //   RegisterState(new RangedAttackState());
    }

    /// <summary>
    /// Adds a state to the FSM state map and injects this brain as
    /// its owner. Called from <see cref="BuildStates"/>.
    /// </summary>
    protected void RegisterState(EnemyStateBase state)
    {
        state.Initialise(this);
        _stateMap[state.GetType()] = state;
    }

    /// <summary>
    /// Retrieves a registered state by its concrete type.
    /// Throws a descriptive exception if the state was never registered,
    /// helping catch misconfigured subclasses early.
    /// </summary>
    public T GetState<T>() where T : EnemyStateBase
    {
        if (_stateMap.TryGetValue(typeof(T), out EnemyStateBase state))
            return (T)state;

        throw new System.InvalidOperationException(
            $"[EnemyBrain] '{name}': State '{typeof(T).Name}' was not registered. " +
            $"Call RegisterState() in BuildStates().");
    }

    /// <summary>
    /// Transitions the FSM from the current state to <paramref name="nextState"/>.
    /// Calls <c>Exit()</c> on the outgoing state and <c>Enter()</c> on the
    /// incoming one. Safely handles a null current state (first transition).
    /// </summary>
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

    /// <summary>
    /// Three-tier synchronous player resolution called from Awake.
    /// Returns null only when the player hasn't spawned yet (Tier 3 case).
    /// </summary>
    private Transform ResolvePlayerTransform()
    {
        // ── Tier 1: GameManager registry ────────────────────
        // Fastest path: O(1) property read, no scene traversal.
        if (GameManager.Instance != null && GameManager.Instance.PlayerTransform != null)
        {
            Debug.Log($"[EnemyBrain] '{name}': Player resolved via GameManager (Tier 1).");
            return GameManager.Instance.PlayerTransform;
        }

        // ── Tier 2: tag search (synchronous fallback) ────────
        // Covers isolated test scenes or scenes without GameManager.
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null)
        {
            Debug.Log($"[EnemyBrain] '{name}': Player resolved via tag search (Tier 2).");
            return found.transform;
        }

        // ── Tier 3 signal: unresolved — coroutine will handle it ─
        Debug.LogWarning($"[EnemyBrain] '{name}': Player not found at Awake time. " +
                         "Starting WaitForPlayer coroutine (Tier 3). " +
                         "Enemy will idle safely until the player spawns.");
        return null;
    }

    /// <summary>
    /// Tier 3 coroutine: polls every frame for a valid player reference.
    /// Resolves via GameManager first (registration may arrive at any frame),
    /// then falls back to tag search.
    ///
    /// Once resolved, the enemy immediately begins reacting to the player
    /// because <see cref="IsPlayerInRange"/> is already guarded against null.
    /// </summary>
    private IEnumerator WaitForPlayer()
    {
        Debug.Log($"[EnemyBrain] '{name}': WaitForPlayer coroutine started.");

        while (PlayerTransform == null)
        {
            // Re-try the two synchronous tiers each frame.
            PlayerTransform = ResolvePlayerTransform();

            // Only yield if still unresolved to avoid an extra frame delay
            // on the frame the player finally spawns.
            if (PlayerTransform == null)
                yield return null;
        }

        Debug.Log($"[EnemyBrain] '{name}': WaitForPlayer resolved. Enemy is now active.");
    }

    // ----------------------------------------------------------
    // HELPERS EXPOSED TO STATES
    // These are the ONLY pieces of EnemyBrain's runtime data that
    // states are allowed to read. Everything else stays private.
    // ----------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> if the player is within <paramref name="range"/> world units.
    /// Performs a flat (XZ) distance check to avoid issues when the
    /// player and enemy are at different heights.
    /// Null-safe: returns false if the player reference is not yet resolved.
    /// </summary>
    public bool IsPlayerInRange(float range)
    {
        if (PlayerTransform == null) return false;

        Vector3 selfFlat   = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerFlat = new Vector3(PlayerTransform.position.x, 0f, PlayerTransform.position.z);

        return Vector3.Distance(selfFlat, playerFlat) <= range;
    }

    // ----------------------------------------------------------
    // DEATH HANDLER
    // ----------------------------------------------------------

    /// <summary>
    /// Subscriber to <see cref="HealthComponent.OnDied"/>.
    /// Halts the FSM, disables the NavMeshAgent so the corpse
    /// stops mid-path, and triggers the Death animation parameter.
    /// Actual destruction / pooling is handled by <see cref="DestroyOnDeath"/>
    /// (which also listens to OnDied), keeping concerns separated.
    /// </summary>
    private void OnDeath()
    {
        // Halt the FSM immediately.
        _isFSMStopped = true;
        _currentState?.Exit();
        _currentState = null;

        // Disable the NavMeshAgent so the corpse does not slide or
        // re-path. We disable rather than destroy so the component
        // can be re-enabled if the enemy is pooled back.
        Agent.isStopped = true;
        Agent.enabled   = false;

        // Fire the Death trigger on the Animator.
        // The Animator parameter MUST be named exactly "Death"
        // (case-sensitive) on every enemy's Animator Controller.
        _animator.SetTrigger("Death");

        Debug.Log($"[EnemyBrain] '{name}' has died.");
    }

    // ----------------------------------------------------------
    // ATTACK  (Part 2 — live implementation)
    // ----------------------------------------------------------

    /// <summary>
    /// Entry point for the attack action. Called by <see cref="AttackState"/>
    /// on every completed cooldown cycle.
    ///
    /// Delegates unconditionally to the <see cref="_equippedWeapon"/> strategy
    /// (Strategy Pattern). <see cref="EnemyBrain"/> has zero knowledge of
    /// whether the weapon is melee, ranged, or anything else — it only
    /// knows the <see cref="IWeapon.ExecuteAttack"/> contract.
    ///
    /// The null-conditional operator (?.) makes a missing weapon assignment
    /// a silent no-op rather than a NullReferenceException, keeping the
    /// FSM running even during incomplete editor setups.
    /// </summary>
    public void PerformAttack()
    {
        _equippedWeapon?.ExecuteAttack();
    }

    // ----------------------------------------------------------
    // EDITOR GIZMOS — visualise ranges in the Scene view
    // Removed from builds automatically by the UNITY_EDITOR guard.
    // ----------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_stats == null) return;

        // Detection range — yellow
        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, _stats.DetectionRange);
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, _stats.DetectionRange);

        // Attack range — red
        UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, _stats.AttackRange);
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, _stats.AttackRange);
    }
#endif
}
