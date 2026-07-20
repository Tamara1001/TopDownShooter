using UnityEngine;

/// <summary>
/// Script de utilidad que escucha la muerte de un HealthComponent
/// y destruye el GameObject. Ideal para cajas, jarrones, etc.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class DestroyOnDeath : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<HealthComponent>().OnDied += HandleDeath;
    }

    private void HandleDeath()
    {
        Destroy(gameObject);
    }
}