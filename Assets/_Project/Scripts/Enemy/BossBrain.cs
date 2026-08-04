
using UnityEngine;
using TopDownShooter.Combat;

/// <summary>
/// Controlador de jefe multifase. Hereda la FSM de EnemyBrain y añade
/// un arsenal de armas, un activador de fase por umbral de salud, y el
/// par BossTransitionState / BossPhase2State.
/// </summary>
public class BossBrain : EnemyBrain
{
    // ─────────────────────────────────────────────────────────────────────
    //  CAMPOS DEL INSPECTOR
    // ─────────────────────────────────────────────────────────────────────

    [Header("Boss Arsenal")]
    [Tooltip("Todas las armas disponibles para el jefe. Deben implementar IWeapon. Índice 0 = primaria de Fase 1, Índice 1 = a distancia de Fase 2, etc.")]
    [SerializeField] private MonoBehaviour[] _bossWeapons;

    [Header("Identity")]
    [Tooltip("Nombre en pantalla que se muestra en el HUD del Jefe. Cámbielo por prefab para que la cadena nunca esté hardcodeada en RoomController.")]
    [SerializeField] private string _bossDisplayName = "Crypt King";

    [Header("Phases")]
    [Tooltip("Salud normalizada (0–1) en la cual se activa la Fase 2. Por defecto 0.5 = se activa al 50 % de HP.")]
    [SerializeField] private float _phase2HealthThreshold = 0.5f;

    [Tooltip("Posición en espacio de mundo a la que se retira el jefe al inicio de la Fase 2. Por defecto es el origen de la escena si se deja en cero.")]
    [SerializeField] private Vector3 _phase2AnchorPoint = Vector3.zero;

    // ─────────────────────────────────────────────────────────────────────
    //  ESTADO PRIVADO
    // ─────────────────────────────────────────────────────────────────────

    private IWeapon[] _equippedBossWeapons;
    private HealthComponent _bossHealth;

    // ─────────────────────────────────────────────────────────────────────
    //  ESTADO PÚBLICO
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Nombre de visualización utilizado por el HUD del Jefe. Configurado en el Inspector.</summary>
    public string BossDisplayName => _bossDisplayName;

    /// <summary>Verdadero una vez que se ha activado la Fase 2 (nunca se restablece).</summary>
    public bool IsInPhase2 { get; private set; }

    /// <summary>Posición de anclaje a la que se mueve el jefe en la Fase 2.</summary>
    public Vector3 Phase2AnchorPoint => _phase2AnchorPoint;

    // ─────────────────────────────────────────────────────────────────────
    //  CICLO DE VIDA
    // ─────────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        // Primero realizar todo el cableado de la clase base (agente, salud, FSM, etc.)
        base.Awake();

        // Solución para mazmorra procedimental: si el anclaje del inspector es cero, asumir que el centro de la sala es el punto de aparición exacto del Jefe.
        if (_phase2AnchorPoint == Vector3.zero) 
            _phase2AnchorPoint = transform.position;

        // ── Analizar el arreglo del inspector en IWeapon[] ─────────────────
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

        // ── Suscribirse a la salud para la detección de cambios de fase ───
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
    //  SOBREESCRITURA DE FSM
    // ─────────────────────────────────────────────────────────────────────

    protected override void BuildStates()
    {
        // Registrar primero los estados base para que GetState<IdleState> etc. sigan funcionando.
        base.BuildStates();

        // Añadir estados exclusivos del jefe.
        RegisterState(new BossTransitionState());
        RegisterState(new BossPhase2State());
    }

    // ─────────────────────────────────────────────────────────────────────
    //  LÓGICA DE FASES
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recibe la salud normalizada de HealthComponent.OnHealthChanged.
    /// Activa la fase 2 exactamente una vez cuando se cruza el umbral.
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
    //  API DE ARMAS Y SOBREESCRITURA DE ATAQUE
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Conecta el AttackState estándar de la Fase 1 directamente en el arsenal del jefe
    /// en lugar de la base _equippedWeapon.
    /// </summary>
    public override void PerformAttack()
    {
        ExecuteBossWeapon(0);
    }

    /// <summary>
    /// Dispara el arma en el índice dado en el arsenal del jefe.
    /// Seguro contra nulos: las armas fuera de rango o no asignadas se omiten silenciosamente.
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

    /// <summary>
    /// Obtiene el enfriamiento (cooldown) del arma en el índice dado.
    /// Devuelve 1f como respaldo seguro si falta el arma.
    /// </summary>
    public float GetBossWeaponCooldown(int index)
    {
        return (_equippedBossWeapons != null && index >= 0 && index < _equippedBossWeapons.Length && _equippedBossWeapons[index] != null) 
            ? _equippedBossWeapons[index].Cooldown : 1f;
    }
}
