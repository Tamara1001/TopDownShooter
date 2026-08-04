

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Contrato de veto consumido por <see cref="DoorController.OpenDoor"/>.
    /// Cualquier componente hermano en el GameObject de la puerta que implemente esta
    /// interfaz puede bloquear la apertura de la puerta hasta que se libere el bloqueo.
    /// </summary>
    public interface IDoorLock
    {
        /// <summary>
        /// <c>true</c> mientras la puerta esté bloqueada y no deba abrirse.
        /// <c>false</c> una vez que se haya cumplido la condición de desbloqueo.
        /// </summary>
        bool IsLocked { get; }
    }
}
