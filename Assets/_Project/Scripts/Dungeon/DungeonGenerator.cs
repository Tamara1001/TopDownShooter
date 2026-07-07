// =============================================================================
//  DungeonGenerator.cs
//  Project : TopDownShooter – Procedural Dungeon System
//
//  PURPOSE
//  -------
//  Core generation algorithm that consumes DungeonConfigSO + Room Prefabs
//  to procedurally build a connected dungeon at runtime.
//
//  ALGORITHM OVERVIEW
//  ──────────────────
//  1. Place the Start room at the grid origin (0,0).
//  2. For each step along the main path:
//     a. Pick a random open socket from the frontier.
//     b. Check whether the target cell is already occupied.
//        – If occupied, discard that socket and try another.
//        – If free, spawn a room there.
//     c. Connect both sockets (origin ↔ new room).
//     d. Register the new room's unconnected sockets into the frontier.
//  3. Repeat until MainPathLength rooms are placed (or the frontier is
//     exhausted — a graceful early exit).
//
//  GRID MODEL
//  ──────────
//  • Every room occupies exactly one 20×20 unit cell.
//  • Cell (x,y) → World (x * _cellSize, 0, y * _cellSize).
//  • A HashSet<Vector2Int> tracks occupied cells — O(1) overlap check,
//    zero reliance on Physics (which is unreliable on frame 0).
//
//  DESIGN NOTES
//  ─────────────
//  • SocketData is a lightweight wrapper binding a live RoomSocket component
//    to the grid coordinate of the adjacent cell it connects to.
//  • Weighted random selection respects RoomDataSO.Weight so designers can
//    control room frequency without duplicating assets.
//  • The Boss room is explicitly placed as the final room on the main path.
//  • The generator is stateless between runs — calling Generate() clears
//    all previous data first, making it safe to call repeatedly.
//
//  FUTURE HOOKS
//  ─────────────
//  ► Branching   : After the main path, iterate remaining _availableSockets
//                   and spawn branch rooms up to _config.MaxBranches.
//  ► Seeded RNG  : Replace Random.Range with a seeded System.Random for
//                   reproducible dungeon layouts.
//  ► Room Variety: Expand the RoomType filter per step (Treasure rooms on
//                   branches only, etc.).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Procedural dungeon generator. Consumes a <see cref="DungeonConfigSO"/>
    /// and builds a connected grid of room instances at runtime.
    /// Attach to an empty "DungeonGenerator" GameObject in the scene.
    /// </summary>
    public sealed class DungeonGenerator : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  NESTED TYPES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Lightweight binding between a live <see cref="RoomSocket"/> on an
        /// already-placed room and the grid coordinate of the cell it connects to.
        /// Used as the frontier data structure during generation.
        /// </summary>
        private sealed class SocketData
        {
            /// <summary>The socket component on the already-placed room.</summary>
            public RoomSocket Socket;

            /// <summary>
            /// Grid cell that a new room would occupy if it connected to this socket.
            /// Computed as: roomGridPos + GetDirectionVector(socket.Direction).
            /// </summary>
            public Vector2Int TargetGridPos;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Configuration")]
        [Tooltip("The DungeonConfigSO asset that defines the room pool, " +
                 "main path length, and branch limits.")]
        [SerializeField] private DungeonConfigSO _config;

        [Header("Prefabs")]
        [Tooltip("Door/archway prefab spawned at every connected socket pair. " +
                 "Pass null to leave doorways visually open.")]
        [SerializeField] private GameObject _doorPrefab;

        [Header("Grid Settings")]
        [Tooltip("World-space size of one grid cell (room footprint). " +
                 "All room prefabs must be exactly this size in XZ.")]
        [SerializeField] private float _cellSize = 20f;

        [Header("Navigation")]
        [SerializeField] private NavMeshSurface _navMeshSurface;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // O(1) lookup of occupied grid cells — the core of the overlap check.
        private HashSet<Vector2Int> _occupiedCells = new HashSet<Vector2Int>();

        // Frontier: sockets on already-placed rooms that can still accept a
        // new neighbour. Consumed and grown as each room is placed.
        private List<SocketData> _availableSockets = new List<SocketData>();

        // Every room instance spawned during generation, parented under a
        // shared container for clean hierarchy.
        private Transform _dungeonRoot;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            Generate();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generates a new dungeon from scratch.
        /// Safe to call multiple times — clears the previous dungeon first.
        /// </summary>
        public void Generate()
        {
            // ── Validate config ──────────────────────────────────────────────
            if (_config == null)
            {
                Debug.LogError("[DungeonGenerator] _config is not assigned. " +
                               "Drag a DungeonConfigSO into the Inspector.", this);
                return;
            }

            if (_config.AvailableRooms == null || _config.AvailableRooms.Count == 0)
            {
                Debug.LogError("[DungeonGenerator] DungeonConfigSO has no available rooms. " +
                               "Populate the AvailableRooms array on the SO.", this);
                return;
            }

            // ── Clear previous state ─────────────────────────────────────────
            ClearDungeon();

            // ── Create a hierarchy root ──────────────────────────────────────
            _dungeonRoot = new GameObject("== DUNGEON ==").transform;

            // ── Step 1: Place the Start room ─────────────────────────────────
            RoomDataSO startRoomData = FindRoomByType(RoomType.Start);
            if (startRoomData == null)
            {
                Debug.LogError("[DungeonGenerator] No room with RoomType.Start found in " +
                               "DungeonConfigSO.AvailableRooms. Cannot generate.", this);
                return;
            }

            Vector2Int startCell = Vector2Int.zero;
            RoomController startRoom = SpawnRoom(startRoomData, startCell);
            RegisterOpenSockets(startRoom, startCell);

            Debug.Log($"[DungeonGenerator] Start room placed at cell {startCell}.");

            // ── Step 2: Main path loop ───────────────────────────────────────
            // MainPathLength includes the Start room, so we need
            // (MainPathLength - 1) more rooms. The last one is the Boss.
            int roomsToPlace = _config.MainPathLength - 1;

            for (int i = 0; i < roomsToPlace; i++)
            {
                // Determine the RoomType for this step:
                // Last step = Boss, everything in between = Combat/Corridor.
                bool isFinalRoom = (i == roomsToPlace - 1);
                RoomType desiredType = isFinalRoom ? RoomType.Boss : RoomType.Combat;

                // ── Find a valid socket + cell pair from the frontier ────────
                SocketData chosenSocket = FindValidSocket();

                if (chosenSocket == null)
                {
                    Debug.LogWarning($"[DungeonGenerator] Ran out of open sockets after " +
                                     $"placing {i + 1}/{roomsToPlace} rooms. " +
                                     "Dungeon may be smaller than MainPathLength.", this);
                    break;
                }

                // ── Pick a room from the pool ────────────────────────────────
                RoomDataSO roomData = isFinalRoom
                    ? FindRoomByType(RoomType.Boss)
                    : PickWeightedRoom(desiredType);

                if (roomData == null)
                {
                    // Fallback: if no Boss/Combat room exists, try any room.
                    roomData = PickWeightedRoom(null);
                    if (roomData == null)
                    {
                        Debug.LogError("[DungeonGenerator] Cannot find any valid room to place. " +
                                       "Check DungeonConfigSO.AvailableRooms.", this);
                        break;
                    }
                }

                // ── Step 3: Spawn the room ───────────────────────────────────
                Vector2Int targetCell = chosenSocket.TargetGridPos;
                RoomController newRoom = SpawnRoom(roomData, targetCell);

                // ── Step 4: Connect sockets ──────────────────────────────────
                ConnectSockets(chosenSocket.Socket, newRoom, targetCell);

                // ── Step 5: Register the new room's open sockets ─────────────
                RegisterOpenSockets(newRoom, targetCell);

                Debug.Log($"[DungeonGenerator] Room '{roomData.name}' ({roomData.Type}) " +
                          $"placed at cell {targetCell}. ({i + 2}/{_config.MainPathLength})");
            }

            Debug.Log($"[DungeonGenerator] Generation complete. " +
                      $"{_occupiedCells.Count} rooms placed, " +
                      $"{_availableSockets.Count} open socket(s) remaining.");

            // ── Step 6: Bake NavMesh ─────────────────────────────────────────
            if (_navMeshSurface != null)
            {
                Debug.Log("[DungeonGenerator] Baking NavMesh...");
                _navMeshSurface.BuildNavMesh();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ROOM SPAWNING
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Instantiates a room prefab at the world position corresponding
        /// to the given grid cell and registers the cell as occupied.
        /// </summary>
        private RoomController SpawnRoom(RoomDataSO data, Vector2Int cell)
        {
            Vector3 worldPos = GridToWorld(cell);

            GameObject instance = Instantiate(
                data.Prefab,
                worldPos,
                Quaternion.identity,
                _dungeonRoot);

            _occupiedCells.Add(cell);

            // RoomController.Awake() auto-discovers sockets/spawners.
            RoomController controller = instance.GetComponent<RoomController>();
            if (controller == null)
            {
                Debug.LogError($"[DungeonGenerator] Prefab '{data.name}' has no RoomController " +
                               "on the root. Add one to the prefab.", this);
            }

            return controller;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SOCKET CONNECTION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Connects the origin socket (on an existing room) to the matching
        /// socket on the newly-spawned room. Both sockets receive a door prefab.
        /// </summary>
        private void ConnectSockets(RoomSocket originSocket, RoomController newRoom,
                                    Vector2Int newRoomCell)
        {
            // The new room's matching socket faces the opposite direction.
            SocketDirection oppositeDir =
                RoomSocket.GetOppositeDirection(originSocket.Direction);

            RoomSocket newRoomSocket = newRoom != null
                ? newRoom.GetAvailableSocket(oppositeDir)
                : null;

            DoorController door = null;
            if (_doorPrefab != null)
            {
                GameObject doorObj = Instantiate(_doorPrefab, originSocket.transform.position, originSocket.transform.rotation, _dungeonRoot);
                door = doorObj.GetComponent<DoorController>();
            }

            // Connect the origin side.
            originSocket.AssignDoor(door);

            // Connect the new room side.
            if (newRoomSocket != null)
            {
                newRoomSocket.AssignDoor(door);
            }
            else
            {
                Debug.LogWarning($"[DungeonGenerator] New room at {newRoomCell} has no " +
                                 $"available {oppositeDir} socket to connect back to. " +
                                 "Check the prefab's RoomSocket setup.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FRONTIER MANAGEMENT
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans a newly-placed room and adds all of its unconnected sockets
        /// to the frontier, each tagged with the grid cell they point toward.
        /// </summary>
        private void RegisterOpenSockets(RoomController room, Vector2Int roomCell)
        {
            if (room == null) return;

            IReadOnlyList<RoomSocket> sockets = room.Sockets;
            for (int i = 0; i < sockets.Count; i++)
            {
                RoomSocket socket = sockets[i];
                if (socket.IsConnected) continue;

                Vector2Int targetCell = roomCell + GetDirectionVector(socket.Direction);

                _availableSockets.Add(new SocketData
                {
                    Socket        = socket,
                    TargetGridPos = targetCell
                });
            }
        }

        /// <summary>
        /// Iterates the frontier in random order, looking for a socket whose
        /// target cell is not yet occupied. Removes every invalid socket it
        /// encounters along the way (shrinks the frontier).
        /// Returns <c>null</c> if no valid socket exists.
        /// </summary>
        private SocketData FindValidSocket()
        {
            while (_availableSockets.Count > 0)
            {
                // Pick a random index and swap-remove it (O(1) removal).
                int randomIndex = Random.Range(0, _availableSockets.Count);
                SocketData candidate = _availableSockets[randomIndex];

                // Swap-remove: replace with last element, then shrink the list.
                int lastIndex = _availableSockets.Count - 1;
                _availableSockets[randomIndex] = _availableSockets[lastIndex];
                _availableSockets.RemoveAt(lastIndex);

                // Check if the target cell is free.
                if (!_occupiedCells.Contains(candidate.TargetGridPos))
                {
                    return candidate;   // Valid — use this socket.
                }

                // Target cell is occupied — discard and try another.
            }

            return null;   // Frontier exhausted — no valid placement exists.
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ROOM SELECTION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the first <see cref="RoomDataSO"/> of the given type, or
        /// <c>null</c> if none exists in the pool. Used for Start and Boss
        /// rooms that appear exactly once.
        /// </summary>
        private RoomDataSO FindRoomByType(RoomType type)
        {
            IReadOnlyList<RoomDataSO> pool = _config.AvailableRooms;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].Type == type)
                    return pool[i];
            }
            return null;
        }

        /// <summary>
        /// Weighted random selection from rooms matching a specific type.
        /// If <paramref name="filter"/> is <c>null</c>, all non-Start,
        /// non-Boss rooms are eligible (fallback pool).
        /// Respects <see cref="RoomDataSO.Weight"/>: a room with weight 3
        /// is three times as likely to be selected as one with weight 1.
        /// </summary>
        private RoomDataSO PickWeightedRoom(RoomType? filter)
        {
            IReadOnlyList<RoomDataSO> pool = _config.AvailableRooms;

            // ── Build the candidate list and total weight ────────────────────
            // Using a temporary list per call. For larger pools, cache this.
            int totalWeight = 0;
            var candidates = new List<RoomDataSO>();

            for (int i = 0; i < pool.Count; i++)
            {
                RoomDataSO room = pool[i];
                if (room == null) continue;

                // Never randomly pick Start or Boss — those are placed explicitly.
                if (room.Type == RoomType.Start || room.Type == RoomType.Boss)
                    continue;

                // If a specific filter is requested, enforce it.
                if (filter.HasValue && room.Type != filter.Value)
                    continue;

                candidates.Add(room);
                totalWeight += room.Weight;
            }

            if (candidates.Count == 0 || totalWeight <= 0)
                return null;

            // ── Weighted random pick ─────────────────────────────────────────
            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += candidates[i].Weight;
                if (roll < cumulative)
                    return candidates[i];
            }

            // Defensive fallback — should never hit.
            return candidates[candidates.Count - 1];
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GRID MATH
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts a <see cref="SocketDirection"/> to a unit offset on the
        /// 2D grid.  North = +Y, South = −Y, East = +X, West = −X.
        /// </summary>
        private static Vector2Int GetDirectionVector(SocketDirection direction)
        {
            return direction switch
            {
                SocketDirection.North => new Vector2Int( 0,  1),
                SocketDirection.South => new Vector2Int( 0, -1),
                SocketDirection.East  => new Vector2Int( 1,  0),
                SocketDirection.West  => new Vector2Int(-1,  0),
                _ => Vector2Int.zero
            };
        }

        /// <summary>
        /// Converts a grid cell coordinate to a world-space position.
        /// Y is always 0 (flat dungeon on the XZ plane).
        /// </summary>
        private Vector3 GridToWorld(Vector2Int cell)
        {
            return new Vector3(
                cell.x * _cellSize,
                0f,
                cell.y * _cellSize);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CLEANUP
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Destroys the previous dungeon hierarchy and resets all generation
        /// state, making <see cref="Generate"/> safe to call multiple times.
        /// </summary>
        private void ClearDungeon()
        {
            if (_dungeonRoot != null)
                Destroy(_dungeonRoot.gameObject);

            _occupiedCells.Clear();
            _availableSockets.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR GIZMOS
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_occupiedCells == null || _occupiedCells.Count == 0) return;

            // Draw a flat wire cube for every occupied cell so the grid is
            // visible in the Scene view during and after generation.
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.3f);
            Vector3 cellExtents = new Vector3(_cellSize, 0.1f, _cellSize);

            foreach (Vector2Int cell in _occupiedCells)
            {
                Vector3 centre = GridToWorld(cell) + new Vector3(_cellSize * 0.5f, 0f, _cellSize * 0.5f);
                // Offset centre by half cell so the wire cube is centred on the room.
                // Actually, rooms are instantiated at the cell's corner (GridToWorld),
                // so centre = corner + half-cell.
                Gizmos.DrawWireCube(centre, cellExtents);
            }

            // Draw open sockets as yellow spheres.
            Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.6f);
            foreach (SocketData sd in _availableSockets)
            {
                if (sd.Socket != null)
                    Gizmos.DrawSphere(sd.Socket.transform.position, 0.3f);
            }
        }
#endif
    }
}
