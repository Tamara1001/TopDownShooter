using UnityEngine;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Applies an explosive impulse force to the Rigidbody on Start,
    /// making the dropped loot "pop" out of enemies or chests.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BouncyLoot : MonoBehaviour
    {
        private void Start()
        {
            if (TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                // Generate a random upward/outward force
                Vector3 force = Vector3.up * 5f + Random.insideUnitSphere * 2f;
                // Guarantee the force is pushing up, not burying it into the floor
                force.y = Mathf.Abs(force.y);
                
                rb.AddForce(force, ForceMode.Impulse);
            }
        }
    }
}
