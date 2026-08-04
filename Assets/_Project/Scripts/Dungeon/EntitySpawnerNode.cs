

using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Componente marcador que indica un punto de aparición (spawn) dentro de un prefab de sala.
    /// Recopilado por <see cref="RoomController"/> y consumido por sistemas de aparición
    /// externos (WaveManager, LootSpawner, etc.).
    /// </summary>
    public sealed class EntitySpawnerNode : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  NESTED ENUM
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Clasifica qué tipo de entidad debe aparecer en este nodo de aparición.
        /// </summary>
        public enum SpawnerType
        {
            /// <summary>Punto de aparición de oleadas de enemigos — usado por el WaveManager.</summary>
            Enemy,

            /// <summary>Aparición de objetos de entorno (barriles, cajas, coberturas).</summary>
            Environment,

            /// <summary>Punto de caída de botín (cofres, objetos recogibles, monedas).</summary>
            Loot
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Spawner Identity")]
        [Tooltip("Qué tipo de entidad genera este nodo. Los nodos Enemy alimentan al WaveManager; los nodos Loot alimentan al LootSpawner; los nodos Environment alimentan al PropPlacer.")]
        [SerializeField] private SpawnerType _type = SpawnerType.Enemy;

        // ─────────────────────────────────────────────────────────────────────
        //  READ-ONLY PROPERTY
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>La categoría de entidad para la cual está designado este nodo.</summary>
        public SpawnerType Type => _type;

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR GIZMOS
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Esfera codificada por colores para que los diseñadores puedan distinguir visualmente
            // los tipos de aparición en la vista de Escena de un vistazo.
            Gizmos.color = _type switch
            {
                SpawnerType.Enemy       => new Color(1f, 0.2f, 0.2f, 0.7f),  // Red
                SpawnerType.Environment => new Color(0.3f, 0.8f, 0.3f, 0.7f),  // Green
                SpawnerType.Loot        => new Color(1f, 0.85f, 0.1f, 0.7f),  // Gold
                _                       => Color.white
            };

            Gizmos.DrawSphere(transform.position, 0.25f);

            // Dibujar una línea hacia arriba para hacer que los nodos sean visibles incluso si están ocluidos por la geometría del suelo.
            Gizmos.DrawRay(transform.position, Vector3.up * 0.8f);
        }
#endif
    }
}
