// =============================================================================
//  RoomDataSO.cs
//  Project : TopDownShooter – Procedural Dungeon System
//
//  PURPOSE
//  -------
//  Immutable data container (ScriptableObject) describing a single room
//  archetype: what type it is, which prefab to instantiate, and how likely
//  it is to be selected during generation.
//
//  USAGE
//  ─────
//  1. Right-click in the Project window → Create → Dungeon → Room Data.
//  2. Assign the room Type, drag the prefab, and set the Weight.
//  3. Add the asset to a DungeonConfigSO's _availableRooms array.
//
//  DESIGN NOTES
//  ─────────────
//  • Properties are read-only to prevent accidental SO mutation at runtime.
//    (SO mutations in the Editor persist across play sessions — a subtle bug.)
//  • _weight uses 1-based weighting: a room with weight 3 is three times as
//    likely to be picked as a room with weight 1.
// =============================================================================

using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// ScriptableObject blueprint for a single room archetype.
    /// Created via <c>Create → Dungeon → Room Data</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewRoomData",
        menuName = "Dungeon/Room Data",
        order    = 0)]
    public sealed class RoomDataSO : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Room Identity")]
        [Tooltip("Gameplay classification of this room (Start, Combat, Treasure, Boss, Corridor).")]
        [SerializeField] private RoomType _type = RoomType.Combat;

        [Tooltip("The prefab instantiated when this room is placed in the dungeon. " +
                 "Must have a RoomController on the root GameObject.")]
        [SerializeField] private GameObject _prefab;

        [Header("Generation")]
        [Tooltip("Relative selection weight during random room picking. " +
                 "Higher = more likely. A room with weight 3 is three times as " +
                 "likely as one with weight 1.")]
        [Min(1)]
        [SerializeField] private int _weight = 1;

        // ─────────────────────────────────────────────────────────────────────
        //  READ-ONLY PROPERTIES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Gameplay classification of this room.</summary>
        public RoomType Type => _type;

        /// <summary>Prefab to instantiate when placing this room.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>Relative selection weight for the random picker.</summary>
        public int Weight => _weight;
    }
}
