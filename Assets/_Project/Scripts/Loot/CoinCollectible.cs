
using UnityEngine;
using TopDownShooter.Player;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Estrategia concreta de <see cref="ICollectible"/> que otorga monedas al
    /// <see cref="PlayerWallet"/> del jugador cuando se recolecta.
    /// Funciona en conjunto con <see cref="AutoPickupTrigger"/>.
    /// </summary>
    public sealed class CoinCollectible : MonoBehaviour, ICollectible
    {
        // ----------------------------------------------------------
        // CAMPOS DEL INSPECTOR
        // ----------------------------------------------------------

        [Header("Coin Settings")]

        [Tooltip("Número de monedas a otorgar cuando se recolecta este objeto. Debe ser positivo.")]
        [SerializeField] private int _coinValue = 1;

        // ----------------------------------------------------------
        // IMPLEMENTACIÓN DE ICollectible
        // ----------------------------------------------------------

        /// <summary>
        /// Otorga <see cref="_coinValue"/> monedas al <see cref="PlayerWallet"/> del jugador.
        /// Se omite silenciosamente si no se encuentra ninguna cartera (wallet),
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
            if (player.TryGetComponent<PlayerWallet>(out PlayerWallet wallet))
            {
                wallet.AddCoins(_coinValue);
            }
            else
            {
                Debug.LogWarning(
                    $"[CoinCollectible] Player '{player.name}' has no PlayerWallet component. " +
                    $"Coins were NOT awarded. The pickup will still be destroyed.",
                    gameObject
                );
            }

            // NOTA: NO llame a Destroy(gameObject) aquí.
            // La destrucción del objeto es responsabilidad estricta de AutoPickupTrigger.
        }
    }
}
