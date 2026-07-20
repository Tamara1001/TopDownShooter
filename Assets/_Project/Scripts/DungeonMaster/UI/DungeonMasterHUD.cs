// =============================================================================
//  DungeonMasterHUD.cs
//  Project : TopDownShooter – Dungeon Master System
//
//  PROPÓSITO
//  ---------
//  Se encarga exclusivamente de la representación visual del sistema D20 en la
//  pantalla. Escucha los eventos estáticos del DungeonMasterDirector (Patrón
//  Observer), lo que garantiza que la lógica del juego y la UI estén totalmente
//  desacopladas.
//
//  COMPORTAMIENTO
//  --------------
//  1. Al recibir OnDiceRolled, inicia una corrutina que simula el giro de un
//     dado D20 cambiando números rápidamente antes de mostrar el resultado final.
//  2. Al recibir OnModifierApplied, muestra el panel del modificador con el
//     nombre del efecto, manteniéndolo visible durante el combate.
//  3. Al recibir OnModifierCleared, oculta el panel del modificador.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TopDownShooter.DungeonMaster.UI
{
    /// <summary>
    /// HUD dedicada a mostrar la animación del dado D20 y el modificador activo.
    /// Se suscribe de forma pasiva a los eventos globales del DungeonMasterDirector.
    /// </summary>
    public class DungeonMasterHUD : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  REFERENCIAS DE UI (Inspector)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Dice Animation UI")]
        [Tooltip("El contenedor o panel principal del dado que se muestra/oculta.")]
        [SerializeField] private GameObject _dicePanel;

        [Tooltip("Texto para mostrar los números simulados y el resultado final.")]
        [SerializeField] private TextMeshProUGUI _diceRollText;

        [Header("Modifier UI")]
        [Tooltip("El contenedor del banner del modificador que persiste durante la sala.")]
        [SerializeField] private GameObject _modifierPanel;

        [Tooltip("Texto que muestra el nombre del modificador aplicado.")]
        [SerializeField] private TextMeshProUGUI _modifierNameText;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO INTERNO
        // ─────────────────────────────────────────────────────────────────────

        // Referencia a la corrutina de animación por si entra un nuevo roll
        // mientras la animación anterior no había terminado (prevención de bugs).
        private Coroutine _diceAnimationCoroutine;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE (Suscripción a Eventos)
        // ─────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Suscribirse a los eventos estáticos del director.
            DungeonMasterDirector.OnDiceRolled += HandleDiceRolled;
            DungeonMasterDirector.OnModifierApplied += HandleModifierApplied;
            DungeonMasterDirector.OnModifierCleared += HandleModifierCleared;
        }

        private void OnDisable()
        {
            // Siempre desuscribirse al deshabilitar para evitar memory leaks 
            // o llamadas a objetos destruidos (NullReferenceException).
            DungeonMasterDirector.OnDiceRolled -= HandleDiceRolled;
            DungeonMasterDirector.OnModifierApplied -= HandleModifierApplied;
            DungeonMasterDirector.OnModifierCleared -= HandleModifierCleared;
        }

        private void Start()
        {
            // Asegurarnos de que los paneles arranquen invisibles por defecto.
            if (_dicePanel != null) _dicePanel.SetActive(false);
            if (_modifierPanel != null) _modifierPanel.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EVENT HANDLERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Se dispara justo cuando el director tira el dado. Inicia el feedback visual.
        /// </summary>
        /// <param name="finalResult">El número real que sacó el director (1-20).</param>
        private void HandleDiceRolled(int finalResult)
        {
            // Si por algún motivo se dispara de nuevo rápido, cancelar la anterior.
            if (_diceAnimationCoroutine != null)
            {
                StopCoroutine(_diceAnimationCoroutine);
            }

            _diceAnimationCoroutine = StartCoroutine(AnimateDiceRoll(finalResult));
        }

        /// <summary>
        /// Se dispara justo después de aplicar el efecto del modificador.
        /// Muestra el nombre en pantalla para que el jugador sepa a qué se enfrenta.
        /// </summary>
        /// <param name="modifierName">El nombre, o string.Empty si fue un tiro Normal.</param>
        private void HandleModifierApplied(string modifierName)
        {
            // El director manda string.Empty en los tiros de Tier Normal (7-14).
            if (string.IsNullOrEmpty(modifierName))
            {
                // Si no hay modificador (sala normal), el panel queda oculto.
                if (_modifierPanel != null) _modifierPanel.SetActive(false);
                return;
            }

            if (_modifierNameText != null)
            {
                _modifierNameText.text = modifierName;
            }

            if (_modifierPanel != null)
            {
                _modifierPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Se dispara cuando la sala es limpiada y el modificador se retira.
        /// </summary>
        private void HandleModifierCleared()
        {
            if (_modifierPanel != null)
            {
                _modifierPanel.SetActive(false);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CORRUTINAS (Game Feel)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Simula un dado rodando: parpadea números aleatorios durante una fracción
        /// de segundo, clava el resultado final, lo deja visible unos segundos 
        /// y luego oculta el panel.
        /// </summary>
        /// <param name="finalResult">El resultado genuino calculado por el backend.</param>
        private IEnumerator AnimateDiceRoll(int finalResult)
        {
            if (_dicePanel == null || _diceRollText == null) yield break;

            _dicePanel.SetActive(true);

            int iterations = 12; // Cantidad de "saltos" del dado falso
            float delay = 0.05f; // Velocidad del salto

            // Fase 1: Simulación de giro
            for (int i = 0; i < iterations; i++)
            {
                // Mostrar un número al azar entre 1 y 20
                _diceRollText.text = UnityEngine.Random.Range(1, 21).ToString();
                
                // Ralentizar sutilmente los últimos saltos para mayor dramatismo
                if (i > iterations - 4) delay += 0.02f;

                yield return new WaitForSeconds(delay);
            }

            // Fase 2: Clavar el resultado real
            _diceRollText.text = finalResult.ToString();
            
            // Aquí se podría reproducir un sonido de impacto o hacer un pequeño shake

            // Fase 3: Pausa para lectura antes de desaparecer
            yield return new WaitForSeconds(1.5f);

            _dicePanel.SetActive(false);
            _diceAnimationCoroutine = null;
        }
    }
}
