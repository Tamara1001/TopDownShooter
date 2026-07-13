// =============================================================================
//  RoomController.cs
//  Project : TopDownShooter – Procedural Dungeon System
//
//  PURPOSE
//  -------
//  Root MonoBehaviour attached to the root of every room prefab.
//  Acts as the central hub for the room's anatomy: sockets (doors) and
//  entity spawner nodes.  Auto-discovers all child components in Awake()
//  so designers never need to manually drag-and-drop references.
//
//  CURRENT STATE: SHELL
//  ─────────────────────
//  This version only collects child components and exposes them.
//  Future iterations will add:
//    ► Room activation / player-enter detection (trigger colliders)
//    ► Door locking during combat encounters
//    ► Wave spawning delegation (hand spawner nodes to WaveManager)
//    ► Cleared/Locked/Active state machine
//
//  DESIGN NOTES
//  ─────────────
//  • GetComponentsInChildren is called once in Awake(), not per-frame.
//  • Lists are exposed as IReadOnlyList so external systems can iterate
//    but cannot add/remove elements.
//  • Validation logs help designers catch prefab setup errors immediately.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using TopDownShooter.Combat;
using TopDownShooter.Enemy;
using TopDownShooter.Managers.UI;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Central hub for a room instance. Auto-discovers all
    /// <see cref="RoomSocket"/> and <see cref="EntitySpawnerNode"/>
    /// components in children during <c>Awake()</c>.
    /// Attach to the root GameObject of every room prefab.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class RoomController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        private enum RoomState { Waiting, Active, Cleared }

        [Header("Room Identity")]
        [Tooltip("The gameplay role of this room. Must match the RoomDataSO Type used " +
                 "to spawn this prefab so the generator selects the correct door prefab.")]
        [SerializeField] private RoomType _roomType;

        [Header("Spawning")]
        [SerializeField] private GameObject[] _enemyPrefabs;
        [SerializeField] private GameObject[] _environmentPrefabs;
        [SerializeField] private GameObject[] _lootPrefabs;

        // Auto-populated in Awake() via GetComponentsInChildren.
        // Using List<T> internally so we can populate from the array,
        // while exposing IReadOnlyList<T> externally for safety.
        private List<RoomSocket>        _sockets  = new List<RoomSocket>();
        private List<EntitySpawnerNode> _spawners = new List<EntitySpawnerNode>();

        private RoomState _state = RoomState.Waiting;
        private int _activeEnemyCount = 0;

        // ─────────────────────────────────────────────────────────────────────
        //  READ-ONLY PROPERTIES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The gameplay role of this room as set in the prefab Inspector.
        /// Used by <see cref="DungeonGenerator"/> to pick the correct door prefab
        /// when connecting two rooms — this is the single authoritative source.
        /// </summary>
        public RoomType Type => _roomType;

        /// <summary>
        /// All doorway sockets discovered in this room's hierarchy.
        /// Read-only view — external systems can iterate but not mutate.
        /// </summary>
        public IReadOnlyList<RoomSocket> Sockets => _sockets;

        /// <summary>
        /// All entity spawner nodes discovered in this room's hierarchy.
        /// Read-only view — external systems can iterate but not mutate.
        /// </summary>
        public IReadOnlyList<EntitySpawnerNode> Spawners => _spawners;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            DiscoverChildComponents();
            ValidateSetup();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INITIALISATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans the entire child hierarchy once and populates the socket
        /// and spawner lists.  Called in Awake() — zero per-frame cost.
        /// </summary>
        private void DiscoverChildComponents()
        {
            // GetComponentsInChildren includes the root GameObject itself
            // and all descendants, which is exactly what we want.
            _sockets.AddRange(GetComponentsInChildren<RoomSocket>());
            _spawners.AddRange(GetComponentsInChildren<EntitySpawnerNode>());
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Logs actionable warnings if the room prefab is missing expected
        /// child components.  Runs once in Awake() during development to
        /// surface prefab setup errors immediately.
        /// </summary>
        private void ValidateSetup()
        {
            if (_sockets.Count == 0)
            {
                Debug.LogWarning($"[RoomController] '{name}': No RoomSocket components found " +
                                 "in children. This room cannot connect to other rooms. " +
                                 "Add RoomSocket scripts to the doorway GameObjects.", this);
            }

            if (_spawners.Count == 0)
            {
                Debug.LogWarning($"[RoomController] '{name}': No EntitySpawnerNode components " +
                                 "found in children. No entities will spawn in this room. " +
                                 "This may be intentional for Start or Corridor rooms.", this);
            }

#if UNITY_EDITOR
            Debug.Log($"[RoomController] '{name}': Discovered {_sockets.Count} socket(s) " +
                      $"and {_spawners.Count} spawner(s).", this);
#endif
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API  (Shell — to be expanded)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the first unconnected socket that faces the given direction,
        /// or <c>null</c> if none is available.
        /// Used by the dungeon generator to find attachment points.
        /// </summary>
        public RoomSocket GetAvailableSocket(SocketDirection direction)
        {
            for (int i = 0; i < _sockets.Count; i++)
            {
                if (_sockets[i].Direction == direction && !_sockets[i].IsConnected)
                    return _sockets[i];
            }

            return null;
        }

        /// <summary>
        /// Returns all spawner nodes of a specific type.
        /// Used by the WaveManager (Enemy), LootSpawner (Loot), or
        /// PropPlacer (Environment) to find their respective spawn points.
        /// </summary>
        public List<EntitySpawnerNode> GetSpawnersByType(EntitySpawnerNode.SpawnerType type)
        {
            var result = new List<EntitySpawnerNode>();

            for (int i = 0; i < _spawners.Count; i++)
            {
                if (_spawners[i].Type == type)
                    result.Add(_spawners[i]);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GAMEPLAY LOGIC
        // ─────────────────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (_state == RoomState.Waiting && other.CompareTag("Player"))
            {
                _state = RoomState.Active;

                SpawnEntities();

                if (_activeEnemyCount > 0)
                {
                    // Close all doors to lock the player in
                    SetAllDoors(true);
                }
                else
                {
                    ClearRoom();
                }
            }
        }

        private void SpawnEntities()
        {
            for (int i = 0; i < _spawners.Count; i++)
            {
                EntitySpawnerNode node = _spawners[i];

                if (node.Type == EntitySpawnerNode.SpawnerType.Environment)
                {
                    if (_environmentPrefabs != null && _environmentPrefabs.Length > 0)
                    {
                        GameObject prefab = _environmentPrefabs[Random.Range(0, _environmentPrefabs.Length)];
                        if (prefab != null) Instantiate(prefab, node.transform.position, node.transform.rotation, transform);
                    }
                }
                else if (node.Type == EntitySpawnerNode.SpawnerType.Enemy)
                {
                    if (_enemyPrefabs != null && _enemyPrefabs.Length > 0)
                    {
                        GameObject prefab = _enemyPrefabs[Random.Range(0, _enemyPrefabs.Length)];
                        if (prefab != null)
                        {
                            GameObject enemyInstance = Instantiate(prefab, node.transform.position, node.transform.rotation, transform);
                            
                            if (enemyInstance.TryGetComponent<HealthComponent>(out HealthComponent health))
                            {
                                _activeEnemyCount++;
                                health.OnDied += HandleEnemyDeath;
                                
                                if (enemyInstance.TryGetComponent<BossBrain>(out BossBrain boss))
                                {
                                    BossHUD.Instance?.ShowBossUI(boss.BossDisplayName, health);
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"[RoomController] Enemy prefab '{prefab.name}' is missing a HealthComponent.");
                            }
                        }
                    }
                }
            }
        }

        private void HandleEnemyDeath()
        {
            _activeEnemyCount--;

            if (_activeEnemyCount <= 0 && _state == RoomState.Active)
            {
                ClearRoom();
            }
        }

        private void ClearRoom()
        {
            _state = RoomState.Cleared;

            // Open doors
            SetAllDoors(false);

            // Spawn loot at all loot nodes
            for (int i = 0; i < _spawners.Count; i++)
            {
                EntitySpawnerNode node = _spawners[i];
                if (node.Type == EntitySpawnerNode.SpawnerType.Loot)
                {
                    if (_lootPrefabs != null && _lootPrefabs.Length > 0)
                    {
                        GameObject prefab = _lootPrefabs[Random.Range(0, _lootPrefabs.Length)];
                        if (prefab != null) Instantiate(prefab, node.transform.position, node.transform.rotation, transform);
                    }
                }
            }
        }

        private void SetAllDoors(bool close)
        {
            for (int i = 0; i < _sockets.Count; i++)
            {
                if (_sockets[i].AssignedDoor != null)
                {
                    if (close) _sockets[i].AssignedDoor.CloseDoor();
                    else _sockets[i].AssignedDoor.OpenDoor();
                }
            }
        }
    }
}
