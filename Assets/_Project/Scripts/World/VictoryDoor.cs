


using UnityEngine;
using TopDownShooter.Inventory;
using TopDownShooter.Player;
using TopDownShooter.Interaction;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TopDownShooter.World
{
    /// <summary>
    /// Objeto del mundo que requiere que una llave <see cref="ConsumableDataSO"/> específica
    /// esté en la ranura de consumibles del jugador antes de otorgar la victoria.
    /// Implementa <see cref="IWorldInteractable"/> para integrarse con el
    /// flujo de interacción de la tecla E de <see cref="PlayerInventory"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class VictoryDoor : MonoBehaviour, IWorldInteractable
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("Door Configuration")]
        [Tooltip("El ConsumableDataSO que representa la llave que desbloquea esta puerta. " +
                 "Debe ser un objeto de misión (IsQuestItem = true) para que no se consuma " +
                 "accidentalmente con Q.")]
        [SerializeField] private ConsumableDataSO _requiredKey;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO PRIVADO
        // ─────────────────────────────────────────────────────────────────────

        // Evita que el estado de victoria se active más de una vez si el jugador
        // presiona E repetidamente antes de que se complete la transición de estado.
        // Refleja la salvaguarda _isUnlocked utilizada en LockedBossDoor.
        // Expuesto públicamente para que las herramientas de depuración puedan consultar el estado sin reflexión.
        public bool IsUnlocked { get; private set; } = false;

        // ─────────────────────────────────────────────────────────────────────
        //  IMPLEMENTACIÓN DE IWorldInteractable
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Llamado por <see cref="PlayerInventory"/> cuando el jugador presiona E
        /// cerca de esta puerta. Comprueba si el jugador sostiene <see cref="_requiredKey"/>
        /// en la ranura de consumibles y realiza la transición a Victoria si es así.
        /// </summary>
        /// <param name="inventory">The player's inventory to inspect.</param>
        public void Interact(PlayerInventory inventory)
        {
            // ── Traza del punto de entrada ───────────────────────────────────
            // Si esta línea NUNCA aparece en la consola, el problema está río arriba:
            // falta el colisionador de la puerta, está en la capa incorrecta o se encuentra fuera
            // del OverlapSphere de PlayerInventory._pickupRadius.
            Debug.Log($"[VictoryDoor] Interact called by '{inventory?.name ?? "NULL"}'. " +
                      $"Player holds: '{inventory?.CurrentConsumable?.DisplayName ?? "None"}'.");

            // ── Salvaguarda: ya desbloqueada ─────────────────────────────────
            if (IsUnlocked) return;

            // ── Salvaguarda: el inventario debe ser válido ───────────────────
            if (inventory == null)
            {
                Debug.LogError("[VictoryDoor] Interact received a null PlayerInventory reference. " +
                               "Check that PlayerInventory calls Interact(this).", this);
                return;
            }

            // ── Salvaguarda: la llave requerida debe estar configurada ───────
            if (_requiredKey == null)
            {
                Debug.LogError("[VictoryDoor] _requiredKey is not assigned! " +
                               "Assign a ConsumableDataSO to _requiredKey in the Inspector.", this);
                return;
            }

            // ── Comparación de llaves ────────────────────────────────────────
            // Igualdad de referencia de SO: dos objetos que comparten el mismo asset SO son
            // del mismo tipo de llave — no se necesita ni se desea comparación de cadenas.
            string heldKeyName   = inventory.CurrentConsumable?.DisplayName ?? "None";
            string neededKeyName = _requiredKey.DisplayName;

            Debug.Log($"[VictoryDoor] Key check — Required: '{neededKeyName}' | " +
                      $"Player holds: '{heldKeyName}'.");

            if (inventory.CurrentConsumable == _requiredKey)
            {
                Debug.Log($"[VictoryDoor] Key '{neededKeyName}' accepted.");

                // ── Verificación del modo de juego activo ────────────────────
                // El comportamiento post-victoria depende de si el jugador
                // completó el Tutorial o una partida Normal.
                if (GameManager.Instance.CurrentMode == GameManager.GameMode.Tutorial)
                {
                    // Modo Tutorial: en lugar de mostrar la pantalla de victoria,
                    // se lanza una partida Normal desde cero. StartNewGame() se
                    // encarga de cambiar el modo a Normal y recargar la escena,
                    // lo que dispara la generación procedural en OnSceneLoaded.
                    Debug.Log("[VictoryDoor] Tutorial completed! Starting a fresh Normal run.");
                    GameManager.Instance.StartNewGame();
                    return; // Evitar que el resto de la lógica de victoria se ejecute.
                }

                // Modo Normal: flujo de victoria estándar.
                // El GameManager congela el tiempo y notifica a la UI.
                Debug.Log($"[VictoryDoor] Triggering Victory!");
                IsUnlocked = true;
                GameManager.Instance.ChangeState(GameManager.GameState.Victory);
            }
            else
            {
                Debug.Log($"[VictoryDoor] Access denied. " +
                          $"Required '{neededKeyName}' but player holds '{heldKeyName}'. " +
                          "Obtain the correct key item and try again.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UTILIDADES Y GIZMOS DE EDITOR
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        /// <summary>
        /// Corrección de un clic para un BoxCollider que está enterrado dentro de la malla de la puerta
        /// y, por lo tanto, es invisible para el OverlapSphere de PlayerInventory.
        /// Ejecutar a través de clic derecho → "Reset Collider Size" en el Inspector.
        /// Ajuste el tamaño para que coincida con su prefab después de ejecutar esto.
        /// </summary>
        [ContextMenu("Reset Collider Size")]
        private void ResetColliderSize()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                Debug.LogWarning("[VictoryDoor] No BoxCollider found on this GameObject. " +
                                 "Add one and run this again.", this);
                return;
            }

            box.center = Vector3.zero;
            box.size   = new Vector3(3f, 5f, 2f);   // Volumen interactivo visible y transitable.

            EditorUtility.SetDirty(this);
            Debug.Log("[VictoryDoor] BoxCollider reset to (3, 5, 2). " +
                      "Adjust size in the Inspector to fit your door prefab.", this);
        }

        private void OnDrawGizmos()
        {
            // Dibujar un cubo de alambre dorado para hacer la puerta visible en la vista de Escena.
            Gizmos.color = new Color(1f, 0.84f, 0f, 0.7f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            // Pequeño icono sobre la puerta.
            Handles.Label(
                transform.position + Vector3.up * 1.5f,
                $"[VictoryDoor]\n" +
                $"Key: {(_requiredKey != null ? _requiredKey.DisplayName : "NOT SET")}\n" +
                $"{(IsUnlocked ? "UNLOCKED" : "LOCKED")}");
        }
#endif
    }
}
