// =============================================================================
//  DungeonMasterDirector.cs
//  Project : TopDownShooter – Dungeon Master System
//
//  PROPÓSITO
//  ---------
//  Manager Singleton que orquesta el sistema de dados D20 "Dungeon Master".
//  Es el único punto de entrada para iniciar un roll y para limpiar el
//  modificador activo. La HUD y otros sistemas escuchan sus eventos estáticos
//  sin necesitar una referencia directa a esta instancia.
//
//  FLUJO DE EJECUCIÓN
//  ──────────────────
//  1. RoomController detecta al jugador entrando en una sala Combat/Boss.
//  2. Llama a DungeonMasterDirector.Instance.TriggerRoomRoll(room, player).
//  3. El Director tira el D20, determina el tier y elige un modificador random.
//  4. Dispara OnDiceRolled (int) → la HUD anima el dado.
//  5. Aplica el modificador y dispara OnModifierApplied (string) → la HUD
//     muestra el nombre del modificador.
//  6. Cuando la sala se despeja, RoomController llama ClearActiveModifier().
//  7. El Director llama RevertModifier y limpia el cache.
//
//  TIERS DEL D20
//  ─────────────
//  • 1          → Critical Failure  (pool: criticalFailures)
//  • 2 – 6      → Bad Roll          (pool: badRolls)
//  • 7 – 14     → Normal            (sin modificador)
//  • 15 – 19    → Good Roll         (pool: goodRolls)
//  • 20         → Critical Success  (pool: criticalSuccesses)
//
//  DESACOPLAMIENTO
//  ────────────────
//  • Los eventos son estáticos → la HUD se suscribe sin referencia directa.
//  • El Director NO conoce tipos concretos de modificador; sólo DungeonModifierSO.
//  • RoomController NO conoce DungeonModifierSO; sólo llama a los dos métodos
//    públicos del Director.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TopDownShooter.Dungeon;

namespace TopDownShooter.DungeonMaster
{
    /// <summary>
    /// Singleton manager del sistema D20. Tira el dado al activarse una sala,
    /// selecciona el modificador del pool correspondiente al tier obtenido,
    /// lo aplica y lo revierte cuando la sala es despejada.
    /// </summary>
    public class DungeonMasterDirector : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  SINGLETON
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Instancia global del Director. Null-safe: los llamadores deben
        /// comprobar que no sea null antes de invocar métodos.
        /// </summary>
        public static DungeonMasterDirector Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        //  POOLS DE MODIFICADORES (Inspector)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Modifier Pools")]
        [Tooltip("Modificadores para el resultado 1 (Fallo Crítico). " +
                 "Se aplican penalizaciones severas al jugador.")]
        [SerializeField] private List<DungeonModifierSO> criticalFailures = new();

        [Tooltip("Modificadores para resultados 2–6 (Mal Tiro). " +
                 "Penalizaciones leves o ventajas para los enemigos.")]
        [SerializeField] private List<DungeonModifierSO> badRolls = new();

        [Tooltip("Modificadores para resultados 15–19 (Buen Tiro). " +
                 "Ventajas moderadas para el jugador.")]
        [SerializeField] private List<DungeonModifierSO> goodRolls = new();

        [Tooltip("Modificadores para el resultado 20 (Éxito Crítico). " +
                 "Ventajas poderosas o efectos espectaculares para el jugador.")]
        [SerializeField] private List<DungeonModifierSO> criticalSuccesses = new();

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO INTERNO
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Modificador actualmente en efecto. Null si la sala es Normal o no
        /// hay sala activa. Se cachea para poder revertirlo exactamente en
        /// ClearActiveModifier, independientemente de cambios al pool en runtime.
        /// </summary>
        private DungeonModifierSO _activeModifier;

        /// <summary>
        /// Referencia al jugador activo cacheada en TriggerRoomRoll para pasarla
        /// a RevertModifier sin necesitar una nueva búsqueda en ClearActiveModifier.
        /// </summary>
        private GameObject _cachedPlayer;

