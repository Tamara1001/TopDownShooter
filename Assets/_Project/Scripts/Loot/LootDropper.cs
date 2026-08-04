using System;
using UnityEngine;
using TopDownShooter.Combat;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Estructura que define un único objeto que puede soltarse, emparejado con su probabilidad de caída.
    /// </summary>
    [Serializable]
    public struct LootEntry
    {
        public GameObject Prefab;
        [Range(0, 100)] public float DropChance;
    }

    /// <summary>
    /// Escucha el evento de muerte de un HealthComponent y genera un número aleatorio
    /// de prefabs de botín basándose en una tabla ponderada.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class LootDropper : MonoBehaviour
    {
        [Header("Drop Settings")]
        [Tooltip("Número mínimo de objetos a soltar.")]
        [SerializeField] private int _minDrops = 1;
        
        [Tooltip("Número máximo de objetos a soltar.")]
        [SerializeField] private int _maxDrops = 3;
        
        [Tooltip("Lista de objetos que pueden soltarse. La selección utiliza un valor aleatorio ponderado acumulativo — la probabilidad de cada entrada es proporcional al peso de su DropChance.")]
        [SerializeField] private LootEntry[] _lootTable;

        private HealthComponent _health;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _health.OnDied += HandleDeath;
        }

        private void HandleDeath()
        {
            // Desuscribirse para evitar múltiples ejecuciones si OnDied se dispara nuevamente.
            _health.OnDied -= HandleDeath;

            // --- Configuración Aleatoria Ponderada Acumulativa ---
            // Sumar la DropChance de cada entrada válida para formar el conjunto de peso total.
            float totalWeight = 0f;
            foreach (LootEntry entry in _lootTable)
            {
                if (entry.Prefab != null)
                    totalWeight += entry.DropChance;
            }

            // Nada que soltar si la tabla está vacía o todos los pesos son cero.
            if (totalWeight <= 0f) return;

            // Determinar cuántos objetos vamos a soltar.
            int dropCount = UnityEngine.Random.Range(_minDrops, _maxDrops + 1);

            for (int i = 0; i < dropCount; i++)
            {
                // Tirar una vez contra el conjunto de peso total para que la probabilidad de cada objeto
                // sea proporcional a su DropChance, independientemente de la posición en el arreglo.
                float roll = UnityEngine.Random.Range(0f, totalWeight);

                for (int j = 0; j < _lootTable.Length; j++)
                {
                    LootEntry entry = _lootTable[j];

                    if (entry.Prefab == null) continue;

                    // Consumir el peso de esta entrada a partir de la tirada.
                    roll -= entry.DropChance;

                    // Cuando la tirada se agota, esta entrada gana la selección.
                    if (roll <= 0f)
                    {
                        // Generar con una pequeña fluctuación (jitter) horizontal aleatoria para que las
                        // caídas simultáneas no se superpongan perfectamente entre sí.
                        Vector3 jitter = new Vector3(
                            UnityEngine.Random.Range(-0.3f, 0.3f),
                            0.5f,
                            UnityEngine.Random.Range(-0.3f, 0.3f));

                        Instantiate(entry.Prefab, transform.position + jitter, Quaternion.identity);

                        // Objeto seleccionado para esta iteración — pasar a la siguiente caída.
                        break;
                    }
                }
            }
        }
    }
}
