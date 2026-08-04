


using System.Collections;
using UnityEngine;
using TopDownShooter.Inventory;
using TopDownShooter.Player;
using TopDownShooter.Interaction;
using TopDownShooter.Dungeon;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TopDownShooter.World
{
    [RequireComponent(typeof(Collider))]
    public class LockedBossDoor : MonoBehaviour, IWorldInteractable, IDoorLock
    {
        [Header("Lock Configuration")]
        [Tooltip("El key (ScriptableObject) requerido en la ranura de consumible del jugador para desbloquear esta puerta.")]
        [SerializeField] private ConsumableDataSO _requiredKey;
        
        [Tooltip("El controlador visual y físico de la puerta sobre el cual actuar al desbloquearse.")]
        [SerializeField] private DoorController _doorController;

        [Header("Visual Feedback")]
        [Tooltip("Color HDR pulsado en la emisión de la puerta cuando el jugador está en el rango.")]
        [SerializeField] private Color _approachGlowColor = new Color(1.2f, 0.8f, 0f, 1f);  // amber HDR

        [Tooltip("Color HDR parpadeado brevemente cuando el jugador intenta abrir sin la llave.")]
        [SerializeField] private Color _denyFlashColor = new Color(2.5f, 0.1f, 0f, 1f);     // red HDR

        [Tooltip("Duración (segundos) del ciclo de parpadeo de denegación.")]
        [SerializeField] [Range(0.1f, 1f)] private float _flashDuration = 0.35f;

        // ── Implementación de IDoorLock ────────────────────────────────────────
        // DoorController consulta esto a través de la interfaz para vetar OpenDoor().
        // IsLocked es verdadero hasta que el jugador usa la llave correcta.
        public bool IsLocked => !IsUnlocked;

        // Expuesto para que los sistemas externos (ej. herramientas de depuración) puedan leer el estado.
        public bool IsUnlocked { get; private set; } = false;

        // ── Caché de Renderer para efectos de emisión ────────────────────────
        // Almacenado en caché una vez en Awake — cero llamadas a GetComponent durante el juego.
        // MaterialPropertyBlock nos permite teñir por renderer sin crear
        // nuevas instancias de Material, manteniendo la base de datos de assets limpia.
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private Coroutine _flashCoroutine;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            // Cache all renderers in this hierarchy once.
            // Used by both OnPlayerApproach (glow) and the deny flash.
            _renderers = GetComponentsInChildren<Renderer>();
            _propBlock = new MaterialPropertyBlock();

            // Ensure emission keyword is enabled on every material so the
            // property block colour actually shows at runtime.
            foreach (Renderer r in _renderers)
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m != null)
                        m.EnableKeyword("_EMISSION");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PROXIMITY FEEDBACK
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Llame a esto cuando el jugador entre en el radio de interacción de esta puerta.
        /// Enciende una sutil emisión ámbar para indicar "este objeto es interactivo".
        /// Conectar desde InteractionDebugger, un ProximityTrigger o
        /// PlayerInventory.TryWorldInteract (antes de la llamada a Interact()).
        /// </summary>
        public void OnPlayerApproach()
        {
            if (IsUnlocked) return;
            SetEmission(_approachGlowColor);
        }

        /// <summary>
        /// Llame a esto cuando el jugador salga del radio de interacción.
        /// Apaga el resplandor de aproximación (a menos que se esté ejecutando un parpadeo de denegación).
        /// </summary>
        public void OnPlayerLeave()
        {
            if (_flashCoroutine != null) return;   // Dejar que el parpadeo termine primero.
            SetEmission(Color.black);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IWorldInteractable
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Llamado por PlayerInventory cuando el jugador presiona la entrada Interact.
        /// </summary>
        public void Interact(PlayerInventory inventory)
        {
            // Cláusulas de salvaguarda
            if (IsUnlocked || _doorController == null) return;

            if (_requiredKey == null)
            {
                Debug.LogError("[LockedBossDoor] No key assigned in the inspector!", this);
                return;
            }

            // Evaluar si el jugador sostiene la llave requerida
            if (inventory != null && inventory.CurrentConsumable == _requiredKey)
            {
                Debug.Log("[LockedBossDoor] Key accepted! Unlocking boss door.");
                IsUnlocked = true;  // IDoorLock.IsLocked se vuelve falso — DoorController.OpenDoor() desbloqueado.

                // Apagar cualquier parpadeo activo antes de abrir — la puerta va a desaparecer.
                StopDenyFlash();
                SetEmission(Color.black);

                _doorController.OpenDoor();
                
                // Deshabilitar este script (y opcionalmente su colisionador si es estrictamente para interacción)
                // para que el prompt nunca vuelva a aparecer.
                this.enabled = false;
                
                Collider col = GetComponent<Collider>();
                if (col != null && col.isTrigger)
                {
                    col.enabled = false;
                }
            }
            else
            {
                string held = inventory?.CurrentConsumable?.DisplayName ?? "None";
                Debug.Log($"[LockedBossDoor] Locked. Requires '{_requiredKey.DisplayName}', " +
                          $"but player holds '{held}'.");

                // ── Parpadeo de denegación ───────────────────────────────────
                // Pulsar brevemente la puerta en rojo para dar un feedback visual claro de que
                // la interacción fue rechazada. El parpadeo restaura la emisión
                // a su estado inactivo (negro) al terminar.
                StopDenyFlash();
                _flashCoroutine = StartCoroutine(FlashDenyColor());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  AYUDANTES DE FEEDBACK VISUAL
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Desvanece la emisión a <see cref="_denyFlashColor"/>, se sostiene brevemente,
        /// luego la desvanece de regreso a negro. Un solo pulso — limpio y legible.
        /// </summary>
        private IEnumerator FlashDenyColor()
        {
            float half = _flashDuration * 0.5f;

            // Rampa ascendente hacia el color de denegación
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                SetEmission(Color.Lerp(Color.black, _denyFlashColor, t / half));
                yield return null;
            }

            SetEmission(_denyFlashColor);

            // Rampa descendente de regreso a negro
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                SetEmission(Color.Lerp(_denyFlashColor, Color.black, t / half));
                yield return null;
            }

            SetEmission(Color.black);
            _flashCoroutine = null;
        }

        /// <summary>
        /// Detiene cualquier corrutina de parpadeo de denegación en ejecución de inmediato.
        /// Llamado antes de comenzar un nuevo parpadeo (evita que dos se ejecuten a la vez)
        /// y cuando la puerta se desbloquea (limpia sin esperar a que termine).
        /// </summary>
        private void StopDenyFlash()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }
        }

        /// <summary>
        /// Aplica el <paramref name="color"/> a la propiedad <c>_EmissionColor</c>
        /// de cada Renderer almacenado en caché a través de un MaterialPropertyBlock.
        /// El uso de un bloque de propiedades evita instanciar nuevos objetos Material,
        /// manteniendo limpia la base de datos de assets y la memoria.
        /// </summary>
        private void SetEmission(Color color)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColorID, color);
                _renderers[i].SetPropertyBlock(_propBlock);
            }
        }

        // ----------------------------------------------------------
        // UTILIDADES DE EDITOR
        // ----------------------------------------------------------

        /// <summary>
        /// Corrección de un clic para un BoxCollider que está enterrado dentro de la malla de la puerta
        /// y, por lo tanto, es invisible para el OverlapSphere de PlayerInventory.
        /// Ejecutar a través de clic derecho → "Reset Collider Size" en el Inspector.
        /// Ajuste el tamaño para que coincida con su prefab de puerta después de ejecutar esto.
        /// </summary>
        [ContextMenu("Reset Collider Size")]
        private void ResetColliderSize()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                Debug.LogWarning("[LockedBossDoor] No BoxCollider found on this GameObject. " +
                                 "Add one and run this again.", this);
                return;
            }

            box.center = Vector3.zero;
            box.size   = new Vector3(3f, 5f, 2f);   // Volumen interactivo visible y transitable.

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            Debug.Log("[LockedBossDoor] BoxCollider reset to (3, 5, 2). " +
                      "Adjust size in the Inspector to fit your door prefab.", this);
#endif
        }

        // ----------------------------------------------------------
        // GIZMOS DE EDITOR
        // ----------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Dibujar un cubo de alambre rojo en la posición de la cerradura
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(2f, 2f, 0.5f));
            
            // Dibujar una etiqueta por encima
            Handles.Label(transform.position + Vector3.up * 2.5f,
                $"[Locked Boss Door]\n{(IsUnlocked ? "UNLOCKED" : "LOCKED")}");
        }
#endif
    }
}
