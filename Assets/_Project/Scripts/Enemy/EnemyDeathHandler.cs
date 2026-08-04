using System.Collections;
using UnityEngine;

/// <summary>
/// Maneja la muerte visual de un enemigo: desactiva colisiones instantaneamente
/// y destruye el objeto tras un retardo para que la animacion de muerte se complete.
/// Patron: Observer — se suscribe a HealthComponent.OnDied en Awake.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class EnemyDeathHandler : MonoBehaviour
{
    [Tooltip("Segundos que dura la animación de muerte antes de que se destruya el cadáver.")]
    [SerializeField] private float _destroyDelay = 2f;

    [Tooltip("Prefab de partículas opcional instanciado en la posición de muerte (sangre, humo, etc.).")]
    [SerializeField] private GameObject _deathVFXPrefab;

    private void Awake()
    {
        GetComponent<HealthComponent>().OnDied += HandleDeath;
    }

    private void HandleDeath()
    {
        if (TryGetComponent<Collider>(out Collider col))
            col.enabled = false;

        if (_deathVFXPrefab != null)
            Instantiate(_deathVFXPrefab, transform.position, Quaternion.identity);

        StartCoroutine(DestroyAfterDelayRoutine());
    }

    private IEnumerator DestroyAfterDelayRoutine()
    {
        yield return new WaitForSeconds(_destroyDelay);
        // Punto de extension para Object Pooling si se requiere en el futuro.
        Destroy(gameObject);
    }
}
