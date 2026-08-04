

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TopDownShooter.Combat; // IWeapon — Strategy Pattern contract



#region ── Base FSM ────────────────────────────────────────────

/// <summary>
/// Base abstracta para cada estado FSM del enemigo.
/// Cada estado concreto recibe una referencia a su propietario
/// (<see cref="EnemyBrain"/>) para que pueda leer las estadísticas y dirigir
/// el NavMeshAgent sin estar estrechamente acoplado a ninguna subclase específica
/// de enemigo.
/// </summary>
public abstract class EnemyStateBase
{
    // ---- referencia protegida al cerebro propietario ----
    // Los estados leen estadísticas y llaman a ayudantes del cerebro, pero NUNCA
    // acceden a campos privados directamente — solo a través de la API protegida
    // o pública expuesta por EnemyBrain.
    protected EnemyBrain Brain { get; private set; }

    /// <summary>
    /// Inyecta la referencia al <see cref="EnemyBrain"/> propietario.
    /// Llamado una vez por EnemyBrain cuando se construye el estado.
    /// </summary>
    public void Initialise(EnemyBrain brain) => Brain = brain;

    // ---- Ganchos de ciclo de vida llamados por el controlador FSM ----

    /// <summary>Llamado una vez cuando este estado se activa.</summary>
    public abstract void Enter();

    /// <summary>Llamado en cada frame mientras este estado está activo (desde Update).</summary>
    public abstract void Tick();

    /// <summary>Llamado una vez justo antes de la transición a otro estado.</summary>
    public abstract void Exit();
}

#endregion

// ==============================================================
#region ── Estados Concretos ─────────────────────────────────────

// ──────────────────────────────────────────────────────────────
/// <summary>
/// ESTADO IDLE — el enemigo permanece inmóvil y busca al jugador.
/// <para>
/// Transición de SALIDA: El jugador entra en el <see cref="EnemyStatsSO.DetectionRange"/>
///                      → realiza la transición a <see cref="ChaseState"/>.
/// </para>
/// </summary>
public class IdleState : EnemyStateBase
{
    public override void Enter()
    {
        // Detener completamente al agente al entrar en Idle.
        Brain.Agent.isStopped = true;
        Brain.Agent.ResetPath();
        Debug.Log($"[{Brain.name}] → Idle");
    }

    public override void Tick()
    {
        // Comprobar si el jugador ha entrado en el rango de detección.
        if (Brain.IsPlayerInRange(Brain.Stats.DetectionRange))
        {
            Brain.ChangeState(Brain.GetState<ChaseState>());
        }
    }

    public override void Exit()
    {
        // Reanudar el movimiento del agente al salir de Idle.
        Brain.Agent.isStopped = false;
    }
}

// ──────────────────────────────────────────────────────────────
/// <summary>
/// ESTADO CHASE — el NavMeshAgent persigue activamente al jugador
/// en cada frame estableciendo su destino en la posición del jugador.
/// <para>
/// Transición de SALIDA (ataque): El jugador entra en el <see cref="EnemyStatsSO.AttackRange"/>
///                                → realiza la transición a <see cref="AttackState"/>.
/// </para>
/// <para>
/// Transición de SALIDA (perdido): El jugador sale del <see cref="EnemyStatsSO.DetectionRange"/>
///                                → regresa al <see cref="IdleState"/>.
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
        // Dirigir siempre hacia la posición actual del jugador.
        Brain.Agent.SetDestination(Brain.PlayerTransform.position);

        // ── Transición de ataque ─────────────────────────────
        if (Brain.IsPlayerInRange(Brain.Stats.AttackRange))
        {
            Brain.ChangeState(Brain.GetState<AttackState>());
            return;
        }

        // ── Transición de jugador perdido ────────────────────
        if (!Brain.IsPlayerInRange(Brain.Stats.DetectionRange))
        {
            Brain.ChangeState(Brain.GetState<IdleState>());
        }
    }

    public override void Exit()
    {
        // Detener al agente antes de transferir el control al siguiente estado.
        Brain.Agent.isStopped = true;
        Brain.Agent.ResetPath();

        if (Brain.Anim != null)
            Brain.Anim.SetBool("IsMoving", false);
    }
}

