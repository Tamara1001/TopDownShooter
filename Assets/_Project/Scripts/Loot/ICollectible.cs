
using UnityEngine;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Interfaz de estrategia para objetos recolectables.
    /// Cualquier efecto de recolección — curación, monedas, munición, potenciadores — debe implementar
    /// este contrato para integrarse con el sistema <see cref="AutoPickupTrigger"/>.
    /// </summary>
    public interface ICollectible
    {
        /// <summary>
        /// Aplica el efecto de este recolectable al GameObject del jugador especificado.
        /// Llamado por <see cref="AutoPickupTrigger"/> cuando el jugador entra en
        /// el volumen del disparador. Las implementaciones NO deben llamar a Destroy aquí;
        /// la vida útil del objeto es gestionada estrictamente por <see cref="AutoPickupTrigger"/>.
        /// </summary>
        /// <param name="player">El GameObject raíz del jugador que entró en el disparador.</param>
        void Collect(GameObject player);
    }
}
