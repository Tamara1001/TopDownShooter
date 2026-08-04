

using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Doorway attachment point on a room prefab.
    /// Placed on each wall that can connect to an adjacent room.
    /// </summary>
    public sealed class RoomSocket : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Socket Identity")]
        [Tooltip("Cardinal direction this socket faces relative to the room's local space. " +
                 "North = +Z, East = +X, South = −Z, West = −X.")]
        [SerializeField] private SocketDirection _direction;

        [Tooltip("Coordenada local en la grilla de este socket relativo al pivote (0,0) de la sala. " +
                 "En salas 1×1 siempre es (0,0). En salas multi-celda indica qué celda " +
                 "de la huella posee este socket, para que el generador calcule " +
                 "la posición absoluta de la celda adyacente correctamente.")]
        [SerializeField] private Vector2Int _localGridPosition = Vector2Int.zero;

        [Header("Wall Reference")]
        [Tooltip("The solid wall GameObject that blocks this doorway when unconnected. " +
                 "Disabled by Connect() and replaced with a door prefab.")]
        [SerializeField] private GameObject _solidWall;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // True once Connect() has been called — prevents double-connection.
        private bool _isConnected;

        // ─────────────────────────────────────────────────────────────────────
        //  READ-ONLY PROPERTIES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Cardinal direction this socket faces.</summary>
        public SocketDirection Direction => _direction;

        /// <summary>True if this socket has been connected to another room.</summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Coordenada local en la grilla donde reside este socket,
        /// relativa al pivote (0,0) de la sala propietaria.
        /// El generador la suma a la posición de la celda de la sala para
        /// obtener la coordenada absoluta de la celda vecina a la que apunta.
        /// </summary>
        public Vector2Int LocalGridPosition => _localGridPosition;

        /// <summary>The shared door assigned to this connection, if any.</summary>
        public DoorController AssignedDoor { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Finalises a connection at this socket:
        /// <list type="number">
        ///   <item>Marks the socket as connected.</item>
        ///   <item>Disables the solid wall blocking the doorway.</item>
        ///   <item>Assigns the shared <paramref name="door"/> reference.</item>
        /// </list>
        /// Idempotent — calling twice is a safe no-op with a warning log.
        /// </summary>
        /// <param name="door">
        /// The shared door controller. Pass <c>null</c> to simply open
        /// the wall without a door (e.g. for corridor connections).
        /// </param>
        public void AssignDoor(DoorController door)
        {
            // Guard: prevent double-connection.
            if (_isConnected)
            {
                Debug.LogWarning($"[RoomSocket] '{name}' ({_direction}): Already connected. " +
                                 "Ignoring duplicate AssignDoor() call.", this);
                return;
            }

            _isConnected = true;
            AssignedDoor = door;

            // Disable the solid wall so the doorway opening is revealed.
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
        //  STATIC UTILITY
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the opposing <see cref="SocketDirection"/> for a given direction.
        /// Used by the generator to find the matching socket on an adjacent room
        /// (e.g. our North socket connects to their South socket).
        /// </summary>
        public static SocketDirection GetOppositeDirection(SocketDirection direction)
        {
            return direction switch
            {
                SocketDirection.North => SocketDirection.South,
                SocketDirection.South => SocketDirection.North,
                SocketDirection.East  => SocketDirection.West,
                SocketDirection.West  => SocketDirection.East,
                _ => direction   // Defensive fallback — should never hit.
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR GIZMOS
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Draw a small directional arrow so designers can verify socket
            // orientation in the Scene view without entering Play Mode.
            Gizmos.color = _isConnected
                ? new Color(0.2f, 0.9f, 0.3f, 0.8f)   // Green = connected
                : new Color(0.9f, 0.2f, 0.2f, 0.8f);   // Red   = available

            Gizmos.DrawSphere(transform.position, 0.15f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.6f);
        }
#endif
    }
}
