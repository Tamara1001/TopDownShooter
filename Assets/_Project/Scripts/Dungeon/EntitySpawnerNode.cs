// =============================================================================
//  EntitySpawnerNode.cs
//  Project : TopDownShooter – Procedural Dungeon System
//
//  PURPOSE
//  -------
//  Lightweight marker component placed on empty GameObjects inside a room
//  prefab to indicate where entities should spawn at runtime.
//  The RoomController collects all nodes via GetComponentsInChildren and
//  passes them to the WaveManager / LootSpawner when the room activates.
//
//  HOW TO USE
//  ──────────
//  1. Inside a room prefab, create empty child GameObjects at each desired
//     spawn location.
//  2. Attach this script and set the _type (Enemy, Environment, or Loot).
//  3. The RoomController will auto-discover them in Awake().
//
//  DESIGN NOTES
//  ─────────────
//  • SpawnerType is nested inside this class because it is only meaningful
//    in the context of entity spawning — it doesn't belong in DungeonEnums.
//  • The component carries no runtime logic.  It is a pure data node.
//    The spawning system that reads these nodes lives elsewhere (SRP).
// =============================================================================

using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Marker component indicating a spawn point inside a room prefab.
    /// Collected by <see cref="RoomController"/> and consumed by external
    /// spawning systems (WaveManager, LootSpawner, etc.).
    /// </summary>
    public sealed class EntitySpawnerNode : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  NESTED ENUM
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Classifies what kind of entity should appear at this spawn node.
        /// </summary>
        public enum SpawnerType
        {
            /// <summary>Enemy wave spawn point — used by the WaveManager.</summary>
            Enemy,

            /// <summary>Environmental prop spawn (barrels, crates, cover).</summary>
            Environment,

            /// <summary>Loot drop point (chests, pickups, currency).</summary>
            Loot
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Spawner Identity")]
        [Tooltip("What kind of entity this node spawns. " +
                 "Enemy nodes feed into the WaveManager; Loot nodes feed " +
                 "into the LootSpawner; Environment nodes feed into the PropPlacer.")]
        [SerializeField] private SpawnerType _type = SpawnerType.Enemy;

        // ─────────────────────────────────────────────────────────────────────
        //  READ-ONLY PROPERTY
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>The category of entity this node is designated for.</summary>
        public SpawnerType Type => _type;

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR GIZMOS
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Colour-coded sphere so designers can visually distinguish
            // spawn types in the Scene view at a glance.
            Gizmos.color = _type switch
            {
                SpawnerType.Enemy       => new Color(1f, 0.2f, 0.2f, 0.7f),  // Red
                SpawnerType.Environment => new Color(0.3f, 0.8f, 0.3f, 0.7f),  // Green
                SpawnerType.Loot        => new Color(1f, 0.85f, 0.1f, 0.7f),  // Gold
                _                       => Color.white
            };

            Gizmos.DrawSphere(transform.position, 0.25f);

            // Draw an upward line to make nodes visible even when occluded by floor geometry.
            Gizmos.DrawRay(transform.position, Vector3.up * 0.8f);
        }
#endif
    }
}
