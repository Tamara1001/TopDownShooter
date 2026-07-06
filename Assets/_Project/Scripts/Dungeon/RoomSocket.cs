// =============================================================================
//  RoomSocket.cs
//  Project : TopDownShooter – Procedural Dungeon System
//
//  PURPOSE
//  -------
//  Represents a single doorway attachment point on a room prefab.
//  Each room prefab has up to 4 sockets (one per cardinal wall).
//  The dungeon generator queries available sockets to determine which
//  directions a room can connect to, and calls Connect() to finalise
//  the link — disabling the solid wall and instantiating a door.
//
//  PLACEMENT GUIDE
//  ─────────────────
//  1. Create an empty child GameObject at the centre of a wall opening.
//  2. Attach this script and set _direction to the wall's outward facing.
//  3. Drag the solid-wall mesh into _solidWall.
//  4. The generator or RoomController will call Connect() at runtime.
//
//  DESIGN NOTES
//  ─────────────
//  • Connect() is idempotent — calling it twice on the same socket is a
//    safe no-op that logs a warning rather than spawning duplicate doors.
//  • _solidWall is null-checked for graceful degradation if a designer
//    forgets the assignment.
// =============================================================================

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

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Finalises a connection at this socket:
        /// <list type="number">
        ///   <item>Marks the socket as connected.</item>
        ///   <item>Disables the solid wall blocking the doorway.</item>
        ///   <item>Instantiates <paramref name="doorPrefab"/> as a child
        ///         of this socket's transform (inherits position/rotation).</item>
        /// </list>
        /// Idempotent — calling twice is a safe no-op with a warning log.
        /// </summary>
        /// <param name="doorPrefab">
        /// The door/archway prefab to spawn. Pass <c>null</c> to simply open
        /// the wall without placing a visual door (e.g. for corridor connections).
        /// </param>
        public void Connect(GameObject doorPrefab)
        {
            // Guard: prevent double-connection.
            if (_isConnected)
            {
                Debug.LogWarning($"[RoomSocket] '{name}' ({_direction}): Already connected. " +
                                 "Ignoring duplicate Connect() call.", this);
                return;
            }

            _isConnected = true;

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

            // Spawn the door visual as a child of this socket.
            if (doorPrefab != null)
            {
                Instantiate(doorPrefab, transform.position, transform.rotation, transform);
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
