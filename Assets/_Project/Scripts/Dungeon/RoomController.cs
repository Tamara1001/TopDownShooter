

using System.Collections.Generic;
using UnityEngine;
using TopDownShooter.Combat;
using TopDownShooter.DungeonMaster;
using TopDownShooter.Enemy;
using TopDownShooter.Managers.UI;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Centro neurálgico de una instancia de sala. Descubre automáticamente todos los
    /// componentes <see cref="RoomSocket"/> y <see cref="EntitySpawnerNode"/>
    /// en los hijos durante el <c>Awake()</c>.
    /// Adjuntar al GameObject raíz de cada prefab de sala.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class RoomController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        private enum RoomState { Waiting, Active, Cleared }

        [Header("Room Identity")]
        [Tooltip("El rol de jugabilidad de esta sala. Debe coincidir con el Type de RoomDataSO utilizado para generar este prefab para que el generador seleccione el prefab de puerta correcto.")]
        [SerializeField] private RoomType _roomType;

        [Header("Spawning")]
        [SerializeField] private GameObject[] _enemyPrefabs;
        [SerializeField] private GameObject[] _environmentPrefabs;
        [SerializeField] private GameObject[] _lootPrefabs;

        // Rellenado automáticamente en Awake() a través de GetComponentsInChildren.
        // Usando List<T> internamente para poder rellenarlo desde el arreglo,
        // mientras se expone IReadOnlyList<T> externamente para mayor seguridad.
        private List<RoomSocket>        _sockets  = new List<RoomSocket>();
        private List<EntitySpawnerNode> _spawners = new List<EntitySpawnerNode>();

        private RoomState _state = RoomState.Waiting;
        private int _activeEnemyCount = 0;

        /// <summary>
        /// Referencia al jugador cacheada en OnTriggerEnter y pasada al
        /// DungeonMasterDirector para que el modificador pueda afectarlo.
        /// </summary>
        private GameObject _playerGameObject;

        // ─────────────────────────────────────────────────────────────────────
        //  PROPIEDADES DE SOLO LECTURA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// El rol de jugabilidad de esta sala tal como se define en el Inspector del prefab.
        /// Utilizado por <see cref="DungeonGenerator"/> para elegir el prefab de puerta correcto
        /// al conectar dos salas — esta es la única fuente autoritativa.
        /// </summary>
        public RoomType Type => _roomType;

        /// <summary>
        /// Todos los sockets de entrada descubiertos en la jerarquía de esta sala.
        /// Vista de solo lectura — los sistemas externos pueden iterar pero no mutar.
        /// </summary>
        public IReadOnlyList<RoomSocket> Sockets => _sockets;

        /// <summary>
        /// Todos los nodos de aparición de entidades descubiertos en la jerarquía de esta sala.
        /// Vista de solo lectura — los sistemas externos pueden iterar pero no mutar.
        /// </summary>
        public IReadOnlyList<EntitySpawnerNode> Spawners => _spawners;

        // ─────────────────────────────────────────────────────────────────────
        //  CICLO DE VIDA DE UNITY
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
        /// Escanea toda la jerarquía de hijos una vez y rellena las listas de sockets
        /// y de spawner. Llamado en Awake() — costo cero por frame.
        /// </summary>
        private void DiscoverChildComponents()
        {
            // GetComponentsInChildren incluye al GameObject raíz en sí
            // y a todos los descendientes, que es exactamente lo que queremos.
            _sockets.AddRange(GetComponentsInChildren<RoomSocket>());
            _spawners.AddRange(GetComponentsInChildren<EntitySpawnerNode>());
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Registra advertencias accionables si al prefab de la sala le faltan componentes
        /// hijos esperados. Se ejecuta una vez en Awake() durante el desarrollo para
        /// sacar a la luz los errores de configuración del prefab de inmediato.
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
        //  API PÚBLICA  (Esqueleto — a expandir)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve el primer socket no conectado que apunta en la dirección dada,
        /// o <c>null</c> si no hay ninguno disponible.
        /// Utilizado por el generador de mazmorras para encontrar puntos de conexión.
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
        /// Devuelve TODOS los sockets libres que apuntan en la dirección indicada.
        /// Usado por <see cref="DungeonGenerator.TryFitRoom"/> para probar cada
        /// alineación posible antes de declarar que la sala no cabe.
        /// En salas multi-celda puede haber más de un socket sur, norte, etc.,
        /// cada uno con un <see cref="RoomSocket.LocalGridPosition"/> distinto,
        /// generando orígenes de sala diferentes que pueden o no colisionar.
        /// </summary>
        /// <param name="direction">Dirección cardinal a filtrar.</param>
        /// <returns>Lista (posiblemente vacía) de sockets compatibles y disponibles.</returns>
        public List<RoomSocket> GetAllAvailableSockets(SocketDirection direction)
        {
            // Capacidad inicial 2: la mayoría de salas tienen a lo sumo dos
            // sockets por cara, evitando reasignaciones internas innecesarias.
            var result = new List<RoomSocket>(2);

            for (int i = 0; i < _sockets.Count; i++)
            {
                if (_sockets[i].Direction == direction && !_sockets[i].IsConnected)
                    result.Add(_sockets[i]);
            }

            return result;
        }

        /// <summary>
        /// Devuelve todos los nodos de aparición de un tipo específico.
        /// Utilizado por el WaveManager (Enemy), LootSpawner (Loot) o
        /// PropPlacer (Environment) para encontrar sus respectivos puntos de aparición.
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

                // Cachear el jugador antes de hacer cualquier otra operación,
                // porque SpawnEntities puede modificar el estado del contador.
                _playerGameObject = other.gameObject;

                SpawnEntities();

                if (_activeEnemyCount > 0)
                {
                    // Cerrar las puertas para encerrar al jugador en la sala.
                    SetAllDoors(true);

                    // Sólo las salas Combat y Boss activan el dado D20.
                    // Las salas Start, Corridor, Key y Treasure no tienen combate
                    // y no deben alterar el estado del modificador activo.
                    if (_roomType == RoomType.Combat || _roomType == RoomType.Boss)
                    {
                        DungeonMasterDirector.Instance?.TriggerRoomRoll(this, _playerGameObject);
                    }
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

            // Revertir el modificador D20 antes de abrir las puertas, de forma
            // que los efectos no persistan en la sala siguiente.
            // Si el tier fue Normal (7-14) o la sala no era Combat/Boss, este
            // método es un no-op seguro.
            if (_roomType == RoomType.Combat || _roomType == RoomType.Boss)
            {
                DungeonMasterDirector.Instance?.ClearActiveModifier();
            }

            // Abrir puertas
            SetAllDoors(false);

            // Spawnear loot en los nodos correspondientes.
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