// ──────────────────────────────────────────────────────────────
/// <summary>
/// ESTADO ATTACK — el enemigo deja de moverse, gira para mirar al
/// jugador y llama repetidamente a <see cref="EnemyBrain.PerformAttack"/>
/// en un enfriamiento basado en la hora del reloj del sistema (timestamp).
/// <para>
/// Transición de SALIDA: El jugador se mueve fuera del <see cref="EnemyStatsSO.AttackRange"/>
///                      → regresa a <see cref="ChaseState"/>.
/// </para>
///
/// SOLUCIÓN-1 — Explotación del enfriamiento cerrada:
///   La implementación anterior almacenaba un temporizador de cuenta regresiva y lo restablecía a 0
///   en Enter(). Si el jugador entraba y salía del rango de ataque rápidamente,
///   cada reentrada reiniciaba la cuenta regresiva en 0, lo que permitía al enemigo
///   atacar a voluntad independientemente de AttackCooldown.
///
///   La solución utiliza una marca de tiempo de reloj del sistema (<c>_lastAttackTime</c>) que se
///   establece en <c>float.NegativeInfinity</c> en la inicialización del campo y
///   NUNCA se escribe en Enter(). La aplicación del enfriamiento es:
///     <c>Time.time >= _lastAttackTime + AttackCooldown</c>
///   Debido a que <c>Time.time</c> aumenta monótonamente y
///   <c>_lastAttackTime</c> solo se sobrescribe cuando se dispara un ataque,
///   ninguna cantidad de reentradas de estado puede burlar la ventana de enfriamiento.
/// </summary>
public class AttackState : EnemyStateBase
{
    // ── SOLUCIÓN-1: marca de tiempo reemplaza cuenta regresiva ──
    // Inicializado en infinito negativo para que el primer ataque se dispare
    // inmediatamente en Enter() sin ningún retraso artificial.
    // Escrito SOLO cuando Brain.PerformAttack() tiene éxito; nunca en Enter().
    private float _lastAttackTime = float.NegativeInfinity;

    public override void Enter()
    {
        // Detener al agente — el enemigo permanece inmóvil mientras ataca.
        // NOTA: _lastAttackTime NO se restablece aquí intencionalmente.
        //       Restablecerlo permitiría ciclos rápidos de entrada/salida para
        //       omitir el enfriamiento (la explotación que esta solución cierra).
        Brain.Agent.isStopped = true;
        Brain.Agent.ResetPath();
        Debug.Log($"[{Brain.name}] → Attack");
    }

    public override void Tick()
    {
        // ── Transición a Chase (el jugador retrocedió) ───────
        if (!Brain.IsPlayerInRange(Brain.Stats.AttackRange))
        {
            Brain.ChangeState(Brain.GetState<ChaseState>());
            return;
        }

        // ── Mirar al jugador ─────────────────────────────────
        FacePlayer();

        // ── Comprobación de enfriamiento de marca de tiempo (SOLUCIÓN-1) ──
        // Compara el reloj monótono con el último tiempo de ataque registrado.
        // Esta comprobación es inmune a las reentradas de estado
        // porque _lastAttackTime nunca se toca en Enter().
        if (Time.time >= _lastAttackTime + Brain.GetCurrentWeaponCooldown())
        {
            if (Brain.Anim != null)
                Brain.Anim.SetTrigger("Attack");

            Brain.PerformAttack();

            // Registrar la hora del reloj del sistema de ESTE ataque.
            // El próximo ataque no puede dispararse hasta que hayan transcurrido
            // al menos los segundos de AttackCooldown.
            _lastAttackTime = Time.time;
        }
    }

    public override void Exit()
    {
        Brain.Agent.isStopped = false;
    }

