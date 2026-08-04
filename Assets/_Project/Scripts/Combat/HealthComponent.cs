
using System;
using UnityEngine;

/// <summary>
/// Un gestor de estado de vida universal que implementa <see cref="IDamageable"/>.
/// Utiliza eventos de C# para notificar a los suscriptores sobre cambios de vida y muerte,
/// siguiendo el Patrón Observer.
/// </summary>
public class HealthComponent : MonoBehaviour, IDamageable
{
    // ----------------------------------------------------------
    // INSPECTOR FIELDS
    // Todo el estado es privado. [SerializeField] los expone al
    // Inspector de Unity sin romper la encapsulación.
    // ----------------------------------------------------------

    [Header("Health Settings")]

    [Tooltip("Los puntos de vida máximos con los que comienza esta entidad.")]
    [SerializeField] private int maxHealth = 100;

    // ----------------------------------------------------------
    // PRIVATE STATE
    // El estado en tiempo de ejecución se mantiene privado para evitar la mutación externa.
    // ----------------------------------------------------------

    /// <summary>La vida actual de la entidad en tiempo de ejecución.</summary>
    private int currentHealth;

    /// <summary>
    /// Bandera de guardia que evita cualquier procesamiento adicional una vez que la entidad ha muerto. Se verifica al principio de TakeDamage().
    /// </summary>
    private bool isDead;

    // ----------------------------------------------------------
    // EVENTOS PÚBLICOS (Patrón Observer)
    // Los sistemas externos se suscriben a estos para reaccionar a los cambios
    // de vida. Este componente nunca sabe quién está escuchando.
    // ----------------------------------------------------------

    /// <summary>
    /// Se dispara cada vez que cambia la vida. Pasa la fracción de vida
    /// normalizada (0.0 = vacía, 1.0 = completa) para uso de las barras de vida de la UI.
    /// </summary>
    public event Action<float> OnHealthChanged;

    /// <summary>
    /// Se dispara una vez, exactamente cuando la vida llega a cero por primera vez.
    /// Los oyentes pueden usar esto para reproducir animaciones de muerte, activar
    /// transiciones FSM, otorgar puntuación, etc.
    /// </summary>
    public event Action OnDied;

    /// <summary>
    /// Cuando es verdadero, todo el daño entrante se ignora silenciosamente.
    /// Establecido por BossTransitionState durante una cinemática de cambio de fase
    /// para que el jefe no pueda morir durante su animación.
    /// </summary>
    public bool IsInvulnerable { get; set; } = false;

    // ----------------------------------------------------------
    // UNITY LIFECYCLE
    // ----------------------------------------------------------

    /// <summary>
    /// Inicializa la vida a su valor máximo y restablece la
    /// bandera de muerte al iniciar el componente.
    /// </summary>
    private void Awake()
    {
        // Preparamos los datos ANTES de que el HUD se suscriba
        currentHealth = maxHealth;
        isDead = false;
    }

    private void Start()
    {
        // Anunciamos el estado inicial una vez que todo está listo
        OnHealthChanged?.Invoke(GetNormalizedHealth());
    }

    // ----------------------------------------------------------
    // IDamageable IMPLEMENTATION
    // ----------------------------------------------------------

    /// <summary>
    /// Aplica daño a esta entidad, limitando la vida a un mínimo
    /// de cero. Dispara <see cref="OnHealthChanged"/> en cada golpe y
    /// <see cref="OnDied"/> exactamente una vez cuando la vida llega a cero.
    /// </summary>
    /// <param name="amount">
    /// Valor de daño entero positivo para restar de la vida actual.
    /// </param>
    public void TakeDamage(int amount)
    {
        // Guardia: ignorar silenciosamente todo el daño mientras sea invulnerable.
        if (IsInvulnerable) return;

        // Guardia: ignorar silenciosamente todo el daño una vez que la entidad esté muerta.
        // Esto evita disparadores de muerte doble (por ejemplo, dos proyectiles
        // impactando en el mismo frame) y mantiene limpia la lógica del evento.
        if (isDead) return;

        // Validar la entrada para evitar exploits accidentales de daño negativo
        // que efectivamente curarían a la entidad a través de este método.
        if (amount <= 0) return;

        // Restar daño y limitar para que la vida nunca baje de cero.
        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Notificar siempre a los oyentes de cambios de vida (por ejemplo, barra de vida de UI).
        OnHealthChanged?.Invoke(GetNormalizedHealth());

        // Verificar la condición de muerte.
        if (currentHealth <= 0)
        {
            isDead = true;

            // Notificar a los oyentes de muerte (FSM, controlador de animación, gestor de juego,
            // etc.). El trabajo de este componente termina aquí — lo que
            // suceda a continuación es responsabilidad del oyente.
            OnDied?.Invoke();
        }
    }

    /// <summary>
    /// Restaura vida a esta entidad, limitándola a <see cref="maxHealth"/>.
    /// Se ignora silenciosamente si la entidad ya está muerta o la cantidad es
    /// no positiva, preservando el mismo contrato defensivo que TakeDamage.
    /// </summary>
    /// <param name="amount">Cantidad entera positiva de vida a restaurar.</param>
    public void Heal(int amount)
    {
        // Guardia: no se puede curar a una entidad muerta.
        if (isDead) return;

        // Validar entrada — una curación negativa efectivamente causaría daño.
        if (amount <= 0) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Notificar a los oyentes de cambios de vida (por ejemplo, barra de vida de UI).
        OnHealthChanged?.Invoke(GetNormalizedHealth());
    }

    /// <summary>
    /// Escala la vida máxima de la entidad por un multiplicador y ajusta la vida 
    /// actual proporcionalmente. Utilizado por el sistema Dungeon Master para
    /// aplicar buff/debuffs temporales a los enemigos de la sala de forma segura.
    /// </summary>
    /// <param name="multiplier">Multiplicador (ej. 0.5 para la mitad, 2.0 para el doble).</param>
    public void ScaleMaxHealth(float multiplier)
    {
        if (isDead || multiplier <= 0f) return;

        // Obtener el porcentaje actual de vida antes del escalado.
        float currentPercentage = GetNormalizedHealth();

        // Aplicar multiplicador al máximo (asegurando un mínimo de 1).
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * multiplier));

        // Ajustar la vida actual para mantener la misma proporción.
        currentHealth = Mathf.RoundToInt(maxHealth * currentPercentage);

        // Notificar a los listeners del nuevo estado (porcentual).
        OnHealthChanged?.Invoke(GetNormalizedHealth());
    }

    // ACCESORES PÚBLICOS DE SÓLO LECTURA
    // Expone el estado de sólo lectura sin romper la encapsulación.
    // No existen setters públicos — sólo TakeDamage() / Heal() mutan el estado.

    /// <summary>Devuelve la vida actual (útil para UI de texto y Debug).</summary>
    public int CurrentHealth => currentHealth;

    /// <summary>Devuelve la vida máxima.</summary>
    public int MaxHealth => maxHealth;

    /// <summary>
    /// Devuelve la vida actual como un float normalizado entre 0 y 1.
    /// Útil para controlar sliders de UI o efectos de shader.
    /// </summary>
    /// <returns>Un float en el rango [0.0, 1.0].</returns>
    public float GetNormalizedHealth()
    {
        // Guard against division by zero if maxHealth is misconfigured.
        if (maxHealth <= 0) return 0f;
        return (float)currentHealth / maxHealth;
    }

    /// <summary>
    /// Devuelve <c>true</c> si la vida de esta entidad ha llegado a cero.
    /// </summary>
    public bool IsDead => isDead;
}
