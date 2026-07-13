// =============================================================================
//  IDoorLock.cs
//  Project : TopDownShooter – Procedural Dungeon System
//
//  PURPOSE
//  -------
//  Lightweight veto interface for any component that can prevent a
//  DoorController from opening. Keeps DoorController decoupled from
//  concrete lock types (LockedBossDoor, etc.) living in other namespaces.
//
//  CONTRACT
//  ─────────
//  • Implement this on any MonoBehaviour that sits on (or is a sibling of)
//    a DoorController and needs to block its OpenDoor() call.
//  • DoorController queries GetComponent<IDoorLock>() once in Awake() and
//    caches the result — zero per-frame overhead.
//  • When IsLocked returns true, DoorController.OpenDoor() is a no-op.
// =============================================================================

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Veto contract consumed by <see cref="DoorController.OpenDoor"/>.
    /// Any sibling component on the door GameObject that implements this
    /// interface can block the door from opening until the lock is cleared.
    /// </summary>
    public interface IDoorLock
    {
        /// <summary>
        /// <c>true</c> while the door is locked and must not open.
        /// <c>false</c> once the lock condition has been satisfied.
        /// </summary>
        bool IsLocked { get; }
    }
}
