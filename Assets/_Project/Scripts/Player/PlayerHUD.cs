using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta la vida del jugador a la barra visual usando eventos (Observer).
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Tooltip("La barra de vida (Image con tipo Fill)")]
    [SerializeField] private Image healthBarFill;

    // Referencia al jugador en escena (arrastrala en el Inspector)
    [SerializeField] private HealthComponent playerHealth;

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            // Suscribimos la barra a los cambios de vida
            playerHealth.OnHealthChanged += UpdateHealthBar;
            // Actualizamos inmediatamente por si ya tenía daño
            UpdateHealthBar(playerHealth.GetNormalizedHealth());
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHealthBar;
        }
    }

    private void UpdateHealthBar(float normalizedHealth)
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = normalizedHealth;
        }
    }
}