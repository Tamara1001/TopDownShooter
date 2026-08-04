
namespace TopDownShooter.Combat
{
    /// <summary>
    /// Contrato de Estrategia Abstracta para todas las armas equipables.
    /// Cualquier MonoBehaviour que implemente esta interfaz puede ser utilizado como el
    /// arma activa de Lunaria a través del Patrón Strategy en <see cref="PlayerCombat"/>.
    /// </summary>
    public interface IWeapon
    {
        /// <summary>
        /// Segundos mínimos entre ataques consecutivos.
        /// Los contextos (Player, EnemyBrain) verifican esto para controlar su cadencia de fuego.
        /// </summary>
        float Cooldown { get; }

        /// <summary>
        /// Ejecuta la lógica del ataque primario del arma.
        /// Llamado por <see cref="PlayerCombat"/> cada vez que se activa la entrada de Ataque.
        /// Las implementaciones son responsables de su propio control de cadencia de fuego,
        /// spawn de proyectiles, sonido, VFX, etc.
        /// </summary>
        void ExecuteAttack();

        /// <summary>
        /// Inyecta multiplicadores de daño y cooldown temporalmente
        /// (utilizado por el sistema D20 Dungeon Master).
        /// </summary>
        void SetDungeonMultipliers(float damageMultiplier, float cooldownMultiplier);

        // ─── Métodos de contrato futuros (descomentar a medida que se construyan los sistemas) ──────────
        // void ExecuteAlternateAttack();   // Clic derecho / fuego secundario
        // void Reload();                   // Para armas basadas en munición
        // bool CanFire { get; }            // Puerta de la FSM: ¿está lista el arma?
    }
}
