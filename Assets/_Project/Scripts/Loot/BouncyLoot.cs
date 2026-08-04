using UnityEngine;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Aplica una fuerza de impulso explosiva al Rigidbody al iniciar,
    /// haciendo que el botín soltado "salte" de los enemigos o cofres.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BouncyLoot : MonoBehaviour
    {
        private void Start()
        {
            if (TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                // Generar una fuerza aleatoria hacia arriba/hacia afuera
                Vector3 force = Vector3.up * 5f + Random.insideUnitSphere * 2f;
                // Garantizar que la fuerza empuje hacia arriba, no que lo entierre en el suelo
                force.y = Mathf.Abs(force.y);
                
                rb.AddForce(force, ForceMode.Impulse);
            }
        }
    }
}
