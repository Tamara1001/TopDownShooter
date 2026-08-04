

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
