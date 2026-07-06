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

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Central hub for a room instance. Auto-discovers all
    /// <see cref="RoomSocket"/> and <see cref="EntitySpawnerNode"/>
    /// components in children during <c>Awake()</c>.
    /// Attach to the root GameObject of every room prefab.
    /// </summary>
    public sealed class RoomController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // Auto-populated in Awake() via GetComponentsInChildren.
        // Using List<T> internally so we can populate from the array,
        // while exposing IReadOnlyList<T> externally for safety.
        private List<RoomSocket>        _sockets  = new List<RoomSocket>();
        private List<EntitySpawnerNode> _spawners = new List<EntitySpawnerNode>();

        // ─────────────────────────────────────────────────────────────────────
        //  READ-ONLY PROPERTIES
        // ─────────────────────────────────────────────────────────────────────

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
    }
}
