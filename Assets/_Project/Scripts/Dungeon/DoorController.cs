
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownShooter.Dungeon
{
    /// <summary>
    /// Controla la traslación vertical y la alternancia de colisión de una puerta.
    /// Se abre hundiendo las partes visuales hijas debajo del suelo; se cierra elevándolas
    /// de vuelta a sus posiciones originales. Utiliza materiales 100% opacos —
    /// no requiere transparencia alfa.
    /// </summary>
    public sealed class DoorController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  NESTED TYPES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Almacena en caché el Transform de una parte visual y su posición local
        /// original (cerrada) para que la animación siempre pueda regresar exactamente
        /// al lugar correcto independientemente de la profundidad de la jerarquía del prefab.
        /// </summary>
        private class VisualPartCache
        {
            /// <summary>El Transform hijo a mover.</summary>
            public Transform Part;

            /// <summary>
            /// La localPosition registrada en Awake — este es el estado CERRADO.
            /// </summary>
            public Vector3 ClosedLocalPosition;

            /// <summary>
            /// Derivado de ClosedLocalPosition + Vector3.down * sinkDistance.
            /// Este es el estado ABIERTO (hundido).
            /// </summary>
            public Vector3 OpenLocalPosition;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("Animation")]
        [Tooltip("Duración de la traslación de apertura/cierre en segundos.")]
        [SerializeField] private float _slideDuration = 0.4f;

        [Tooltip("Distancia (unidades del mundo) que las partes visuales se hunden por debajo de su " +
                 "posición de reposo cuando la puerta está abierta. " +
                 "Debe ser al menos tan alta como la malla de su puerta.")]
        [SerializeField] private float _sinkDistance = 5f;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO PRIVADO
        // ─────────────────────────────────────────────────────────────────────

        // Colisionadores físicos — alternados en CloseDoor / OpenDoor.
        private Collider[] _colliders;

        // Partes visuales que se trasladarán durante la animación.
        private List<VisualPartCache> _visualParts = new List<VisualPartCache>();

        // Corrutina de deslizamiento activa — detenida antes de lanzar una nueva para que
        // llamar a OpenDoor() a mitad del cierre (o viceversa) nunca cause conflictos.
        private Coroutine _slideCoroutine;

        // Componente de bloqueo cacheado — consultado una vez en Awake, costo cero por frame.
        // Cualquier MonoBehaviour hermano que implemente IDoorLock puede vetar OpenDoor().
        private IDoorLock _lock;

        // ─────────────────────────────────────────────────────────────────────
        //  CICLO DE VIDA DE UNITY
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Cachear cualquier hermano IDoorLock — por ejemplo, LockedBossDoor en la misma raíz del prefab.
            // GetComponent es O(1) and only runs once, so the veto costs nothing at runtime.
            _lock = GetComponent<IDoorLock>();

            // Descubrir todas las barreras físicas — comprobación isTrigger aplicada en SetCollidersState.
            _colliders = GetComponentsInChildren<Collider>();

            // ── Descubrir partes visuales ──────────────────────────────────────────
            // Movemos solo los Transforms que poseen un Renderer, evitando el desplazamiento
            // accidental de sockets, nodos de aparición u otros hijos no visuales.
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                Transform t = r.transform;
                Vector3 closed = t.localPosition;

                _visualParts.Add(new VisualPartCache
                {
                    Part               = t,
                    ClosedLocalPosition = closed,
                    OpenLocalPosition  = closed + Vector3.down * _sinkDistance
                });
            }

            // ── Inicialización consciente del bloqueo ─────────────────────────────
            // Puerta bloqueada → comenzar en posición CERRADA (elevada, sólida, bloqueando).
            // Puerta desbloqueada → comenzar en posición ABIERTA (hundida, transitable) — este es
            //                       el valor predeterminado para las puertas de combate/pasillos que solo se cierran
            //                       cuando el jugador entra en la sala.
            if (_lock != null && _lock.IsLocked)
            {
                SetToPosition(closed: true);
                SetCollidersState(true);
            }
            else
            {
                SetToPosition(closed: false);
                SetCollidersState(false);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Eleva la puerta a su posición cerrada y habilita la colisión física.
        /// Es seguro llamarlo mientras la puerta ya se está cerrando o a mitad de la animación —
        /// la corrutina anterior se detiene y se inicia una nueva desde la posición trasladada actual.
        /// </summary>
        public void CloseDoor()
        {
            SetCollidersState(true);

            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlideRoutine(closingDoor: true));
        }

        /// <summary>
        /// Hunde la puerta debajo del suelo y deshabilita la colisión física.
        /// Vetado silenciosamente cuando un hermano <see cref="IDoorLock"/> reporta
        /// <c>IsLocked == true</c> — por ejemplo, cuando RoomController.ClearRoom()
        /// transmite la orden a todas las puertas pero la del jefe todavía está bloqueada con llave.
        /// </summary>
        public void OpenDoor()
        {
            // ── Veto de bloqueo ──────────────────────────────────────────────
            // Un LockedBossDoor (o cualquier IDoorLock) en este mismo GameObject puede
            // bloquear esta llamada hasta que el jugador use la llave mediante Interact().
            if (_lock != null && _lock.IsLocked)
            {
                Debug.Log($"[DoorController] OpenDoor() bloqueado por el cerrojo en '{gameObject.name}'. " +
                          "Esperando a que el jugador use la llave requerida.", this);
                return;
            }

            SetCollidersState(false);

            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlideRoutine(closingDoor: false));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SLIDE ANIMATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Traslada suavemente cada parte visual entre sus posiciones locales abiertas y cerradas
        /// durante <see cref="_slideDuration"/> segundos.
        /// Utiliza una atenuación smoothstep (Mathf.SmoothStep) para una sensación satisfactoria.
        /// </summary>
        /// <param name="closingDoor">
        /// <c>true</c>  → anima hacia ClosedLocalPosition (elevar).<br/>
        /// <c>false</c> → anima hacia OpenLocalPosition   (hundir).
        /// </param>
        private IEnumerator SlideRoutine(bool closingDoor)
        {
            if (_visualParts.Count == 0)
            {
                _slideCoroutine = null;
                yield break;
            }

            // Sample the current position from the first part so that
            // interrupting a half-finished animation starts from where it is,
            // not from a hard-coded endpoint.
            Vector3 startPos = _visualParts[0].Part.localPosition;

            float elapsed = 0f;

            while (elapsed < _slideDuration)
            {
                elapsed += Time.deltaTime;

                // Clamp t to [0,1] — last delta may overshoot _slideDuration.
                float t = Mathf.Clamp01(elapsed / _slideDuration);

                // Smoothstep: fast start/end, slower in the middle — feels
                // mechanical without requiring an AnimationCurve asset.
                float smooth = Mathf.SmoothStep(0f, 1f, t);

                for (int i = 0; i < _visualParts.Count; i++)
                {
                    VisualPartCache part = _visualParts[i];
                    if (part.Part == null) continue;

                    // Recompute per-part start on first frame using the
                    // cached closed/open endpoints for parts after index 0.
                    Vector3 from = (i == 0)
                        ? startPos
                        : (closingDoor ? part.OpenLocalPosition : part.ClosedLocalPosition);

                    Vector3 to = closingDoor
                        ? part.ClosedLocalPosition
                        : part.OpenLocalPosition;

                    part.Part.localPosition = Vector3.LerpUnclamped(from, to, smooth);
                }

                yield return null;
            }

            // Ajustar al objetivo exacto — elimina cualquier desviación de punto flotante.
            for (int i = 0; i < _visualParts.Count; i++)
            {
                if (_visualParts[i].Part == null) continue;

                _visualParts[i].Part.localPosition = closingDoor
                    ? _visualParts[i].ClosedLocalPosition
                    : _visualParts[i].OpenLocalPosition;
            }

            _slideCoroutine = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UTILIDAD DE POSICIÓN
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ajusta instantáneamente todas las partes visuales a la posición cerrada o abierta
        /// sin animación. Usado durante la inicialización de Awake().
        /// </summary>
        private void SetToPosition(bool closed)
        {
            for (int i = 0; i < _visualParts.Count; i++)
            {
                VisualPartCache part = _visualParts[i];
                if (part.Part == null) continue;

                part.Part.localPosition = closed
                    ? part.ClosedLocalPosition
                    : part.OpenLocalPosition;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UTILIDAD DE COLISIÓN
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Habilita o deshabilita cada <see cref="Collider"/> que no sea disparador (non-trigger) en
        /// la jerarquía de la puerta. Los colisionadores isTrigger se omiten intencionadamente —
        /// se utilizan para la detección de IWorldInteractable (VictoryDoor, LockedBossDoor)
        /// y la activación de sala en RoomController.OnTriggerEnter.
        /// Deshabilitarlos rompería silenciosamente ambos sistemas.
        /// </summary>
        private void SetCollidersState(bool state)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null && !_colliders[i].isTrigger)
                {
                    _colliders[i].enabled = state;
                }
            }
        }
    }
}
