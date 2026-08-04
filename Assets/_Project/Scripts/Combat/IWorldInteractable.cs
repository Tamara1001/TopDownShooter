
using TopDownShooter.Player;

namespace TopDownShooter.Interaction
{
    /// <summary>
    /// Contrato para cualquier objeto del mundo que pueda responder a la entrada de
    /// Interactuar (E) del jugador. El inventario pasado como parámetro le da al
    /// implementador acceso de sólo lectura a lo que el jugador sostiene actualmente.
    /// </summary>
    public interface IWorldInteractable
    {
        /// <summary>
        /// Llamado por <see cref="PlayerInventory"/> cuando el jugador presiona la
        /// tecla de interactuar mientras está dentro del alcance de este objeto.
        /// </summary>
        /// <param name="inventory">
        /// El inventario del jugador. Los implementadores pueden leer
        /// <see cref="PlayerInventory.CurrentConsumable"/> (y otras ranuras)
        /// para validar condiciones sin acoplarse directamente al prefab del jugador.
        /// </param>
        void Interact(PlayerInventory inventory);
    }
}
