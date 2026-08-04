

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Controla el movimiento, la rotación de apuntado, la gravedad y el salto de Lunaria
    /// utilizando el CharacterController de Unity y el Nuevo Sistema de Entradas.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerController3D : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  PARÁMETROS EXPUESTOS EN EL INSPECTOR  (private + [SerializeField])
        // ─────────────────────────────────────────────────────────────────────

        [Header("Movement")]
        [Tooltip("Velocidad de movimiento horizontal base en unidades por segundo.")]
        [SerializeField] private float moveSpeed = 6f;

        [Tooltip("Multiplicador aplicado sobre moveSpeed al correr.")]
        [SerializeField] private float sprintMultiplier = 1.65f;

        [Header("Dash")]
        [Tooltip("Costo de energía deducido del PlayerResourceComponent en cada intento de dash. Si no hay suficiente energía disponible, el dash se rechaza silenciosamente.")]
        [SerializeField] private int   _dashCost     = 20;

        [Tooltip("Velocidad horizontal en unidades por segundo aplicada durante la ventana de dash.")]
        [SerializeField] private float _dashSpeed    = 18f;

        [Tooltip("Duración en segundos que el desvío de velocidad del dash permanece activo.")]
        [Min(0.05f)]
        [SerializeField] private float _dashDuration = 0.2f;

        [Header("Jump & Gravity")]
        [Tooltip("Velocidad vertical inicial cuando el jugador salta.")]
        [SerializeField] private float jumpForce = 7f;

        [Tooltip("Magnitud de la gravedad aplicada cada segundo mientras está en el aire. Use un valor positivo; se niega internamente.")]
        [SerializeField] private float gravity = 20f;

        [Tooltip("Pequeña fuerza hacia abajo aplicada al estar en el suelo para mantener el CharacterController firmemente presionado contra el piso.")]
        [SerializeField] private float groundStickForce = 2f;

        [Header("Rotation / Aiming")]
        [Tooltip("Velocidad a la que el personaje gira para mirar el cursor del ratón. Mayor = más rápido/directo, menor = más suave.")]
        [SerializeField] private float rotationSpeed = 15f;

        [Tooltip("Altura del plano de apuntado virtual por encima del origen del mundo. Establézcalo a la altura de la cadera/cintura de Lunaria para obtener los mejores resultados visuales.")]
        [SerializeField] private float aimPlaneHeight = 0f;

        [Header("Flags – Runtime Control")]
        [Tooltip("Desactivar para bloquear todo el movimiento horizontal (por ejemplo, durante cinemáticas).")]
        [SerializeField] private bool canMove = true;

        [Tooltip("Desactivar para bloquear la rotación de apuntado (por ejemplo, durante ataques con movimiento de raíz).")]
        [SerializeField] private bool canRotate = true;

        [Header("VFX")]
        [Tooltip("Sistema de partículas reproducido al inicio de cada dash exitoso. Dejar sin asignar para omitir (seguro contra nulos).")]
        [SerializeField] private ParticleSystem _dashDustParticles;

        [Tooltip("TrailRenderer activado/desactivado durante la ventana de dash para un efecto de estela fantasma. Dejar sin asignar para omitir (seguro contra nulos).")]
        [SerializeField] private TrailRenderer _dashTrail;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO PRIVADO  (never serialised, never public)
        // ─────────────────────────────────────────────────────────────────────

        // Referencias a componentes – almacenadas en caché en Awake()
        private CharacterController _characterController;
        private Camera              _mainCamera;
        private Transform           _transform;

        // Componente de recursos opcional — si está ausente, el dash es gratuito (comportamiento alternativo).
        private PlayerResourceComponent _resourceComponent;

        // Componente de estadísticas opcional — si está ausente, los multiplicadores de velocidad son 1 por defecto.
        private PlayerStatsComponent _statsComponent;

        // Valores de entrada brutos escritos por las devoluciones de llamada del Nuevo Sistema de Entradas
        private Vector2 _rawMoveInput;
        private Vector2 _rawMouseScreenPosition;
        private bool    _jumpRequested;
        private bool    _sprintHeld;

        // Estado de dash — verdadero por exactamente _dashDuration segundos por dash
        private bool _isDashing = false;

        // Estado de físicas
        private Vector3 _verticalVelocity;   // Solo se utiliza el componente Y
        private Plane   _aimPlane;           // Plano de suelo matemático para raycasting

        // ─────────────────────────────────────────────────────────────────────
        //  STATIC EVENTS  (subscribed by HUD / other UI without a direct reference)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Se activa cuando un intento de dash es rechazado debido a Energía insuficiente.
        /// Estático para que el HUD pueda suscribirse sin mantener una referencia a este componente.
        /// </summary>
        public static event Action OnEnergyDepleted;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC READ-ONLY PROPERTIES  (for FSM / animation layer queries)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Verdadero cuando el CharacterController informa contacto con el suelo.</summary>
        public bool IsGrounded  => _characterController.isGrounded;

        /// <summary>Verdadero cuando hay una entrada de movimiento distinta de cero en este frame.</summary>
        public bool IsMoving    => _rawMoveInput.sqrMagnitude > 0.01f;

        /// <summary>Verdadero cuando se mantiene presionado el modificador de correr.</summary>
        public bool IsSprinting => _sprintHeld && IsMoving;

        /// <summary>Verdadero mientras hay un dash activo en curso.</summary>
        public bool IsDashing   => _isDashing;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            CacheComponents();
            InitialiseAimPlane();
        }

        /// <summary>
        /// Despachador limpio – cada frame enruta el trabajo a ayudantes de un solo propósito.
        /// Ninguna lógica vive directamente aquí.
        /// </summary>
        private void Update()
        {
            ApplyGravityAndJump();
            MovePlayer();
            RotateTowardsMouse();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INITIALISATION HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Guarda en caché todas las referencias de componentes requeridas una vez al inicio.
        /// Registra un error fatal y deshabilita el script si falta algo,
        /// evitando excepciones NullReference poco claras más adelante en tiempo de ejecución.
        /// </summary>
        private void CacheComponents()
        {
            _transform = transform;

            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
            {
                Debug.LogError($"[PlayerController3D] CharacterController missing on '{name}'. " +
                               "The script will be disabled.", this);
                enabled = false;
                return;
            }

            // Camera.main utiliza una búsqueda de etiquetas; la guardamos en caché una vez para evitar búsquedas O(n).
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("[PlayerController3D] No Camera tagged 'MainCamera' found in the scene. " +
                               "Please tag your camera (or Cinemachine brain camera) as 'MainCamera'.", this);
                enabled = false;
                return;
            }

            // Opcional — el dash es gratuito si no está presente (degradación suave).
            if (!TryGetComponent(out _resourceComponent))
            {
                Debug.LogWarning("[PlayerController3D] No PlayerResourceComponent found. " +
                                 "Dash will work without any Energy cost.", this);
            }

            // Opcional — los multiplicadores de velocidad vuelven a 1 por defecto si no está presente.
            TryGetComponent(out _statsComponent);
        }

        /// <summary>
        /// Crea el plano matemático utilizado para el raycasting de apuntado del ratón.
        /// El plano es plano (normal = arriba) a la altura de apuntado configurada.
        /// Llame a esto nuevamente en tiempo de ejecución si aimPlaneHeight cambia dinámicamente.
        /// </summary>
        private void InitialiseAimPlane()
        {
            // Plane(normal, distancia desde el origen a lo largo de la normal)
            _aimPlane = new Plane(Vector3.up, new Vector3(0f, aimPlaneHeight, 0f));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MOVEMENT
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Traduce la entrada bruta de 2 ejes en movimiento absoluto en el espacio del mundo (XZ).
        ///
        /// ¿POR QUÉ EL ESPACIO DEL MUNDO ABSOLUTO?
        /// En un shooter top-down, la cámara está fija en la parte superior, por lo que el jugador
        /// siempre espera W = norte (+Z del mundo), S = sur, A = oeste, D = este,
        /// ignorando por completo la dirección en la que está orientado el modelo de Lunaria.
        /// Esta es la convención de movimiento estándar de Twin-Stick / ARPG.
        /// </summary>
        private void MovePlayer()
        {
            if (!canMove) return;

            // ── ANULACIÓN POR DASH ─────────────────────────────────────────────
            // Durante el dash, ignora toda entrada normal y empuja al jugador a lo largo de transform.forward.
            // Dado que RotateTowardsMouse() ya garantiza que forward = la dirección en la que apunta el jugador,
            // el dash viaja exactamente hacia el cursor del ratón. La velocidad vertical se conserva para que
            // el salto y el dash puedan combinarse de forma natural.
            if (_isDashing)
            {
                Vector3 dashVelocity = _transform.forward * _dashSpeed;
                _characterController.Move((dashVelocity + _verticalVelocity) * Time.deltaTime);
                return;
            }

            // ── MOVIMIENTO NORMAL ─────────────────────────────────────────────
            // Mapear Vector2 (X,Y) de teclado/gamepad → ejes del mundo (X,Z)
            // _rawMoveInput.x = desplazamiento lateral (A/D), _rawMoveInput.y = avanzar (W/S)
            Vector3 worldMoveDirection = new Vector3(_rawMoveInput.x, 0f, _rawMoveInput.y);

            // Limitar a magnitud 1 para que el movimiento diagonal no sea más rápido (el
            // compuesto Dpad del Nuevo Sistema de Entradas se normaliza automáticamente, pero
            // nos protegemos aquí por seguridad con los sticks analógicos).
            if (worldMoveDirection.sqrMagnitude > 1f)
                worldMoveDirection.Normalize();

            // Aplicar el multiplicador de velocidad de movimiento de reliquias de PlayerStatsComponent.
            // Vuelve a 1 suavemente si el componente no está presente.
            float relicMultiplier = _statsComponent != null ? _statsComponent.MoveSpeedMultiplier : 1f;
            float currentSpeed    = (IsSprinting ? moveSpeed * sprintMultiplier : moveSpeed) * relicMultiplier;

            // Combinar velocidad horizontal y vertical en una sola llamada a Move()
            // para que CharacterController maneje la colisión correctamente.
            Vector3 horizontalVelocity = worldMoveDirection * currentSpeed;
            Vector3 totalVelocity      = horizontalVelocity + _verticalVelocity;

            _characterController.Move(totalVelocity * Time.deltaTime);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PHYSICS – GRAVITY & JUMP
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Acumula gravedad a lo largo del tiempo y aplica un impulso inicial hacia arriba
        /// cuando se ha solicitado un salto. Utiliza integración manual en lugar de
        /// físicas de Rigidbody para mantener el control determinista total sobre la sensación de juego.
        /// </summary>
        private void ApplyGravityAndJump()
        {
            if (IsGrounded)
            {
                // Ajustar a un pequeño valor negativo para que isGrounded siga siendo verdadero
                // en el próximo frame, incluso en terrenos ligeramente irregulares.
                _verticalVelocity.y = -groundStickForce;

                if (_jumpRequested)
                {
                    // ► SO : Reemplazar jumpForce con playerStats.JumpForce
                    _verticalVelocity.y = jumpForce;
                }
            }
            else
            {
                // Aplicar gravedad (campo de gravedad positivo, negado aquí)
                // ► SO : Reemplazar gravity con playerStats.Gravity
                _verticalVelocity.y -= gravity * Time.deltaTime;
            }

            // Consumir siempre la solicitud de salto, incluso si está en el aire (sin doble salto)
            _jumpRequested = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ROTATION / AIMING
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Rota a Lunaria para que mire a su cursor del ratón intersectando un rayo desde
        /// la cámara a través de la posición de la pantalla sobre el plano de suelo virtual.
        ///
        /// ALGORITMO
        /// ─────────
        /// 1. Construir un Ray desde la cámara a través del píxel de la pantalla del ratón.
        /// 2. Encontrar dónde golpea ese rayo a _aimPlane (un plano horizontal plano).
        /// 3. Calcular la dirección desde los pies de Lunaria hasta el punto de impacto.
        /// 4. Poner a cero el componente Y (evita inclinar hacia arriba/abajo).
        /// 5. Slerp la rotación actual hacia el objetivo para suavizar el apuntado.
        /// </summary>
        private void RotateTowardsMouse()
        {
            if (!canRotate) return;

            // Construir el rayo en espacio de pantalla. _rawMouseScreenPosition contiene la
            // posición de píxel bruta informada por el Sistema de Entradas.
            Ray screenRay = _mainCamera.ScreenPointToRay(_rawMouseScreenPosition);

            // Intersectar el rayo con nuestro plano de suelo matemático.
            // Raycast devuelve verdadero si el rayo no es paralelo al plano.
            if (!_aimPlane.Raycast(screenRay, out float hitDistance)) return;

            // Punto en el espacio del mundo donde el cursor "aterriza" en el plano del suelo
            Vector3 aimWorldPoint = screenRay.GetPoint(hitDistance);

            // Dirección desde la posición del personaje hasta el punto de apuntado
            Vector3 lookDirection = aimWorldPoint - _transform.position;

            // CRÍTICO: Aislar el plano horizontal – establecer Y = 0 para que Lunaria
            // nunca incline su cuerpo hacia arriba o hacia abajo cuando el ratón esté cerca de sus pies.
            lookDirection.y = 0f;

            // Guardia degenerada: si el cursor está directamente sobre el personaje
            // la dirección vector es casi cero – omitir para evitar rotaciones NaN.
            if (lookDirection.sqrMagnitude < 0.001f) return;

            // Construir la rotación objetivo a partir de la dirección de la mirada
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

            // ► SO : Reemplazar rotationSpeed con playerStats.RotationSpeed
            // Slerp proporciona una interpolación suave a lo largo del arco más corto de la
            // esfera unitaria, evitando artefactos de "girar por el camino largo".
            _transform.rotation = Quaternion.Slerp(
                _transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // ─────────────────────────────────────────────────────────────────────
        //  NEW INPUT SYSTEM – MESSAGE CALLBACKS
        //  (Called automatically by PlayerInput in "Send Messages" mode)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Recibe la acción Move (Vector2) del mapa de acciones Player.
        /// El nombre de la acción en el Asset de Input debe ser exactamente "Move".
        /// </summary>
        private void OnMove(InputValue value)
        {
            _rawMoveInput = value.Get<Vector2>();
        }

        /// <summary>
        /// Recibe la acción Look (Vector2) del mapa de acciones Player.
        /// Para Teclado+Ratón esto debe estar vinculado a &lt;Mouse&gt;/position
        /// (posición absoluta de pantalla) – NO a la diferencia delta.
        /// Para gamepad, el stick derecho se convierte a una posición de pantalla simulada
        /// a través del ayudante de apuntado de gamepad (ver guía de configuración).
        ///
        /// IMPORTANTE: El binding de la acción Look DEBE usar &lt;Mouse&gt;/position
        /// (absoluta), NOT &lt;Pointer&gt;/delta (movimiento relativo). La
        /// llamada ScreenPointToRay requiere coordenadas de pantalla absolutas.
        /// </summary>
        private void OnLook(InputValue value)
        {
            _rawMouseScreenPosition = value.Get<Vector2>();
        }

        /// <summary>
        /// Recibe la acción Jump (Botón) del mapa de acciones Player.
        /// Establece una bandera de un solo frame consumida por ApplyGravityAndJump().
        /// </summary>
        private void OnJump(InputValue value)
        {
            if (value.isPressed)
                _jumpRequested = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DEVUELTAS DE LLAMADA VACÍAS (STUBS)  (listas para futuros sistemas)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Código vacío intencional — la entrada de ataque es manejada por <see cref="TopDownShooter.Combat.PlayerCombat"/>.
        ///
        /// PlayerInput (Send Messages) transmite OnAttack a TODOS los MonoBehaviours en
        /// este GameObject. PlayerCombat.OnAttack() contiene la lógica real.
        /// Este código vacío evita que Unity registre una advertencia de "Método no encontrado"
        /// mientras mantiene la Responsabilidad Única de este script (solo locomoción).
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnAttack(InputValue value) { /* Intentional no-op. See PlayerCombat.cs */ }

        /// <summary>
        /// Código vacío intencional — la lógica de recogida/intercambio es manejada por <see cref="TopDownShooter.Player.PlayerInventory"/>.
        /// PlayerInput (Send Messages) transmite OnInteract a TODOS los MonoBehaviours
        /// en este GameObject. Este código vacío suprime la advertencia de "Método no encontrado".
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnInteract(InputValue value) { /* Operación nula intencional. Ver PlayerInventory.cs */ }

        /// <summary>
        /// Código vacío intencional — la lógica de uso de consumibles es manejada por <see cref="TopDownShooter.Player.PlayerInventory"/>.
        /// PlayerInput (Send Messages) transmite OnConsume a TODOS los MonoBehaviours
        /// en este GameObject. Este código vacío suprime la advertencia de "Método no encontrado".
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnConsume(InputValue value) { /* Operación nula intencional. Ver PlayerInventory.cs */ }

        /// <summary>
        /// Recibe el botón sostenido Sprint (Botón) del mapa de acciones Player.
        /// </summary>
        private void OnSprint(InputValue value)
        {
            _sprintHeld = value.isPressed;
        }

        /// <summary>
        /// Recibe la acción Dash (Botón) del mapa de acciones Player.
        /// Se vincula a &lt;Keyboard&gt;/space en el Asset de Input (acción "Dash").
        ///
        /// FLUJO:
        /// [Espacio] → PlayerInput (Send Messages) → OnDash()
        ///   → Guardia: no estar ya haciendo dash
        ///   → TryConsumeEnergy(_dashCost) → si es verdadero: StartCoroutine(DashRoutine)
        ///
        /// El dash viaja a lo largo de transform.forward, que ya apunta al
        /// cursor del ratón gracias a que RotateTowardsMouse() se ejecuta en cada frame.
        /// </summary>
        public void OnDash(InputValue value)
        {
            if (!value.isPressed) return;
            if (_isDashing)     return;   // No dash chaining while one is active
            if (!canMove)       return;   // Respect the movement lock flag

            // Compuerta de recursos — omitir la verificación de costos por completo si el componente no está presente.
            if (_resourceComponent != null &&
                !_resourceComponent.TryConsumeEnergy(_dashCost))
            {
                Debug.Log($"[PlayerController3D] Not enough Energy to dash. " +
                          $"Required: {_dashCost}.");
                OnEnergyDepleted?.Invoke();
                return;
            }

            // Reproducir VFX de inicio de dash (seguro contra nulos — seguro si no se asigna en el Inspector).
            _dashDustParticles?.Play();

            StartCoroutine(DashRoutine());
        }

        /// <summary>
        /// Activa la anulación de la velocidad de dash por exactamente <see cref="_dashDuration"/> segundos.
        /// Utiliza <c>WaitForSeconds</c> (tiempo a escala) para que el dash respete
        /// Time.timeScale — pausar el juego pausa el dash a mitad de camino de forma natural.
        /// </summary>
        private IEnumerator DashRoutine()
        {
            _isDashing = true;

            // Habilitar la estela durante la duración del dash.
            if (_dashTrail != null) _dashTrail.emitting = true;

            yield return new WaitForSeconds(_dashDuration);

            // Deshabilitar la estela tan pronto como se reanude el movimiento normal.
            if (_dashTrail != null) _dashTrail.emitting = false;

            _isDashing = false;
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        //  VISUALIZACIÓN SOLO EN EDITOR
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Dibuja el plano de apuntado como una cuadrícula coloreada en la vista de Scene para facilitar
        /// la depuración – solo se compila en el Editor de Unity.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // Dibuja el plano de apuntado como un disco semitransparente
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Vector3 planeOrigin = new Vector3(
                transform.position.x,
                aimPlaneHeight,
                transform.position.z
            );
            Gizmos.DrawSphere(planeOrigin, 0.1f);

            // Dibuja la dirección forward (orientación actual)
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);

            // Dibuja la dirección de movimiento
            if (Application.isPlaying && _rawMoveInput.sqrMagnitude > 0.01f)
            {
                Gizmos.color = Color.yellow;
                Vector3 moveDir = new Vector3(_rawMoveInput.x, 0f, _rawMoveInput.y).normalized;
                Gizmos.DrawRay(transform.position, moveDir * 2f);
            }
        }
#endif
    }
}
