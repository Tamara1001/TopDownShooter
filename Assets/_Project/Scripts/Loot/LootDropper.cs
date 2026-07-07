using System;
using UnityEngine;
using TopDownShooter.Combat;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// A struct defining a single item that can drop, paired with its drop chance.
    /// </summary>
    [Serializable]
    public struct LootEntry
    {
        public GameObject Prefab;
        [Range(0, 100)] public float DropChance;
    }

    /// <summary>
    /// Listens to a HealthComponent's death event and spawns a random
    /// number of loot prefabs based on a weighted table.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class LootDropper : MonoBehaviour
    {
        [Header("Drop Settings")]
        [Tooltip("Minimum number of items to drop.")]
        [SerializeField] private int _minDrops = 1;
        
        [Tooltip("Maximum number of items to drop.")]
        [SerializeField] private int _maxDrops = 3;
        
        [Tooltip("List of items that can drop. Evaluated in order per drop iteration.")]
        [SerializeField] private LootEntry[] _lootTable;

        private HealthComponent _health;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _health.OnDied += HandleDeath;
        }

        private void HandleDeath()
        {
            // Unsubscribe to prevent multiple executions if OnDied fires again.
            _health.OnDied -= HandleDeath;

            // Determine how many items we are going to attempt to drop
            int dropCount = UnityEngine.Random.Range(_minDrops, _maxDrops + 1);
            
            for (int i = 0; i < dropCount; i++)
            {
                // Iterate through the loot table
                for (int j = 0; j < _lootTable.Length; j++)
                {
                    LootEntry entry = _lootTable[j];
                    float roll = UnityEngine.Random.Range(0f, 100f);
                    
                    if (roll <= entry.DropChance && entry.Prefab != null)
                    {
                        // Spawn the loot slightly above the object to prevent clipping
                        Instantiate(entry.Prefab, transform.position + (Vector3.up * 0.5f), Quaternion.identity);
                        
                        // We found an item for this drop iteration, move on to the next drop
                        break;
                    }
                }
            }
        }
    }
}
