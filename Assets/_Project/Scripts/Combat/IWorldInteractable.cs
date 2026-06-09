// =============================================================================
//  IWorldInteractable.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Marker interface that any world object (doors, switches, NPCs) must implement
//  to receive an interaction call from the player's E-key press.
//
//  ARCHITECTURE
//  ─────────────
//  Follows the Interface Segregation Principle. PlayerInventory only knows about
//  this contract; it never depends on concrete interactable types (VictoryDoor,
//  ShopKeeper, etc.), keeping both sides fully decoupled.
//
//  USAGE
//  ─────
//  1. Implement this interface on any MonoBehaviour that lives in the world.
//  2. Ensure its GameObject has a Collider on the _interactableLayerMask layer.
//  3. PlayerInventory.OnInteract will detect the collider, retrieve the
//     IWorldInteractable component, and call Interact(this).
// =============================================================================

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
