// =============================================================================
//  PlayerController3D.cs
//  Author  : [Your Name]
//  Project : TopDownShooter – Protagonist: Lunaria (Mage)
//  Created : 2026
//
//  PURPOSE
//  -------
//  Handles all first-person locomotion for the player character in absolute
//  world-space (camera-independent WASD), smooth mouse-aim rotation via a
//  ground-plane raycast, manual gravity, and basic jumping.
//
//  ARCHITECTURE NOTES
//  ------------------
//  • Strictly follows Single Responsibility Principle – Update() is a clean
//    dispatcher that calls focused private methods only.
//  • All tunable values are [SerializeField] private – zero public state.
//  • Component references are cached once in Awake() and never polled again.
//  • Designed as a leaf node ready to be managed by an external FSM:
//      - Expose a public bool IsGrounded property for state queries.
//      - Movement & rotation can be individually enabled/disabled via the
//        CanMove / CanRotate flags (useful during spell-cast lock-out, etc.).
//  • Input is received via Unity New Input System "Send Messages" behaviour
//    on the PlayerInput component:  OnMove, OnJump, OnLook.
//
//  FUTURE INTEGRATION HOOKS (comments marked ► FSM / ► SO / ► ICombat)
//  -----------------------------------------------------------------------
//  ► FSM   : Finite State Machine can read IsGrounded, IsMoving, IsSprinting.
//  ► SO    : Replace [SerializeField] speed/jump/gravity fields with a
//            reference to a PlayerStatsSO ScriptableObject asset.
//  ► ICombat : The OnAttack callback (stubbed at bottom) calls into an
//              ICombatHandler interface injected at runtime.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Drives Lunaria's movement, aiming rotation, gravity, and jump
    /// using Unity's CharacterController and New Input System.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerController3D : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR-EXPOSED PARAMETERS  (private + [SerializeField])
        // ─────────────────────────────────────────────────────────────────────

        [Header("Movement")]
        [Tooltip("Base horizontal movement speed in units per second.")]
        [SerializeField] private float moveSpeed = 6f;

        [Tooltip("Multiplier applied on top of moveSpeed when sprinting.")]
        [SerializeField] private float sprintMultiplier = 1.65f;

        [Header("Jump & Gravity")]
        [Tooltip("Initial vertical velocity when the player jumps.")]
        [SerializeField] private float jumpForce = 7f;

        [Tooltip("Gravity magnitude applied each second while airborne. " +
                 "Use a positive value; it is negated internally.")]
        [SerializeField] private float gravity = 20f;

        [Tooltip("Small downward force applied when grounded to keep the " +
                 "CharacterController firmly pressed against the floor.")]
        [SerializeField] private float groundStickForce = 2f;

        [Header("Rotation / Aiming")]
        [Tooltip("Speed at which the character rotates to face the mouse " +
                 "cursor. Higher = snappier, lower = smoother.")]
        [SerializeField] private float rotationSpeed = 15f;

        [Tooltip("Height of the virtual aim plane above world origin. " +
                 "Set to Lunaria's hip/waist height for best visual results.")]
        [SerializeField] private float aimPlaneHeight = 0f;

        [Header("Flags – Runtime Control")]
        [Tooltip("Disable to lock all horizontal movement (e.g. during cutscenes).")]
        [SerializeField] private bool canMove = true;

        [Tooltip("Disable to lock aim-rotation (e.g. during root-motion attacks).")]
        [SerializeField] private bool canRotate = true;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE  (never serialised, never public)
        // ─────────────────────────────────────────────────────────────────────

        // Component references – cached in Awake()
        private CharacterController _characterController;
        private Camera              _mainCamera;
        private Transform           _transform;

        // Raw input values written by New Input System callbacks
        private Vector2 _rawMoveInput;
        private Vector2 _rawMouseScreenPosition;
        private bool    _jumpRequested;
        private bool    _sprintHeld;

        // Physics state
        private Vector3 _verticalVelocity;   // Only Y component is used
        private Plane   _aimPlane;           // Mathematical ground plane for raycasting

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC READ-ONLY PROPERTIES  (for FSM / animation layer queries)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>True when the CharacterController reports ground contact.</summary>
        public bool IsGrounded  => _characterController.isGrounded;

        /// <summary>True when there is non-zero movement input this frame.</summary>
        public bool IsMoving    => _rawMoveInput.sqrMagnitude > 0.01f;

        /// <summary>True when the sprint modifier is held.</summary>
        public bool IsSprinting => _sprintHeld && IsMoving;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            CacheComponents();
            InitialiseAimPlane();
        }

        /// <summary>
        /// Clean dispatcher – each frame routes work to single-purpose helpers.
        /// No logic lives here directly.
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
        /// Caches all required component references once at startup.
        /// Logs a fatal error and disables the script if anything is missing,
        /// preventing obscure NullReferenceExceptions later at runtime.
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

            // Camera.main uses a tag-lookup; cache it once to avoid O(n) searches.
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("[PlayerController3D] No Camera tagged 'MainCamera' found in the scene. " +
                               "Please tag your camera (or Cinemachine brain camera) as 'MainCamera'.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// Creates the mathematical Plane used for mouse-aim raycasting.
        /// The plane is flat (normal = up) at the configured aim height.
        /// Call this again at runtime if aimPlaneHeight changes dynamically.
        /// </summary>
        private void InitialiseAimPlane()
        {
            // Plane(normal, distance from origin along normal)
            _aimPlane = new Plane(Vector3.up, new Vector3(0f, aimPlaneHeight, 0f));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MOVEMENT
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Translates the raw 2-axis input into absolute world-space (XZ) motion.
        ///
        /// WHY ABSOLUTE WORLD SPACE?
        /// In a top-down shooter the camera is fixed overhead, so the player
        /// always expects W = north (world +Z), S = south, A = west, D = east,
        /// completely ignoring which direction Lunaria's model is facing.
        /// This is the standard Twin-Stick / ARPG movement convention.
        /// </summary>
        private void MovePlayer()
        {
            if (!canMove) return;

            // Map Vector2 (X,Y) from keyboard/gamepad → world (X,Z) axes
            // _rawMoveInput.x = strafe (A/D), _rawMoveInput.y = forward (W/S)
            Vector3 worldMoveDirection = new Vector3(_rawMoveInput.x, 0f, _rawMoveInput.y);

            // Clamp to magnitude 1 so diagonal movement isn't faster (the
            // New Input System Dpad composite normalises automatically, but
            // we guard here for safety with analogue sticks).
            if (worldMoveDirection.sqrMagnitude > 1f)
                worldMoveDirection.Normalize();

            // ► SO : Replace moveSpeed with playerStats.MoveSpeed
            float currentSpeed = IsSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

            // Combine horizontal and vertical velocity into a single Move() call
            // so CharacterController handles collision correctly.
            Vector3 horizontalVelocity = worldMoveDirection * currentSpeed;
            Vector3 totalVelocity      = horizontalVelocity + _verticalVelocity;

            _characterController.Move(totalVelocity * Time.deltaTime);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PHYSICS – GRAVITY & JUMP
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Accumulates gravity over time and applies an initial upward impulse
        /// when a jump has been requested.  Uses manual integration rather than
        /// Rigidbody physics so we keep full deterministic control over feel.
        /// </summary>
        private void ApplyGravityAndJump()
        {
            if (IsGrounded)
            {
                // Snap to a small negative value so isGrounded stays true
                // on the next frame even on slightly uneven terrain.
                _verticalVelocity.y = -groundStickForce;

                if (_jumpRequested)
                {
                    // ► SO : Replace jumpForce with playerStats.JumpForce
                    _verticalVelocity.y = jumpForce;
                }
            }
            else
            {
                // Apply gravity (positive gravity field, negated here)
                // ► SO : Replace gravity with playerStats.Gravity
                _verticalVelocity.y -= gravity * Time.deltaTime;
            }

            // Always consume the jump request, even if airborne (no double-jump)
            _jumpRequested = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ROTATION / AIMING
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Rotates Lunaria to face her mouse cursor by intersecting a ray from
        /// the camera through the screen position onto the virtual ground plane.
        ///
        /// ALGORITHM
        /// ─────────
        /// 1. Build a Ray from the camera through the mouse's screen pixel.
        /// 2. Find where that ray hits _aimPlane (a flat horizontal Plane).
        /// 3. Calculate the direction from Lunaria's feet to the hit point.
        /// 4. Zero out the Y component (prevents tilting up/down).
        /// 5. Slerp the current rotation toward the target to smooth the aim.
        /// </summary>
        private void RotateTowardsMouse()
        {
            if (!canRotate) return;

            // Build the screen-space ray.  _rawMouseScreenPosition holds the
            // raw pixel position reported by the Input System.
            Ray screenRay = _mainCamera.ScreenPointToRay(_rawMouseScreenPosition);

            // Intersect the ray with our mathematical ground plane.
            // Raycast returns true if the ray is not parallel to the plane.
            if (!_aimPlane.Raycast(screenRay, out float hitDistance)) return;

            // World-space point where the cursor "lands" on the ground plane
            Vector3 aimWorldPoint = screenRay.GetPoint(hitDistance);

            // Direction from the character's position to the aim point
            Vector3 lookDirection = aimWorldPoint - _transform.position;

            // CRITICAL: Isolate the horizontal plane – set Y = 0 so Lunaria
            // never tilts her body up or down when the mouse is near her feet.
            lookDirection.y = 0f;

            // Degenerate guard: if the cursor is directly over the character
            // the direction vector is near-zero – skip to avoid NaN rotations.
            if (lookDirection.sqrMagnitude < 0.001f) return;

            // Build the target rotation from the look direction
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

            // ► SO : Replace rotationSpeed with playerStats.RotationSpeed
            // Slerp gives smooth interpolation along the shortest arc of the
            // unit sphere, avoiding "spinning the long way around" artefacts.
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
        /// Receives the Move action (Vector2) from the Player action map.
        /// Action name in the Input Asset must be exactly "Move".
        /// </summary>
        private void OnMove(InputValue value)
        {
            _rawMoveInput = value.Get<Vector2>();
        }

        /// <summary>
        /// Receives the Look action (Vector2) from the Player action map.
        /// For Keyboard+Mouse this should be bound to &lt;Mouse&gt;/position
        /// (absolute screen position) – NOT the delta.
        /// For gamepad, right stick is converted to a pseudo-screen position
        /// via the gamepad aim helper (see setup guide).
        ///
        /// IMPORTANT: The Look action binding MUST use &lt;Mouse&gt;/position
        /// (absolute), NOT &lt;Pointer&gt;/delta (relative movement). The
        /// ScreenPointToRay call requires absolute screen coordinates.
        /// </summary>
        private void OnLook(InputValue value)
        {
            _rawMouseScreenPosition = value.Get<Vector2>();
        }

        /// <summary>
        /// Receives the Jump action (Button) from the Player action map.
        /// Sets a one-frame flag consumed by ApplyGravityAndJump().
        /// </summary>
        private void OnJump(InputValue value)
        {
            if (value.isPressed)
                _jumpRequested = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  STUB CALLBACKS  (ready for future systems)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// ► ICombat : Forward the attack event to the injected combat handler.
        /// Bind &lt;Mouse&gt;/leftButton and &lt;Gamepad&gt;/buttonWest in the
        /// Input Asset to the "Attack" action in the Player action map.
        /// </summary>
        private void OnAttack(InputValue value)
        {
            if (!value.isPressed) return;
            // ► ICombat : _combatHandler?.PerformAttack();
            // Example: GetComponent<ICombatHandler>()?.PerformAttack();
        }

        /// <summary>
        /// ► FSM : Notify the state machine that a sprint was requested.
        /// Bind &lt;Keyboard&gt;/leftShift to the "Sprint" action in the Input Asset.
        /// </summary>
        private void OnSprint(InputValue value)
        {
            _sprintHeld = value.isPressed;
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR-ONLY VISUALISATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draws the aim plane as a coloured grid in the Scene view for easy
        /// debugging – only compiled in the Unity Editor.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // Draw the aim plane as a semi-transparent disc
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Vector3 planeOrigin = new Vector3(
                transform.position.x,
                aimPlaneHeight,
                transform.position.z
            );
            Gizmos.DrawSphere(planeOrigin, 0.1f);

            // Draw forward direction (current facing)
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);

            // Draw move direction
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
