// =============================================================================
//  InteractionDebugger.cs
//  Project : TopDownShooter
//
//  !! SCRIPT DE DIAGNÓSTICO TEMPORAL — ELIMINAR ANTES DE LANZAR !!
//  ───────────────────────────────────────────────────────────
//  Adjunte esto al GameObject Player junto con PlayerInventory.
//  Cada <_logInterval> segundos ejecuta la misma OverlapSphere que
//  PlayerInventory.TryWorldInteract() usa y registra lo que encuentra.
//
//  CÓMO LEER LA SALIDA
//  ───────────────────
//  Caso 1 — "No interactables found in range"
//    La OverlapSphere no golpeó nada en _interactableLayerMask.
//    Subcaso A: "All-layer scan found: VictoryDoor (layer: Default)"
//      → La puerta existe pero está en la capa INCORRECTA. Cámbiela a 'Interactable'.
//    Subcaso B: "All-layer scan found nothing either"
//      → La puerta está fuera de alcance O no tiene ningún colisionador.
//
//  Caso 2 — "Found interactable: VictoryDoor (layer: Interactable)"
//    La puerta se detecta correctamente. Si la tecla E sigue sin hacer nada, el error
//    está dentro de VictoryDoor.Interact() — verifique los registros de la consola de [VictoryDoor].
//
//  CONFIGURACIÓN
//  ─────────────
//  • _interactableLayerMask debe coincidir con el valor establecido en PlayerInventory.
//  • _detectionRadius debe coincidir con PlayerInventory._pickupRadius (por defecto 1.5).
// =============================================================================

