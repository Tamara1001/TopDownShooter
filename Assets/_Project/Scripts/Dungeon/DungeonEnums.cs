// =============================================================================
//  DungeonEnums.cs
//  Project : TopDownShooter – Procedural Dungeon System
//
//  PURPOSE
//  -------
//  Central home for all enum types used by the dungeon generation pipeline.
//  Grouped here because they are small, tightly coupled, and always imported
//  together — one file is easier to find and maintain than four single-enum files.
//
//  ENUMS
//  ─────
//  • RoomType        : Classifies a room's gameplay purpose (Start, Combat, etc.)
//  • SocketDirection  : Cardinal direction of a doorway within a room prefab.
// =============================================================================

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Classifies a room's gameplay role within the dungeon graph.
    /// Used by the generator to enforce placement rules (e.g. exactly one Start,
    /// Boss always at the end of the main path) and by the RoomController to
    /// decide which runtime systems to activate (wave spawning, loot drops, etc.).
    /// </summary>
    public enum RoomType
    {
        /// <summary>Player spawn room. No enemies, safe zone.</summary>
        Start,

        /// <summary>Standard combat encounter — waves of enemies.</summary>
        Combat,

        /// <summary>Reward room with chests or pickups — no enemies.</summary>
        Treasure,

        /// <summary>End-of-floor boss encounter. Always terminal on the main path.</summary>
        Boss,

        /// <summary>Narrow connector between major rooms — optional encounters.</summary>
        Corridor,

        /// <summary>Contains the key required to unlock the Boss door.</summary>
        Key
    }

    /// <summary>
    /// Cardinal direction of a <see cref="RoomSocket"/> relative to the room's
    /// local space.  North = +Z, East = +X, South = −Z, West = −X.
    /// The generator uses opposing pairs (North ↔ South, East ↔ West) to
    /// snap rooms together with correct alignment.
    /// </summary>
    public enum SocketDirection
    {
        /// <summary>+Z local axis — pairs with <see cref="South"/>.</summary>
        North,

        /// <summary>−Z local axis — pairs with <see cref="North"/>.</summary>
        South,

        /// <summary>+X local axis — pairs with <see cref="West"/>.</summary>
        East,

        /// <summary>−X local axis — pairs with <see cref="East"/>.</summary>
        West
    }
}