        /// <summary>
        /// Lista de enemigos vivos al momento del roll. Pasada a Apply y Revert.
        /// Se construye desde los spawns registrados en el RoomController activo.
        /// </summary>
        private List<GameObject> _cachedRoomEnemies = new();

        // ─────────────────────────────────────────────────────────────────────
        //  EVENTOS ESTÁTICOS (HUD y otros sistemas se suscriben aquí)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Disparado inmediatamente después de calcular el resultado del D20.
        /// El entero es el valor crudo (1–20).
        /// La HUD lo usa para animar el dado antes de mostrar el modificador.
        /// </summary>
        public static event Action<int> OnDiceRolled;

        /// <summary>
        /// Disparado después de aplicar el modificador seleccionado.
        /// El string es <see cref="DungeonModifierSO.ModifierName"/> del modificador activo,
        /// o <see cref="string.Empty"/> si el tier es Normal (7–14, sin modificador).
        /// La HUD lo usa para mostrar el banner con el nombre del efecto.
        /// </summary>
        public static event Action<string> OnModifierApplied;

        /// <summary>
        /// Disparado cuando el modificador activo es revertido y limpiado.
        /// La HUD puede usarlo para ocultar el banner o reproducir un efecto de salida.
        /// </summary>
        public static event Action OnModifierCleared;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Patrón Singleton: destruir duplicados que se carguen tarde.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  API PÚBLICA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Punto de entrada principal del sistema. Llamado por
        /// <see cref="TopDownShooter.Dungeon.RoomController"/> cuando una sala
        /// Combat o Boss se activa (las puertas se cierran).
        /// </summary>
        /// <param name="room">
        /// La sala que se acaba de activar. Se usa para recolectar la lista
        /// de enemigos vivos y pasarla al modificador.
        /// </param>
        /// <param name="player">
        /// El GameObject raíz del jugador. Se cachea para el RevertModifier.
        /// </param>
        public void TriggerRoomRoll(RoomController room, GameObject player)
        {
            // Guardar referencias para el Revert posterior sin búsquedas extra.
            _cachedPlayer = player;
            _cachedRoomEnemies = CollectRoomEnemies(room);

            // ── Tirar el D20 ─────────────────────────────────────────────────
            // Random.Range es exclusivo en el límite superior, por eso usamos 21.
            int roll = UnityEngine.Random.Range(1, 21);

            Debug.Log($"[DungeonMasterDirector] D20 rolled: {roll} in room '{room.name}'.");

            // Notificar a la HUD para que anime el dado ANTES de aplicar el efecto.
            OnDiceRolled?.Invoke(roll);

            // ── Seleccionar el pool según el tier ────────────────────────────
            DungeonModifierSO selected = SelectModifierForRoll(roll);

            // ── Aplicar el modificador (o no-op si es tier Normal) ───────────
            ApplySelectedModifier(selected);
        }

