

using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Punto de conexión de entrada (puerta) en un prefab de sala.
    /// Colocado en cada pared que pueda conectarse a una sala adyacente.
    /// </summary>
    public sealed class RoomSocket : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("Socket Identity")]
        [Tooltip("Dirección cardinal a la que mira este socket relativa al espacio local de la sala. North = +Z, East = +X, South = −Z, West = −X.")]
        [SerializeField] private SocketDirection _direction;

        [Tooltip("Coordenada local en la grilla de este socket relativo al pivote (0,0) de la sala. " +
                 "En salas 1×1 siempre es (0,0). En salas multi-celda indica qué celda " +
                 "de la huella posee este socket, para que el generador calcule " +
                 "la posición absoluta de la celda adyacente correctamente.")]
        [SerializeField] private Vector2Int _localGridPosition = Vector2Int.zero;

        [Header("Wall Reference")]
        [Tooltip("El GameObject de pared sólida que bloquea esta entrada cuando no está conectada. Se desactiva mediante Connect() y se reemplaza con un prefab de puerta.")]
        [SerializeField] private GameObject _solidWall;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO PRIVADO
        // ─────────────────────────────────────────────────────────────────────

        // Verdadero una vez que se ha llamado a Connect() — evita la doble conexión.
        private bool _isConnected;

        // ─────────────────────────────────────────────────────────────────────
        //  PROPIEDADES DE SOLO LECTURA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Dirección cardinal a la que mira este socket.</summary>
        public SocketDirection Direction => _direction;

        /// <summary>Verdadero si este socket ha sido conectado a otra sala.</summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Coordenada local en la grilla donde reside este socket,
        /// relativa al pivote (0,0) de la sala propietaria.
        /// El generador la suma a la posición de la celda de la sala para
        /// obtener la coordenada absoluta de la celda vecina a la que apunta.
        /// </summary>
        public Vector2Int LocalGridPosition => _localGridPosition;

        /// <summary>La puerta compartida asignada a esta conexión, si existe alguna.</summary>
        public DoorController AssignedDoor { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        //  API PÚBLICA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Finaliza una conexión en este socket:
        /// <list type="number">
        ///   <item>Marca el socket como conectado.</item>
        ///   <item>Desactiva la pared sólida que bloquea la entrada.</item>
        ///   <item>Asigna la referencia de <paramref name="door"/> compartida.</item>
        /// </list>
        /// Idempotente — llamarlo dos veces es una operación segura y sin efectos secundarios con un registro de advertencia.
        /// </summary>
        /// <param name="door">
        /// El controlador de puerta compartido. Pase <c>null</c> para simplemente abrir
        /// la pared sin una puerta (por ejemplo, para conexiones de pasillos).
        /// </param>
        public void AssignDoor(DoorController door)
        {
            // Guardia: evitar la doble conexión.
            if (_isConnected)
            {
                Debug.LogWarning($"[RoomSocket] '{name}' ({_direction}): Already connected. " +
                                 "Ignoring duplicate AssignDoor() call.", this);
                return;
            }

            _isConnected = true;
            AssignedDoor = door;

            // Desactivar la pared sólida para que se revele la apertura de la entrada.
            if (_solidWall != null)
            {
                _solidWall.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"[RoomSocket] '{name}' ({_direction}): _solidWall is not " +
                                 "assigned. The doorway will appear open by default.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UTILIDAD ESTÁTICA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve la <see cref="SocketDirection"/> opuesta para una dirección dada.
        /// Utilizada por el generador para encontrar el socket coincidente en una sala adyacente
        /// (por ejemplo, nuestro socket North se conecta a su socket South).
        /// </summary>
        public static SocketDirection GetOppositeDirection(SocketDirection direction)
        {
            return direction switch
            {
                SocketDirection.North => SocketDirection.South,
                SocketDirection.South => SocketDirection.North,
                SocketDirection.East  => SocketDirection.West,
                SocketDirection.West  => SocketDirection.East,
                _ => direction   // Respaldo defensivo — nunca debería ocurrir.
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GIZMOS DE EDITOR
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Dibujar una pequeña flecha direccional para que los diseñadores puedan verificar
            // la orientación del socket en la vista de Escena sin entrar en Play Mode.
            Gizmos.color = _isConnected
                ? new Color(0.2f, 0.9f, 0.3f, 0.8f)   // Green = connected
                : new Color(0.9f, 0.2f, 0.2f, 0.8f);   // Red   = available

            Gizmos.DrawSphere(transform.position, 0.15f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.6f);
        }
#endif
    }
}
