
using UnityEngine;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Estrategia concreta de <see cref="ICollectible"/> que restaura salud al
    /// <see cref="HealthComponent"/> del jugador cuando se recolecta.
    /// Funciona en conjunto con <see cref="AutoPickupTrigger"/>.
    /// </summary>
    public sealed class HealthCollectible : MonoBehaviour, ICollectible
    {
        // ----------------------------------------------------------
        // CAMPOS DEL INSPECTOR
        // ----------------------------------------------------------

        [Header("Health Settings")]

        [Tooltip("Cantidad de puntos de salud (HP) a restaurar cuando se recolecta este objeto. Debe ser positivo.")]
        [SerializeField] private int _healAmount = 10;

        // ----------------------------------------------------------
        // IMPLEMENTACIÓN DE ICollectible
        // ----------------------------------------------------------

        /// <summary>
        /// Restaura <see cref="_healAmount"/> HP al <see cref="HealthComponent"/> del jugador.
        /// Se omite silenciosamente si no se encuentra ningún HealthComponent,
        /// evitando un error crítico que también suprimiría la llamada a Destroy
        /// en <see cref="AutoPickupTrigger"/>. NO llame a Destroy aquí.
        /// </summary>
        /// <param name="player">
        /// El <see cref="GameObject"/> raíz del jugador pasado por
        /// <see cref="AutoPickupTrigger.OnTriggerEnter"/>.
        /// </param>
        public void Collect(GameObject player)
        {
            // TryGetComponent evita lanzar una excepción al fallar y no genera asignaciones
            // de memoria (allocation-free) — preferible sobre GetComponent en rutas críticas de ejecución.
            if (player.TryGetComponent<HealthComponent>(out HealthComponent health))
            {
                health.Heal(_healAmount);
            }
            else
            {
                Debug.LogWarning(
                    $"[HealthCollectible] Player '{player.name}' has no HealthComponent. " +
                    $"No healing was applied. The pickup will still be destroyed.",
                    gameObject
                );
            }

            // NOTA: NO llame a Destroy(gameObject) aquí.
            // La destrucción del objeto es responsabilidad estricta de AutoPickupTrigger.
        }
    }
}
