
using System.Collections.Generic;
using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// ScriptableObject que contiene todos los parámetros de generación para un piso de mazmorra.
    /// Creado a través de <c>Create → Dungeon → Dungeon Config</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewDungeonConfig",
        menuName = "Dungeon/Dungeon Config",
        order    = 1)]
    public sealed class DungeonConfigSO : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("Path Parameters")]
        [Tooltip("Número de salas a lo largo del camino principal (Start → Boss inclusive). Mínimo 2 (Start + Boss).")]
        [Min(2)]
        [SerializeField] private int _mainPathLength = 6;

        [Tooltip("Número máximo de caminos secundarios (ramas) permitidos a partir del camino principal. 0 = mazmorra lineal sin salas secundarias.")]
        [Min(0)]
        [SerializeField] private int _maxBranches = 3;

        [Header("Room Pool")]
        [Tooltip("Todos los arquetipos de sala que el generador puede elegir. Incluya al menos una sala Start y una sala Boss.")]
        [SerializeField] private RoomDataSO[] _availableRooms;

        // ─────────────────────────────────────────────────────────────────────
        //  PROPIEDADES DE SOLO LECTURA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Número de salas a lo largo del camino principal (Start → Boss).</summary>
        public int MainPathLength => _mainPathLength;

        /// <summary>Ramas máximas permitidas a partir del camino principal.</summary>
        public int MaxBranches => _maxBranches;

        /// <summary>
        /// Vista de solo lectura del conjunto de salas. Evita que los llamadores muten
        /// accidentalmente el arreglo interno del SO en tiempo de ejecución.
        /// </summary>
        public IReadOnlyList<RoomDataSO> AvailableRooms => _availableRooms;

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDACIÓN EN EDITOR
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
