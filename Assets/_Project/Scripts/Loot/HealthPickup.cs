using UnityEngine;
using TopDownShooter.Combat; // Required for HealthComponent

namespace TopDownShooter.Loot
{
    /// <summary>
    /// A trigger volume that heals the player by 10 upon collision.
    /// </summary>
    public sealed class HealthPickup : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (other.TryGetComponent<HealthComponent>(out HealthComponent health))
                {
                    health.Heal(10);
                    Destroy(gameObject);
                }
            }
        }
    }
}
