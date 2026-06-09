using System.Collections;
using UnityEngine;

/// <summary>
/// Maneja la muerte visual de un enemigo. 
/// Desactiva colisiones instantáneamente y espera un tiempo antes de destruir el objeto
/// para permitir que la animación de muerte se reproduzca completa.
/// </summary>
[RequireComponent(typeof(HealthComponent))]
public class EnemyDeathHandler : MonoBehaviour
{
    [Tooltip("Segundos que dura la animación de muerte antes de borrar el cadáver.")]
    [SerializeField] private float _destroyDelay = 2f;

    [Tooltip("Efecto de partículas opcional que se reproduce al morir (ej. sangre o humo).")]
    [SerializeField] private GameObject _deathVFXPrefab;

    private void Awake()
    {
        // Nos suscribimos al evento de muerte
        GetComponent<HealthComponent>().OnDied += HandleDeath;
    }

    private void HandleDeath()
    {
        // 1. Apagamos el Collider principal para que Lunaria y los proyectiles lo atraviesen
        if (TryGetComponent<Collider>(out Collider col))
        {
            col.enabled = false;
        }

        // 2. (Opcional) Instanciamos partículas de muerte en el lugar
        if (_deathVFXPrefab != null)
        {
            Instantiate(_deathVFXPrefab, transform.position, Quaternion.identity);
        }

        // 3. Iniciamos la cuenta regresiva para limpiar la memoria
        StartCoroutine(DestroyAfterDelayRoutine());
    }

    private IEnumerator DestroyAfterDelayRoutine()
    {
        yield return new WaitForSeconds(_destroyDelay);

        // Acá a futuro podemos agregar lógica de Object Pooling
        Destroy(gameObject);
    }
}