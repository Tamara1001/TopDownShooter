// =============================================================================
//  DungeonConfigSO.cs
//  Project : TopDownShooter – Procedural Dungeon System
//
//  PURPOSE
//  -------
//  Top-level configuration asset consumed by the dungeon generator.
//  Contains all tunable parameters for a single dungeon "recipe":
//  how long the main path is, how many branches to allow, and which
//  room archetypes are in the pool.
//
//  USAGE
//  ─────
//  1. Right-click in the Project window → Create → Dungeon → Dungeon Config.
//  2. Adjust _mainPathLength, _maxBranches, and populate _availableRooms.
//  3. Assign this asset to the DungeonGenerator's config slot.
//
//  DESIGN NOTES
//  ─────────────
//  • AvailableRooms is exposed as IReadOnlyList<RoomDataSO> to prevent
//    callers from modifying the pool at runtime (mutating SO arrays in the
//    Editor persists across sessions).
//  • Validation in OnValidate() catches misconfigurations during authoring,
//    not at runtime.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// ScriptableObject holding all generation parameters for a dungeon floor.
    /// Created via <c>Create → Dungeon → Dungeon Config</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewDungeonConfig",
        menuName = "Dungeon/Dungeon Config",
        order    = 1)]
    public sealed class DungeonConfigSO : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Path Parameters")]
        [Tooltip("Number of rooms along the main path (Start → Boss inclusive). " +
                 "Minimum 2 (Start + Boss).")]
        [Min(2)]
        [SerializeField] private int _mainPathLength = 6;

        [Tooltip("Maximum number of branch corridors allowed to sprout from " +
                 "the main path. 0 = linear dungeon with no side rooms.")]
        [Min(0)]
        [SerializeField] private int _maxBranches = 3;

        [Header("Room Pool")]
        [Tooltip("All room archetypes the generator can choose from. " +
                 "Include at least one Start and one Boss room.")]
        [SerializeField] private RoomDataSO[] _availableRooms;

        // ─────────────────────────────────────────────────────────────────────
        //  READ-ONLY PROPERTIES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Number of rooms along the main path (Start → Boss).</summary>
        public int MainPathLength => _mainPathLength;

        /// <summary>Maximum branches allowed to sprout from the main path.</summary>
        public int MaxBranches => _maxBranches;

        /// <summary>
        /// Read-only view of the room pool. Prevents callers from accidentally
        /// mutating the SO's internal array at runtime.
        /// </summary>
        public IReadOnlyList<RoomDataSO> AvailableRooms => _availableRooms;

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR VALIDATION
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_availableRooms == null || _availableRooms.Length == 0)
            {
                Debug.LogWarning($"[DungeonConfigSO] '{name}': AvailableRooms is empty. " +
                                 "The generator will have no rooms to place.", this);
            }
        }
#endif
    }
}