using UnityEngine;
using TopDownShooter.Interaction;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Ayudante de diagnóstico temporal. Ejecuta una OverlapSphere periódica idéntica a
    /// <see cref="PlayerInventory.TryWorldInteract"/> y registra cada
    /// <see cref="IWorldInteractable"/> que encuentra (o advierte cuando no encuentra ninguno).
    /// Adjuntar al GameObject Player; desactivar o eliminar antes de lanzar.
    /// </summary>
    public sealed class InteractionDebugger : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Espejar estos valores desde PlayerInventory")]
        [Tooltip("Debe coincidir con PlayerInventory._pickupRadius (por defecto: 1.5). " +
                 "La OverlapSphere no detectará una puerta que esté fuera de este radio.")]
        [SerializeField] private float _detectionRadius = 1.5f;

        [Tooltip("Debe coincidir con PlayerInventory._interactableLayerMask. " +
                 "Si esta máscara está vacía, PlayerInventory nunca detectará nada.")]
        [SerializeField] private LayerMask _interactableLayerMask;

        [Header("Regulador")]
        [Tooltip("Segundos entre cada escaneo de diagnóstico. " +
                 "Mantener en 0.5 o más alto para evitar inundar la Consola.")]
        [SerializeField] [Range(0.1f, 5f)] private float _logInterval = 0.5f;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        private float _nextLogTime = 0f;

        // Buffer preasignado — reutilizado en cada escaneo, cero asignaciones de GC.
        private readonly Collider[] _buffer = new Collider[16];

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            // Advertir de inmediato si la máscara no está configurada — este es el fallo silencioso número 1.
            if (_interactableLayerMask.value == 0)
            {
                Debug.LogWarning("[InteractionDebugger] _interactableLayerMask is EMPTY (Nothing). " +
                                 "The scan will never find any interactables. " +
                                 "Set it to the same LayerMask as PlayerInventory._interactableLayerMask.", this);
            }

            Debug.Log($"[InteractionDebugger] Attached to '{gameObject.name}'. " +
                      $"Scanning radius: {_detectionRadius}m every {_logInterval}s. " +
                      $"LayerMask value: {_interactableLayerMask.value} " +
                      $"({LayerMaskToString(_interactableLayerMask)}).", this);
        }

        private void Update()
        {
            if (Time.time < _nextLogTime) return;
            _nextLogTime = Time.time + _logInterval;

            RunDiagnosticScan();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DIAGNOSTIC LOGIC
        // ─────────────────────────────────────────────────────────────────────

        private void RunDiagnosticScan()
        {
            Vector3 origin = transform.position;

            // ── Paso 1: Escaneo enmascarado (replica a PlayerInventory exactamente) ─────
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                _detectionRadius,
                _buffer,
                _interactableLayerMask);

            bool foundAny = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _buffer[i];
                if (col == null) continue;

                // Espejar PlayerInventory: solo importan los objetos con IWorldInteractable.
                if (col.TryGetComponent<IWorldInteractable>(out _))
                {
                    Debug.Log($"[InteractionDebugger] Found interactable: '{col.gameObject.name}' " +
                              $"on layer '{LayerMask.LayerToName(col.gameObject.layer)}' " +
                              $"| Distance: {Vector3.Distance(origin, col.transform.position):F2}m.", this);
                    foundAny = true;
                }
                else
                {
                    // El colisionador está en la capa correcta pero le falta IWorldInteractable —
                    // este es otro error común de configuración.
                    Debug.LogWarning($"[InteractionDebugger] Collider '{col.gameObject.name}' " +
                                     $"is on layer '{LayerMask.LayerToName(col.gameObject.layer)}' " +
                                     "but has NO IWorldInteractable component. " +
                                     "Add VictoryDoor / LockedBossDoor to this GameObject.", this);
                }
            }

            // Limpiar el buffer para liberar referencias de objetos obsoletas.
            System.Array.Clear(_buffer, 0, hitCount);

            if (foundAny) return;

            // ── Paso 2: Alternativa para todas las capas — revela una mala configuración de capa ──
            // Si el Paso 1 no encontró nada, escanear TODAS las capas para ver si el objeto
            // está simplemente en la capa incorrecta.
            int allHitCount = Physics.OverlapSphereNonAlloc(
                origin,
                _detectionRadius,
                _buffer,
                ~0);                          // ~0 = every layer

            bool foundOnWrongLayer = false;

            for (int i = 0; i < allHitCount; i++)
            {
                Collider col = _buffer[i];
                if (col == null) continue;
                if (col.gameObject == gameObject) continue;   // Omitir al propio Player.

                if (col.TryGetComponent<IWorldInteractable>(out _))
                {
                    Debug.LogWarning($"[InteractionDebugger] All-layer scan found: '{col.gameObject.name}' " +
                                     $"(layer: '{LayerMask.LayerToName(col.gameObject.layer)}'). " +
                                     "This object has IWorldInteractable but is NOT on the " +
                                     "_interactableLayerMask. Change its layer in the Inspector " +
                                     "to match PlayerInventory._interactableLayerMask.", this);
                    foundOnWrongLayer = true;
                }
            }

            System.Array.Clear(_buffer, 0, allHitCount);

            if (!foundOnWrongLayer)
            {
                Debug.LogWarning($"[InteractionDebugger] No interactables found in range " +
                                 $"(radius: {_detectionRadius}m). " +
                                 "Either the door is too far away, has no Collider, " +
                                 "or has no IWorldInteractable component.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve una lista legible para humanos de los nombres de capas incluidos en la máscara.
        /// Útil para verificar que la máscara esté configurada correctamente sin abrir el Inspector.
        /// </summary>
        private static string LayerMaskToString(LayerMask mask)
        {
            if (mask.value == 0) return "Nothing";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                if ((mask.value & (1 << i)) != 0)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(layerName);
                    }
                }
            }
            return sb.Length > 0 ? sb.ToString() : $"Unknown mask {mask.value}";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR GIZMOS
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Relleno semitransparente — muestra la burbuja de detección exacta.
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.08f);
            Gizmos.DrawSphere(transform.position, _detectionRadius);

            // Anillo de alambre sólido — fácil de juzgar la distancia de un vistazo.
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_detectionRadius + 0.2f),
                $"[Debugger] r={_detectionRadius}m");
        }
#endif
    }
}