    // ──────────────────────────────────────────────────────────
    // AYUDANTES PRIVADOS
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Gira suavemente al enemigo en el eje Y para mirar al jugador,
    /// manteniendo el modelo erguido independientemente de las diferencias de altura.
    /// </summary>
    private void FacePlayer()
    {
        Vector3 direction = Brain.PlayerTransform.position - Brain.transform.position;
        direction.y = 0f; // Ignorar el desplazamiento vertical para evitar inclinaciones.

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
// CEREBRO PRINCIPAL
// ==============================================================

/// <summary>
/// MonoBehaviour controlador central para cada entidad enemiga.
/// Posee y conduce la FSM, lee la configuración de un
/// <see cref="EnemyStatsSO"/> y reacciona al evento
/// <see cref="HealthComponent.OnDied"/>.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HealthComponent))]
public class EnemyBrain : MonoBehaviour
{
    // ----------------------------------------------------------
    // INSPECTOR — configuración
    // ----------------------------------------------------------

    [Header("Configuration")]
    [Tooltip("Asset ScriptableObject que define todas las estadísticas para este arquetipo de enemigo.")]
    [SerializeField] private EnemyStatsSO _stats;

    // ----------------------------------------------------------
    // ESTRATEGIA DE ARMAS — Parte 2
    // ----------------------------------------------------------
    // Aceptamos un MonoBehaviour en el Inspector (Unity no puede
    // serializar tipos de interfaz directamente), luego hacemos el cast a IWeapon
    // una vez en Awake. La FSM llama a PerformAttack() → ExecuteAttack()
    // y nunca sabe si el arma equipada es cuerpo a cuerpo o a distancia.
    // ----------------------------------------------------------

    [Header("Weapon (Part 2)")]
    [Tooltip("Asigne un componente MeleeWeapon o RangedWeapon (en este GameObject o en un hijo). Debe implementar IWeapon.")]
    [SerializeField] private MonoBehaviour _weaponComponent;

    /// <summary>
    /// La estrategia de arma activa resuelta en Awake.
    /// Seguro contra nulos: si no hay un arma asignada, el enemigo lucha sin infligir daño.
    /// </summary>
    private IWeapon _equippedWeapon;

    // ----------------------------------------------------------
    // REFERENCIAS DE COMPONENTES (resueltas en Awake)
    // Expuestas como propiedades de solo lectura para que los Estados puedan acceder a ellas
    // sin llamadas por reflexión o FindComponent en cada frame.
    // ----------------------------------------------------------

    /// <summary>Componente NavMeshAgent de este enemigo.</summary>
    public NavMeshAgent Agent { get; private set; }

    /// <summary>Acceso de solo lectura al asset de estadísticas.</summary>
    public EnemyStatsSO Stats { get; private set; }

    /// <summary>El Transform del jugador, encontrado mediante etiqueta al inicio.</summary>
    public Transform PlayerTransform { get; private set; }

    /// <summary>Expone un acceso seguro de solo lectura al Animator para los controladores de estado.</summary>
    public Animator Anim => _animator;

    // Referencias privadas que solo el cerebro necesita.
    private HealthComponent _health;
    private Animator _animator;

    // ----------------------------------------------------------
    // ALMACENAMIENTO DE FSM
    // Un diccionario mapea cada TIPO de Estado a su instancia singleton
    // para que los estados puedan hacer referencias cruzadas entre sí por tipo sin
    // comparaciones de cadenas o conversiones de enums.
    // ----------------------------------------------------------

    /// <summary>
    /// Todos los estados disponibles, indexados por su tipo concreto.
    /// Rellenado en <see cref="BuildStates"/>; sobreescribir en subclases
    /// para inyectar estados personalizados.
    /// </summary>
    private readonly Dictionary<System.Type, EnemyStateBase> _stateMap =
        new Dictionary<System.Type, EnemyStateBase>();

    /// <summary>El estado FSM en ejecución actualmente. Nulo cuando la FSM está detenida.</summary>
    private EnemyStateBase _currentState;

    /// <summary>
    /// Bandera de guardia establecida por <see cref="OnDeath"/>. Cuando es verdadera, Update()
    /// no actualizará la FSM y no ocurrirán más transiciones de estado.
    /// </summary>
    private bool _isFSMStopped;

    // ----------------------------------------------------------
    // CICLO DE VIDA DE UNITY
    // ----------------------------------------------------------

    protected virtual void Awake()
    {
        // ── Resolver componentes requeridos ──────────────────
        Agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<HealthComponent>();
        _animator = GetComponentInChildren<Animator>();

        if (_animator == null)
        {
            Debug.LogWarning($"[EnemyBrain] '{name}': No Animator component found in child meshes.", this);
        }

        // ── Validar la referencia del SO ─────────────────────
        if (_stats == null)
        {
            Debug.LogError($"[EnemyBrain] '{name}': No EnemyStatsSO assigned! Disabling.", this);
            enabled = false;
            return;
        }

        Stats = _stats; // exponer a través del getter público

        // ── Aplicar datos del SO al agente ───────────────────
        Agent.speed = Stats.MoveSpeed;
        Agent.stoppingDistance = Mathf.Max(0f, Stats.AttackRange - 0.5f);

        // ── Resolver y validar la estrategia de IWeapon ──────
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

        // ── Resolver la referencia del Jugador (SOLUCIÓN-4) ───
        PlayerTransform = ResolvePlayerTransform();

        // ── Suscribirse al evento de muerte ───────────────────
        _health.OnDied += OnDeath;

        // ── Construir y registrar los estados de la FSM ───────
        BuildStates();

        // ── Iniciar la FSM ───────────────────────────────────
        ChangeState(GetState<IdleState>());

        // ── Nivel 3: iniciar la corrutina si aún no está resuelto ─
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
    // GESTIÓN DE LA FSM
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
    // AYUDANTES DE RESOLUCIÓN DEL JUGADOR (SOLUCIÓN-4)
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
    // AYUDANTES EXPUESTOS A LOS ESTADOS
    // ----------------------------------------------------------

    public bool IsPlayerInRange(float range)
    {
        if (PlayerTransform == null) return false;

        Vector3 selfFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerFlat = new Vector3(PlayerTransform.position.x, 0f, PlayerTransform.position.z);

        return Vector3.Distance(selfFlat, playerFlat) <= range;
    }

    // ----------------------------------------------------------
    // MANEJADOR DE MUERTE
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
    // ATAQUE
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
    // GIZMOS DE EDITOR
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