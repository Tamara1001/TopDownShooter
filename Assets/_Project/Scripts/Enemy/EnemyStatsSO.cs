// ==============================================================
// EnemyStatsSO.cs
// --------------------------------------------------------------
// PURPOSE:
//   A ScriptableObject that acts as a pure DATA CONTAINER for a
//   single enemy archetype (e.g., Zombie, Mummy, Boss).
//
//   Following the "Separate Data from Logic" principle, this asset
//   holds nothing but configuration values. Multiple enemy
//   prefabs can share the same SO, or each can have its own
//   tuned copy — without changing a single line of code.
//
// USAGE:
//   1. Right-click in the Project window.
//   2. Create ▶ TopDownShooter ▶ Enemy Stats
//   3. Assign the created asset to an EnemyBrain component.
//
// OOP NOTES:
//   - All fields are [SerializeField] private → full encapsulation.
//   - Public getters expose read-only access; no public setters.
//   - Validation via OnValidate() prevents bad data at edit time.
// ==============================================================

using UnityEngine;

/// <summary>
/// ScriptableObject that stores all tunable stats for one enemy archetype.
/// Assign one of these assets to <see cref="EnemyBrain"/> via the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "TopDownShooter/Enemy Stats")]
public class EnemyStatsSO : ScriptableObject
{
    // ----------------------------------------------------------
    // HEALTH
    // ----------------------------------------------------------

    [Header("Health")]
    [Tooltip("Maximum hit points. The HealthComponent will read this value at initialisation.")]
    [SerializeField] private int maxHealth = 100;

    // ----------------------------------------------------------
    // MOVEMENT
    // ----------------------------------------------------------

    [Header("Movement")]
    [Tooltip("NavMeshAgent movement speed (units per second).")]
    [SerializeField] private float moveSpeed = 3.5f;

    // ----------------------------------------------------------
    // DETECTION & RANGE
    // ----------------------------------------------------------

    [Header("Detection & Attack Range")]
    [Tooltip("Radius (world units) within which this enemy detects the player and transitions from Idle → Chase.")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("Radius (world units) at which this enemy stops chasing and transitions to the Attack state.")]
    [SerializeField] private float attackRange = 2f;

    // ----------------------------------------------------------
    // COMBAT TIMING
    // ----------------------------------------------------------

    [Header("Combat")]
    [Tooltip("Minimum seconds between consecutive attack executions while in Attack state.")]
    [SerializeField] private float attackCooldown = 1.5f;

    // ----------------------------------------------------------
    // PUBLIC READ-ONLY GETTERS
    // External code (e.g., EnemyBrain) reads these but CANNOT
    // write to them, preserving the SO's data integrity.
    // ----------------------------------------------------------

    /// <summary>Maximum hit points for this enemy archetype.</summary>
    public int   MaxHealth      => maxHealth;

    /// <summary>NavMeshAgent movement speed in units per second.</summary>
    public float MoveSpeed      => moveSpeed;

    /// <summary>World-space radius at which the enemy detects the player.</summary>
    public float DetectionRange => detectionRange;

    /// <summary>World-space radius at which the enemy switches to the Attack state.</summary>
    public float AttackRange    => attackRange;

    /// <summary>Seconds between consecutive attack activations.</summary>
    public float AttackCooldown => attackCooldown;

    // ----------------------------------------------------------
    // EDITOR VALIDATION
    // Runs in the Editor whenever a value is changed in the
    // Inspector, catching design mistakes before entering Play Mode.
    // ----------------------------------------------------------

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Clamp health to a sensible minimum.
        if (maxHealth <= 0)
        {
            maxHealth = 1;
            Debug.LogWarning($"[EnemyStatsSO] '{name}': maxHealth must be > 0. Clamped to 1.", this);
        }

        // Speed must be positive.
        if (moveSpeed <= 0f)
        {
            moveSpeed = 0.1f;
            Debug.LogWarning($"[EnemyStatsSO] '{name}': moveSpeed must be > 0. Clamped to 0.1.", this);
        }

        // Attack range must be strictly inside detection range, otherwise
        // the enemy would never Chase before reaching the player.
        if (attackRange >= detectionRange)
        {
            attackRange = detectionRange - 0.5f;
            Debug.LogWarning($"[EnemyStatsSO] '{name}': attackRange must be < detectionRange. Adjusted to {attackRange}.", this);
        }

        // Cooldown must be positive to avoid infinite attack spam.
        if (attackCooldown <= 0f)
        {
            attackCooldown = 0.1f;
            Debug.LogWarning($"[EnemyStatsSO] '{name}': attackCooldown must be > 0. Clamped to 0.1.", this);
        }
    }
#endif
}
