
namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Clasifica el rol de jugabilidad de una sala dentro del grafo de la mazmorra.
    /// Utilizado por el generador para hacer cumplir las reglas de colocación (ej. exactamente un Start,
    /// Boss siempre al final del camino principal) y por el RoomController para
    /// decidir qué sistemas de ejecución activar (generación de oleadas, caída de botín, etc.).
    /// </summary>
    public enum RoomType
    {
        /// <summary>Sala de aparición (spawn) del jugador. Sin enemigos, zona segura.</summary>
        Start,

        /// <summary>Encuentro de combate estándar — oleadas de enemigos.</summary>
        Combat,

        /// <summary>Sala de recompensa con cofres u objetos recogibles — sin enemigos.</summary>
        Treasure,

        /// <summary>Encuentro con el jefe de final del piso. Siempre terminal en el camino principal.</summary>
        Boss,

        /// <summary>Conector estrecho entre salas principales — encuentros opcionales.</summary>
        Corridor,

        /// <summary>Contiene la llave requerida para desbloquear la puerta del Boss.</summary>
        Key
    }

    /// <summary>
    /// Dirección cardinal de un <see cref="RoomSocket"/> relativa al espacio local
    /// de la sala. North = +Z, East = +X, South = −Z, West = −X.
    /// El generador utiliza pares opuestos (North ↔ South, East ↔ West) para
    /// encajar las salas entre sí con la alineación correcta.
    /// </summary>
    public enum SocketDirection
    {
        /// <summary>Eje local +Z — se empareja con <see cref="South"/>.</summary>
        North,

        /// <summary>Eje local −Z — se empareja con <see cref="North"/>.</summary>
        South,

        /// <summary>Eje local +X — se empareja con <see cref="West"/>.</summary>
        East,

        /// <summary>Eje local −X — se empareja con <see cref="East"/>.</summary>
        West
    }
}
