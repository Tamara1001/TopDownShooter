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
        
        [Tooltip("List of items that can drop. Selection uses cumulative weighted random — every entry's chance is proportional to its DropChance weight.")]
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

            // --- Cumulative Weighted Random Setup ---
            // Sum the DropChance of every valid entry to form the total weight pool.
            float totalWeight = 0f;
            foreach (LootEntry entry in _lootTable)
            {
                if (entry.Prefab != null)
                    totalWeight += entry.DropChance;
            }

            // Nothing to drop if the table is empty or all weights are zero.
            if (totalWeight <= 0f) return;

            // Determine how many items we are going to drop.
            int dropCount = UnityEngine.Random.Range(_minDrops, _maxDrops + 1);

            for (int i = 0; i < dropCount; i++)
            {
                // Roll once against the full weight pool so every item's probability
                // is proportional to its DropChance, regardless of array position.
                float roll = UnityEngine.Random.Range(0f, totalWeight);

                for (int j = 0; j < _lootTable.Length; j++)
                {
                    LootEntry entry = _lootTable[j];

                    if (entry.Prefab == null) continue;

                    // Consume this entry's weight from the roll.
                    roll -= entry.DropChance;

                    // When roll is exhausted, this entry wins the selection.
                    if (roll <= 0f)
                    {
                        // Spawn with a small random horizontal jitter so simultaneous
                        // drops don't perfectly overlap each other.
                        Vector3 jitter = new Vector3(
                            UnityEngine.Random.Range(-0.3f, 0.3f),
                            0.5f,
                            UnityEngine.Random.Range(-0.3f, 0.3f));

                        Instantiate(entry.Prefab, transform.position + jitter, Quaternion.identity);

                        // Item selected for this iteration — move to the next drop.
                        break;
                    }
                }
            }
        }
    }
}
