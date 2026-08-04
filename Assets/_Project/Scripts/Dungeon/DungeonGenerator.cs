
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
        /// already-placed room and the absolute grid coordinate of the cell
        /// it points toward (accounting for the socket's LocalGridPosition).
        /// Used as the frontier data structure during generation.
        /// </summary>
        private sealed class SocketData
        {
            /// <summary>El socket físico en la sala ya colocada.</summary>
            public RoomSocket Socket;

            /// <summary>
            /// Celda absoluta de la grilla a la que apunta este socket.
            /// Calculado como: roomOrigin + socket.LocalGridPosition + GetDirectionVector(socket.Direction).
            /// Una sala candidata cuyo socket opuesto tenga LocalGridPosition (lx, ly)
            /// quedará con su origen en TargetGridPos − (lx, ly).
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

        [Tooltip("Maximum number of branching rooms to spawn off the main path.")]
        [SerializeField] private int _maxBranches = 3;

        [Header("Prefabs")]
        [Tooltip("Door/archway prefab spawned at every connected socket pair. " +
                 "Pass null to leave doorways visually open.")]
        [SerializeField] private GameObject _doorPrefab;
        [SerializeField] private GameObject _doorPrefabBoss;
        [SerializeField] private GameObject _doorPrefabTreasure;
        [SerializeField] private GameObject _doorPrefabKey;
        [SerializeField] private GameObject _victoryDoorPrefab;

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
            // La generación ya no se dispara automáticamente.
            // El GameManager es el responsable de llamar Generate()
            // según el modo de juego activo (Normal o Tutorial).
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
            RoomController startRoom = InstantiateRoomPrefab(startRoomData);

            // Posicionar en el origen del mundo y registrar TODAS las celdas
            // de la huella para salas multi-celda.
            startRoom.transform.position = GridToWorld(startCell);
            foreach (Vector2Int local in startRoomData.Footprint)
                _occupiedCells.Add(startCell + local);

            RegisterOpenSockets(startRoom, startCell);

            Debug.Log($"[DungeonGenerator] Start room placed at cell {startCell} " +
                      $"(footprint: {startRoomData.Footprint.Count} cell(s)).");

            // ── Step 2: Main path loop ───────────────────────────────────────
            // MainPathLength includes the Start room, so we need
            // (MainPathLength - 1) more rooms. The last one is the Boss.
            int roomsToPlace = _config.MainPathLength - 1;
            RoomController bossRoomInstance = null;

            for (int i = 0; i < roomsToPlace; i++)
            {
                // Determinar el tipo de sala para este paso:
                // Último paso = Boss, el resto = Combat.
                bool isFinalRoom = (i == roomsToPlace - 1);
                RoomType desiredType = isFinalRoom ? RoomType.Boss : RoomType.Combat;

                // ── Obtener un socket válido del frontier ────────────────────
                SocketData chosenSocket = FindValidSocket();

                if (chosenSocket == null)
                {
                    Debug.LogWarning($"[DungeonGenerator] Ran out of open sockets after " +
                                     $"placing {i + 1}/{roomsToPlace} rooms. " +
                                     "Dungeon may be smaller than MainPathLength.", this);
                    break;
                }

                // ── Seleccionar sala del pool ────────────────────────────────
                RoomDataSO roomData = isFinalRoom
                    ? FindRoomByType(RoomType.Boss)
                    : PickWeightedRoom(desiredType);

                if (roomData == null)
                {
                    // Fallback: si no existe sala del tipo deseado, intentar cualquiera.
                    roomData = PickWeightedRoom(null);
                    if (roomData == null)
                    {
                        Debug.LogError("[DungeonGenerator] Cannot find any valid room to place. " +
                                       "Check DungeonConfigSO.AvailableRooms.", this);
                        break;
                    }
                }

                // ── Instanciar la sala como sonda en el origen ───────────────
                // El prefab se crea en Vector3.zero para poder leer sus sockets
                // antes de decidir si cabe. Si no cabe, se destruye sin costo.
                RoomController newRoom = InstantiateRoomPrefab(roomData);

                // ── Validar la huella completa contra las celdas ocupadas ────
                if (!TryFitRoom(roomData, newRoom, chosenSocket,
                                out Vector2Int roomOrigin, out RoomSocket _))
                {
                    // La sala no cabe: destruir la sonda y reintentar este paso.
                    Destroy(newRoom.gameObject);
                    i--; // Reintentar el mismo paso con otro socket/sala.
                    continue;
                }

                // ── Cabe: posicionar, registrar celdas, conectar ─────────────
                newRoom.transform.position = GridToWorld(roomOrigin);
                newRoom.transform.SetParent(_dungeonRoot);

                foreach (Vector2Int local in roomData.Footprint)
                    _occupiedCells.Add(roomOrigin + local);

                // CRÍTICO: el bossRoomInstance solo se asigna si TryFitRoom tuvo éxito.
                if (isFinalRoom)
                    bossRoomInstance = newRoom;

                // ── Conectar sockets ─────────────────────────────────────────
                ConnectSockets(chosenSocket.Socket, newRoom, roomOrigin);

                // ── Registrar sockets abiertos en el frontier ────────────────
                // CRÍTICO: los sockets de la sala Boss se retienen intencionalmente
                // para que GenerateBranches() no pueda adjuntar nada a ella,
                // garantizando que sea un callejón sin salida terminal.
                if (!isFinalRoom)
                    RegisterOpenSockets(newRoom, roomOrigin);

                Debug.Log($"[DungeonGenerator] Room '{roomData.name}' ({roomData.Type}) " +
                          $"placed at origin {roomOrigin} " +
                          $"(footprint: {roomData.Footprint.Count} cell(s)). " +
                          $"({i + 2}/{_config.MainPathLength})" +
                          (isFinalRoom ? " [TERMINAL — sockets withheld from frontier]" : ""));
            }

            Debug.Log($"[DungeonGenerator] Main path complete. " +
                      $"{_occupiedCells.Count} rooms placed, " +
                      $"{_availableSockets.Count} open socket(s) remaining.");

            // ── Step 6: Generate Branches ────────────────────────────────────
            GenerateBranches();

            // ── Step 7: Spawn Victory Door ───────────────────────────────────
            SpawnVictoryDoor(bossRoomInstance);

            // ── Step 8: Bake NavMesh ─────────────────────────────────────────
            if (_navMeshSurface != null)
            {
                Debug.Log("[DungeonGenerator] Baking NavMesh...");
                _navMeshSurface.BuildNavMesh();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  BRANCH SPAWNING
        // ─────────────────────────────────────────────────────────────────────

        private void GenerateBranches()
        {
            bool hasSpawnedKeyRoom = false;

            for (int i = 0; i < _maxBranches; i++)
            {
                SocketData chosenSocket = FindValidSocket();
                if (chosenSocket == null) break;

                RoomType targetType = !hasSpawnedKeyRoom ? RoomType.Key : RoomType.Treasure;
                RoomDataSO roomData = PickWeightedRoom(targetType);

                if (roomData == null) continue;

                // ── Instanciar sonda y validar huella ────────────────────────
                RoomController newRoom = InstantiateRoomPrefab(roomData);

                if (!TryFitRoom(roomData, newRoom, chosenSocket,
                                out Vector2Int roomOrigin, out RoomSocket _))
                {
                    // No cabe: destruir la sonda y quemar este socket.
                    Destroy(newRoom.gameObject);
                    i--; // Reintentar este slot de rama con otro socket.
                    continue;
                }

                // ── Cabe: posicionar, registrar celdas, conectar ─────────────
                newRoom.transform.position = GridToWorld(roomOrigin);
                newRoom.transform.SetParent(_dungeonRoot);

                foreach (Vector2Int local in roomData.Footprint)
                    _occupiedCells.Add(roomOrigin + local);

                ConnectSockets(chosenSocket.Socket, newRoom, roomOrigin);
                RegisterOpenSockets(newRoom, roomOrigin);

                if (targetType == RoomType.Key)
                    hasSpawnedKeyRoom = true;

                Debug.Log($"[DungeonGenerator] Branch '{roomData.name}' ({roomData.Type}) " +
                          $"placed at origin {roomOrigin} " +
                          $"(footprint: {roomData.Footprint.Count} cell(s)).");
            }
        }

        private void SpawnVictoryDoor(RoomController bossRoom)
        {
            if (bossRoom == null || _victoryDoorPrefab == null) return;

            // Reconstruir la celda origen de la sala Boss a partir de su
            // posición en el mundo. RoundToInt absorbe cualquier imprecisión
            // de punto flotante acumulada durante el posicionamiento.
            Vector2Int roomOrigin = new Vector2Int(
                Mathf.RoundToInt(bossRoom.transform.position.x / _cellSize),
                Mathf.RoundToInt(bossRoom.transform.position.z / _cellSize));

            IReadOnlyList<RoomSocket> sockets = bossRoom.Sockets;

            // ── Primera pasada: socket preferido que apunta al espacio libre ──
            // Calcular la celda absoluta hacia la que apunta cada socket y
            // verificar que no esté ocupada antes de colocar la puerta.
            // Esto evita que la puerta de victoria spawne contra una pared
            // cuando el dungeon forma un bucle cerrado alrededor de la Boss room.
            for (int i = 0; i < sockets.Count; i++)
            {
                RoomSocket socket = sockets[i];
                if (socket.IsConnected) continue;

                // Celda absoluta a la que apunta este socket.
                Vector2Int targetCell = roomOrigin
                                       + socket.LocalGridPosition
                                       + GetDirectionVector(socket.Direction);

                if (!_occupiedCells.Contains(targetCell))
                {
                    // La celda de destino está libre — la puerta es accesible.
                    Instantiate(_victoryDoorPrefab,
                                socket.transform.position,
                                socket.transform.rotation,
                                _dungeonRoot);
                    socket.AssignDoor(null);
                    return;
                }
            }

            // ── Fallback: el mapa está tan denso que todos los sockets libres
            //    de la Boss room enfrentan celdas ocupadas. En ese caso extremo
            //    usar el primer socket desconectado sin restricción, para
            //    garantizar que siempre exista una salida.
            for (int i = 0; i < sockets.Count; i++)
            {
                RoomSocket socket = sockets[i];
                if (!socket.IsConnected)
                {
                    Debug.LogWarning("[DungeonGenerator] SpawnVictoryDoor: all free sockets on " +
                                     "the Boss room face occupied cells. " +
                                     "Placing victory door on first available socket as fallback.",
                                     this);
                    Instantiate(_victoryDoorPrefab,
                                socket.transform.position,
                                socket.transform.rotation,
                                _dungeonRoot);
                    socket.AssignDoor(null);
                    return;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ROOM SPAWNING
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Instancia el prefab de la sala en Vector3.zero como objeto sonda.
        /// NO asigna posición final ni registra celdas — eso le corresponde al
        /// llamador una vez que <see cref="TryFitRoom"/> confirma que la huella
        /// completa de la sala cabe en el grid sin solapamientos.
        /// </summary>
        private RoomController InstantiateRoomPrefab(RoomDataSO data)
        {
            // Instanciar en el origen; la posición real se aplica después de
            // validar la huella con TryFitRoom.
            GameObject instance = Instantiate(
                data.Prefab,
                Vector3.zero,
                Quaternion.identity);

            // RoomController.Awake() auto-descubre sockets y spawners.
            RoomController controller = instance.GetComponent<RoomController>();
            if (controller == null)
            {
                Debug.LogError($"[DungeonGenerator] Prefab '{data.name}' has no RoomController " +
                               "on the root. Add one to the prefab.", this);
            }

            return controller;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FOOTPRINT VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Determina si la sala candidata cabe en el grid respecto al socket
        /// del frontier dado, y calcula el origen de la sala si cabe.
        /// <para>
        /// Itera TODOS los sockets disponibles en la dirección opuesta:
        /// cada socket con un <see cref="RoomSocket.LocalGridPosition"/> distinto
        /// produce un <paramref name="roomOrigin"/> diferente. El método prueba
        /// cada alineación y retorna en el primer ajuste sin colisiones.
        /// Solo retorna false si TODAS las alineaciones posibles colisionan.
        /// </para>
        /// </summary>
        /// <param name="data">Datos de la sala (contiene la huella).</param>
        /// <param name="roomInstance">Instancia sonda ya creada (para leer sockets reales del prefab).</param>
        /// <param name="frontierSocket">Socket del frontier al que se conectará la sala.</param>
        /// <param name="roomOrigin">Celda de la grilla donde debe instanciarse el pivote (0,0) de la sala.</param>
        /// <param name="matchingSocket">Socket de la sala candidata elegido para conectar con el frontier.</param>
        /// <returns>True si al menos una alineación cabe sin solapamientos; false si todas colisionan.</returns>
        private bool TryFitRoom(RoomDataSO data, RoomController roomInstance,
                                SocketData frontierSocket,
                                out Vector2Int roomOrigin, out RoomSocket matchingSocket)
        {
            // ── Paso 1: Obtener TODOS los sockets opuestos disponibles ────────
            // En salas 1×1 esto devuelve exactamente 1 elemento (comportamiento
            // idéntico a la versión anterior). En salas multi-celda puede haber
            // varios sockets sur/norte/etc. con LocalGridPosition distintos, cada
            // uno ofreciendo una alineación diferente con el socket del frontier.
            SocketDirection oppositeDir =
                RoomSocket.GetOppositeDirection(frontierSocket.Socket.Direction);

            List<RoomSocket> matchingSockets =
                roomInstance.GetAllAvailableSockets(oppositeDir);

            if (matchingSockets.Count == 0)
            {
                // La sala no expone ningún socket en la dirección requerida.
                roomOrigin    = Vector2Int.zero;
                matchingSocket = null;
                return false;
            }

            // ── Paso 2: Probar cada alineación hasta encontrar una que quepa ──
            // Para cada socket candidato se calcula un roomOrigin independiente:
            //   roomOrigin = TargetGridPos − candidateSocket.LocalGridPosition
            // Esto coloca el pivote de la sala de modo que ese socket quede
            // exactamente en la celda a la que apunta el frontier.
            foreach (RoomSocket candidateSocket in matchingSockets)
            {
                Vector2Int candidateOrigin =
                    frontierSocket.TargetGridPos - candidateSocket.LocalGridPosition;

                // ── Paso 3: Validar la huella completa para esta alineación ───
                bool fits = true;
                foreach (Vector2Int local in data.Footprint)
                {
                    if (_occupiedCells.Contains(candidateOrigin + local))
                    {
                        // Al menos una celda de la huella choca — probar el siguiente socket.
                        fits = false;
                        break;
                    }
                }

                if (fits)
                {
                    // Esta alineación es válida: propagar resultados y retornar.
                    roomOrigin     = candidateOrigin;
                    matchingSocket = candidateSocket;
                    return true;
                }
            }

            // Todos los sockets disponibles produjeron colisión — la sala no cabe.
            roomOrigin    = Vector2Int.zero;
            matchingSocket = null;
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SOCKET CONNECTION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Connects the origin socket (on an existing room) to the matching
        /// socket on the newly-spawned room. Selects the door prefab based
        /// strictly on <paramref name="newRoom"/>.Type — the prefab's own
        /// declared <see cref="RoomType"/> — so the prefab is the single
        /// source of truth and caller intent cannot cause mismatches.
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

            // Door prefab is chosen from the DESTINATION room's RoomType.
            // This guarantees e.g. _doorPrefabBoss only appears at Boss room
            // entrances, regardless of where in the algorithm ConnectSockets
            // is invoked.
            RoomType targetType = (newRoom != null) ? newRoom.Type : RoomType.Combat;

            GameObject selectedDoorPrefab = _doorPrefab;
            if      (targetType == RoomType.Boss     && _doorPrefabBoss     != null)
                selectedDoorPrefab = _doorPrefabBoss;
            else if (targetType == RoomType.Treasure && _doorPrefabTreasure != null)
                selectedDoorPrefab = _doorPrefabTreasure;
            else if (targetType == RoomType.Key      && _doorPrefabKey      != null)
                selectedDoorPrefab = _doorPrefabKey;

            DoorController door = null;
            if (selectedDoorPrefab != null)
            {
                GameObject doorObj = Instantiate(selectedDoorPrefab, originSocket.transform.position, originSocket.transform.rotation, _dungeonRoot);
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
        /// Escanea la sala recién colocada y agrega todos sus sockets sin conectar
        /// al frontier, cada uno etiquetado con la celda absoluta de la grilla
        /// a la que apunta (teniendo en cuenta su LocalGridPosition).
        /// </summary>
        private void RegisterOpenSockets(RoomController room, Vector2Int roomOrigin)
        {
            if (room == null) return;

            IReadOnlyList<RoomSocket> sockets = room.Sockets;
            for (int i = 0; i < sockets.Count; i++)
            {
                RoomSocket socket = sockets[i];
                if (socket.IsConnected) continue;

                // La celda a la que apunta este socket es la celda de la grilla
                // donde reside el socket (roomOrigin + LocalGridPosition) más el
                // vector unitario en su dirección. Esto garantiza que una sala
                // candidata cuyo socket opuesto tenga LocalGridPosition (lx, ly)
                // calcule su origen como TargetGridPos − (lx, ly), alineando
                // correctamente los sockets de ambos lados.
                Vector2Int targetCell = roomOrigin
                                       + socket.LocalGridPosition
                                       + GetDirectionVector(socket.Direction);

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

            // Dibujar un cubo de alambre plano para cada celda ocupada.
            // GridToWorld ya devuelve la posición exacta del pivote de la sala
            // (0,0,0 local), por lo que no se necesita offset de media celda.
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.3f);
            Vector3 cellExtents = new Vector3(_cellSize, 0.1f, _cellSize);

            foreach (Vector2Int cell in _occupiedCells)
            {
                // El cubo se centra directamente en el pivote de la sala.
                Vector3 centre = GridToWorld(cell);
                Gizmos.DrawWireCube(centre, cellExtents);
            }

            // Dibujar los sockets abiertos del frontier como esferas amarillas.
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
