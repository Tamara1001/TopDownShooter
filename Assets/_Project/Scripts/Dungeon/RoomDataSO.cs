

using System.Collections.Generic;
using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Plantilla ScriptableObject para un arquetipo de sala único.
    /// Creado a través de <c>Create → Dungeon → Room Data</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewRoomData",
        menuName = "Dungeon/Room Data",
        order    = 0)]
    public sealed class RoomDataSO : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("Room Identity")]
        [Tooltip("Clasificación de jugabilidad de esta sala (Start, Combat, Treasure, Boss, Corridor).")]
        [SerializeField] private RoomType _type = RoomType.Combat;

        [Tooltip("El prefab instanciado cuando esta sala se coloca en la mazmorra. Debe tener un RoomController en el GameObject raíz.")]
        [SerializeField] private GameObject _prefab;

        [Header("Generation")]
        [Tooltip("Peso de selección relativo durante la elección aleatoria de salas. Mayor = más probable. Una sala con peso 3 es tres veces más probable que una con peso 1.")]
        [Min(1)]
        [SerializeField] private int _weight = 1;

        [Header("Spatial Footprint")]
        [Tooltip("Lista de coordenadas locales que ocupa esta sala en la grilla. " +
                 "(0,0) es la celda pivote donde se instancia el prefab. " +
                 "Salas 1×1 clásicas solo necesitan el elemento (0,0) predeterminado. " +
                 "Salas en forma de 'L' o 'T' agregan los offsets adicionales.")]
        [SerializeField] private List<Vector2Int> _footprint = new List<Vector2Int> { Vector2Int.zero };

        // ─────────────────────────────────────────────────────────────────────
        //  PROPIEDADES DE SOLO LECTURA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Clasificación de jugabilidad de esta sala.</summary>
        public RoomType Type => _type;

        /// <summary>Prefab a instanciar cuando se coloca esta sala.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>Peso de selección relativo para el selector aleatorio.</summary>
        public int Weight => _weight;

        /// <summary>
        /// Lista de celdas locales que ocupa la sala, relativas al pivote (0,0).
        /// Usada por el generador para registrar todas las celdas de la huella
        /// en el HashSet de celdas ocupadas y evitar solapamientos.
        /// </summary>
        public IReadOnlyList<Vector2Int> Footprint => _footprint;
    }
}
