
using UnityEngine;

/// <summary>
/// ScriptableObject que almacena todas las estadísticas ajustables para un arquetipo de enemigo.
/// Asigne uno de estos assets a <see cref="EnemyBrain"/> a través del Inspector.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "TopDownShooter/Enemy Stats")]
public class EnemyStatsSO : ScriptableObject
{
    // ----------------------------------------------------------
    // SALUD
    // ----------------------------------------------------------

    [Header("Health")]
    [Tooltip("Puntos de vida máximos. El HealthComponent leerá este valor al inicializarse.")]
    [SerializeField] private int maxHealth = 100;

    // ----------------------------------------------------------
    // MOVIMIENTO
    // ----------------------------------------------------------

    [Header("Movement")]
    [Tooltip("Velocidad de movimiento del NavMeshAgent (unidades por segundo).")]
    [SerializeField] private float moveSpeed = 3.5f;

    // ----------------------------------------------------------
    // DETECCIÓN Y RANGO
    // ----------------------------------------------------------

    [Header("Detection & Attack Range")]
    [Tooltip("Radio (unidades del mundo) dentro del cual este enemigo detecta al jugador y realiza la transición de Idle → Chase.")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("Radio (unidades del mundo) en el cual este enemigo deja de perseguir y realiza la transición al estado Attack.")]
    [SerializeField] private float attackRange = 2f;

    // ----------------------------------------------------------
    // TIEMPOS DE COMBATE
    // ----------------------------------------------------------

    [Header("Combat")]
    [Tooltip("Segundos mínimos entre ejecuciones de ataque consecutivas mientras está en el estado Attack.")]
    [SerializeField] private float attackCooldown = 1.5f;

    // ----------------------------------------------------------
    // GETTERS PÚBLICOS DE SOLO LECTURA
    // El código externo (por ejemplo, EnemyBrain) lee estos pero NO puede
    // escribir en ellos, preservando la integridad de los datos del SO.
    // ----------------------------------------------------------

    /// <summary>Puntos de vida máximos para este arquetipo de enemigo.</summary>
    public int   MaxHealth      => maxHealth;

    /// <summary>Velocidad de movimiento del NavMeshAgent en unidades por segundo.</summary>
    public float MoveSpeed      => moveSpeed;

    /// <summary>Radio en espacio de mundo en el cual el enemigo detecta al jugador.</summary>
    public float DetectionRange => detectionRange;

    /// <summary>Radio en espacio de mundo en el cual el enemigo cambia al estado Attack.</summary>
    public float AttackRange    => attackRange;

    /// <summary>Segundos entre activaciones de ataque consecutivas.</summary>
    public float AttackCooldown => attackCooldown;

    // ----------------------------------------------------------
    // VALIDACIÓN EN EDITOR
    // Se ejecuta en el Editor cada vez que se cambia un valor en el
    // Inspector, detectando errores de diseño antes de entrar en Play Mode.
    // ----------------------------------------------------------

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Limitar la salud a un mínimo sensato.
        if (maxHealth <= 0)
        {
            maxHealth = 1;
            Debug.LogWarning($"[EnemyStatsSO] '{name}': maxHealth must be > 0. Clamped to 1.", this);
        }

        // La velocidad debe ser positiva.
        if (moveSpeed <= 0f)
        {
            moveSpeed = 0.1f;
            Debug.LogWarning($"[EnemyStatsSO] '{name}': moveSpeed must be > 0. Clamped to 0.1.", this);
        }

        // El rango de ataque debe estar estrictamente dentro del rango de detección, de lo contrario
        // el enemigo nunca realizaría la persecución (Chase) antes de alcanzar al jugador.
        if (attackRange >= detectionRange)
        {
            attackRange = detectionRange - 0.5f;
            Debug.LogWarning($"[EnemyStatsSO] '{name}': attackRange must be < detectionRange. Adjusted to {attackRange}.", this);
        }

        // El enfriamiento (cooldown) debe ser positivo para evitar el spam infinito de ataques.
        if (attackCooldown <= 0f)
        {
            attackCooldown = 0.1f;
            Debug.LogWarning($"[EnemyStatsSO] '{name}': attackCooldown must be > 0. Clamped to 0.1.", this);
        }
    }
#endif
}
