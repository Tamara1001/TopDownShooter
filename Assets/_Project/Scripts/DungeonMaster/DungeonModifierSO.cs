// =============================================================================
//  DungeonModifierSO.cs
//  Project : TopDownShooter – Dungeon Master System
//
//  PROPÓSITO
//  ---------
//  ScriptableObject abstracto que sirve como contrato base para todos los
//  modificadores del sistema D20 "Dungeon Master".
//
//  PATRÓN DE DISEÑO: Strategy + Template Method
//  ─────────────────────────────────────────────
//  • Cada modificador concreto (ej. SpeedBoostModifier, DoubleDamageModifier)
//    hereda de esta clase e implementa ApplyModifier / RevertModifier.
//  • El DungeonMasterDirector actúa como Context: sólo conoce esta interfaz,
//    nunca los tipos concretos. Así agregar un modificador nuevo = una sola
//    clase nueva, sin tocar el Director ni la HUD.
//
//  CÓMO CREAR UN MODIFICADOR NUEVO
//  ─────────────────────────────────
//  1. Crear una clase concreta que herede DungeonModifierSO.
//  2. Agregar [CreateAssetMenu] con el menú que corresponda.
//  3. Implementar ApplyModifier y RevertModifier.
//  4. Crear el asset en el Project y arrastrarlo al pool del Director.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace TopDownShooter.DungeonMaster
{
    /// <summary>
    /// Contrato base para todos los modificadores de sala del sistema D20.
    /// Instanciar subclases concretas como assets en el Project window.
    /// </summary>
    public abstract class DungeonModifierSO : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────────────
        //  DATOS DE IDENTIDAD
        // ─────────────────────────────────────────────────────────────────────

        [Header("Modifier Identity")]
        [Tooltip("Nombre visible en la HUD cuando este modificador se activa. " +
                 "Debe ser descriptivo y corto (ej. 'Velocidad Doble', 'Maldición de Fuego').")]
        [SerializeField] private string _modifierName;

        [Tooltip("Descripción breve del efecto para tooltips o UI expandida (opcional).")]
        [SerializeField] [TextArea(2, 4)] private string _description;

        // ─────────────────────────────────────────────────────────────────────
        //  PROPIEDADES PÚBLICAS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Nombre del modificador que se muestra en la HUD al activarse.
        /// El <see cref="DungeonMasterDirector"/> lo envía vía el evento
        /// <see cref="DungeonMasterDirector.OnModifierApplied"/>.
        /// </summary>
        public string ModifierName => _modifierName;

        /// <summary>
        /// Descripción corta del efecto para tooltips o paneles de información.
        /// </summary>
        public string Description => _description;

        // ─────────────────────────────────────────────────────────────────────
        //  CONTRATO ABSTRACTO
        //  Obligatorio implementar en cada subclase concreta.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Aplica el efecto de este modificador al jugador y/o a los enemigos
        /// de la sala activa. Llamado por <see cref="DungeonMasterDirector"/>
        /// cuando se selecciona este modificador del pool correspondiente.
        /// </summary>
        /// <param name="player">
        /// El GameObject raíz del jugador. Usar TryGetComponent para acceder
        /// a sus sub-sistemas (HealthComponent, PlayerResourceComponent, etc.).
        /// </param>
        /// <param name="roomEnemies">
        /// Lista de GameObjects enemigos vivos en la sala en el momento del
        /// roll. Puede estar vacía si aún no se spawnearon.
        /// </param>
        public abstract void ApplyModifier(GameObject player, List<GameObject> roomEnemies);

        /// <summary>
        /// Revierte exactamente lo que ApplyModifier hizo, dejando al jugador
        /// y los enemigos en su estado original. Llamado por
        /// <see cref="DungeonMasterDirector.ClearActiveModifier"/> cuando la
        /// sala se despeja o el jugador muere.
        /// </summary>
        /// <param name="player">El mismo GameObject del jugador.</param>
        /// <param name="roomEnemies">
        /// Lista de enemigos de la sala (puede tener menos elementos si
        /// algunos murieron durante el combate — validar nulos).
        /// </param>
        public abstract void RevertModifier(GameObject player, List<GameObject> roomEnemies);

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDACIÓN EN EDITOR
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // Previene assets sin nombre que serían invisibles en la HUD.
            if (string.IsNullOrWhiteSpace(_modifierName))
            {
                Debug.LogWarning($"[DungeonModifierSO] '{name}': ModifierName está vacío. " +
                                 "La HUD mostrará una cadena vacía al activarse.", this);
            }
        }
#endif
    }
}
