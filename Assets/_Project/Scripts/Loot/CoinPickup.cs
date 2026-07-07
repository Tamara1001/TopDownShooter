using UnityEngine;
using TopDownShooter.Player;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// A trigger volume that adds 1 coin to the player's wallet upon collision.
    /// </summary>
    public sealed class CoinPickup : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (other.TryGetComponent<PlayerWallet>(out PlayerWallet wallet))
                {
                    wallet.AddCoins(1);
                    Destroy(gameObject);
                }
            }
        }
    }
}