        /// <summary>
        /// Revierte el modificador activo y limpia el estado interno.
        /// Llamado por <see cref="TopDownShooter.Dungeon.RoomController"/>
        /// cuando todos los enemigos mueren y la sala queda despejada.
        /// </summary>
        public void ClearActiveModifier()
        {
            if (_activeModifier == null)
            {
                // Sala Normal (7–14): no hay nada que revertir.
                return;
            }

            Debug.Log($"[DungeonMasterDirector] Reverting modifier: '{_activeModifier.ModifierName}'.");

            _activeModifier.RevertModifier(_cachedPlayer, _cachedRoomEnemies);

            // Limpiar el estado para que la próxima sala empiece desde cero.
            _activeModifier  = null;
            _cachedPlayer    = null;
            _cachedRoomEnemies.Clear();

            OnModifierCleared?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  LÓGICA PRIVADA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Determina qué modificador corresponde al resultado del dado según
        /// el sistema de tiers, elige uno aleatorio del pool y lo devuelve.
        /// Retorna null si el tier es Normal (7–14) — no se aplica ningún efecto.
        /// </summary>
        /// <param name="roll">Resultado del D20 (1–20).</param>
        private DungeonModifierSO SelectModifierForRoll(int roll)
        {
            return roll switch
            {
                1             => PickRandom(criticalFailures),
                >= 2 and <= 6 => PickRandom(badRolls),
                // Rango 7–14: tier Normal. Sin modificador. No tocar el juego.
                >= 7  and <= 14 => null,
                >= 15 and <= 19 => PickRandom(goodRolls),
                20              => PickRandom(criticalSuccesses),
                _               => null   // Defensa ante valores fuera de rango.
            };
        }

        /// <summary>
        /// Aplica el modificador seleccionado, cachea la referencia y dispara
        /// el evento <see cref="OnModifierApplied"/> con el nombre del efecto.
        /// Si <paramref name="modifier"/> es null (tier Normal), el evento se
        /// dispara igualmente con string.Empty para que la HUD pueda reaccionar.
        /// </summary>
        /// <param name="modifier">El modificador a aplicar, o null si es Normal.</param>
        private void ApplySelectedModifier(DungeonModifierSO modifier)
        {
            _activeModifier = modifier;

            if (_activeModifier == null)
            {
                // Tier Normal: notificar a la HUD que no hay modificador activo.
                Debug.Log("[DungeonMasterDirector] Normal tier (7-14): no modifier applied.");
                OnModifierApplied?.Invoke(string.Empty);
                return;
            }

            Debug.Log($"[DungeonMasterDirector] Applying modifier: '{_activeModifier.ModifierName}'.");

            _activeModifier.ApplyModifier(_cachedPlayer, _cachedRoomEnemies);

            // Notificar a la HUD con el nombre del efecto activo.
            OnModifierApplied?.Invoke(_activeModifier.ModifierName);
        }

        /// <summary>
        /// Elige un elemento aleatorio de la lista provista.
        /// Retorna null si la lista está vacía o no asignada, para que el
        /// sistema degrade de forma segura sin lanzar excepciones.
        /// </summary>
        /// <param name="pool">Pool de modificadores correspondiente al tier.</param>
        private static DungeonModifierSO PickRandom(List<DungeonModifierSO> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                Debug.LogWarning("[DungeonMasterDirector] Modifier pool is empty or null. " +
                                 "No modifier will be applied for this tier. " +
                                 "Populate the pool in the Inspector.");
                return null;
            }

            // Random.Range es exclusivo en el límite superior.
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        /// <summary>
        /// Recolecta los GameObjects enemigos activos dentro de la sala
        /// consultando los <see cref="EntitySpawnerNode"/> de tipo Enemy
        /// en el RoomController. Se llama una sola vez al inicio del roll.
        /// </summary>
        /// <remarks>
        /// En esta versión la lista se construye desde los hijos del RoomController
        /// con tag "Enemy", porque los enemigos son spawneados como hijos del transform
        /// de la sala. Es la forma más directa sin acoplar al WaveManager.
        /// </remarks>
        /// <param name="room">La sala activa.</param>
        private static List<GameObject> CollectRoomEnemies(RoomController room)
        {
            var enemies = new List<GameObject>();
            if (room == null) return enemies;

            // Los enemigos se instancian como hijos del transform de la sala
            // en RoomController.SpawnEntities(), por lo que buscar en los hijos
            // con el tag "Enemy" es la forma más directa de obtenerlos.
            foreach (Transform child in room.transform)
            {
                if (child.CompareTag("Enemy"))
                    enemies.Add(child.gameObject);
            }

            return enemies;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDACIÓN EN EDITOR
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Advertir al diseñador si los pools están vacíos, porque el sistema
            // degradará silenciosamente a "sin modificador" en runtime.
            WarnIfEmpty(criticalFailures,  "criticalFailures");
            WarnIfEmpty(badRolls,          "badRolls");
            WarnIfEmpty(goodRolls,         "goodRolls");
            WarnIfEmpty(criticalSuccesses, "criticalSuccesses");
        }

        private void WarnIfEmpty(List<DungeonModifierSO> pool, string poolName)
        {
            if (pool == null || pool.Count == 0)
            {
                Debug.LogWarning($"[DungeonMasterDirector] Pool '{poolName}' está vacío. " +
                                 $"El tier correspondiente no aplicará modificadores.", this);
            }
        }
#endif
    }
}
