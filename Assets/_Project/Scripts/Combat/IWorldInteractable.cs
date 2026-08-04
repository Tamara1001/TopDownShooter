
using TopDownShooter.Player;

namespace TopDownShooter.Interaction
{
    /// <summary>
    /// Contract for any world object that can respond to the player's
    /// Interact (E) input. The inventory passed as parameter gives the
    /// implementor read-only access to what the player is currently holding.
    /// </summary>
    public interface IWorldInteractable
    {
        /// <summary>
        /// Called by <see cref="PlayerInventory"/> when the player presses the
        /// Interact key while within range of this object.
        /// </summary>
        /// <param name="inventory">
        /// The player's inventory. Implementors may read
        /// <see cref="PlayerInventory.CurrentConsumable"/> (and other slots)
        /// to validate conditions without coupling to the Player prefab directly.
        /// </param>
        void Interact(PlayerInventory inventory);
    }
}
