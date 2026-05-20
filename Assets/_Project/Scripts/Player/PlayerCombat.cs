// =============================================================================
//  PlayerCombat.cs
//  Author  : [Your Name]
//  Project : TopDownShooter – Protagonist: Lunaria (Mage)
//  Created : 2026
//
//  PURPOSE
//  -------
//  Decoupled combat input handler for the player character (Lunaria).
//  Acts as the "Context" in the Strategy Pattern: it holds a reference to
//  the currently equipped IWeapon and delegates all attack logic to it.
//
//  STRATEGY PATTERN ROLE: Context
//  ────────────────────────────────
//  • PlayerCombat ONLY knows about the IWeapon interface contract.
//  • It has zero knowledge of MagicWand, projectiles, pools, or fire rates.
//  • Switching weapons at runtime = a single SetWeapon() call. No rewiring.
//
//  DECOUPLING FROM PlayerController3D
//  ────────────────────────────────────
//  • PlayerController3D (locomotion) and PlayerCombat (combat input) both
//    live on the Lunaria GameObject.
//  • PlayerInput (Send Messages) broadcasts OnAttack to ALL MonoBehaviours
//    on the same GameObject. PlayerController3D has an OnAttack stub that
//    does nothing — PlayerCombat owns the real implementation.
//  • This keeps Single Responsibility Principle intact: movement never knows
//    about attacking, and attacking never knows about movement.
//
//  INPUT SYSTEM NOTES
//  ──────────────────
//  • Behaviour: "Send Messages" on the PlayerInput component.
//  • The method must be named exactly: OnAttack(InputValue value)
//  • The matching action in the Input Asset must be named exactly: "Attack"
//    (case-sensitive, in the "Player" action map).
//
//  FUTURE HOOKS
//  ► FSM  : CanAttack property gates the attack (e.g. during cast animations).
//  ► SO   : Inject a WeaponInventorySO to swap weapons from loadout data.
//  ► UI   : Expose CurrentWeapon for HUD weapon icon / cooldown display.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace TopDownShooter.Combat
{
    /// <summary>
    /// Strategy Context: receives attack input and delegates to the active
    /// <see cref="IWeapon"/>. Attach this alongside <c>PlayerController3D</c>
    /// on the Lunaria GameObject.
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR-EXPOSED PARAMETERS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Weapon Setup")]
        [Tooltip("The MonoBehaviour component that implements IWeapon. " +
                 "Drag the MagicWand component (or any IWeapon) here. " +
                 "This is the weapon Lunaria starts the game with.")]
        [SerializeField] private MonoBehaviour initialWeapon;

        [Header("Flags – Runtime Control")]
        [Tooltip("Set to false to prevent all attack input (e.g. in menus, cutscenes).")]
        [SerializeField] private bool canAttack = true;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // The currently equipped weapon. Stored as IWeapon — no concrete type.
        private IWeapon _equippedWeapon;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC READ-ONLY PROPERTIES  (for FSM / HUD / achievement queries)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns the currently equipped weapon strategy.</summary>
        public IWeapon CurrentWeapon => _equippedWeapon;

        /// <summary>True when attack input is globally permitted.</summary>
        public bool CanAttack
        {
            get => canAttack;
            set => canAttack = value;   // FSM can set this to false during cast animations
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            InitialiseWeapon();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INITIALISATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates and sets the initial weapon from the Inspector reference.
        /// Logs clear errors rather than crashing with a NullReferenceException.
        /// </summary>
        private void InitialiseWeapon()
        {
            if (initialWeapon == null)
            {
                Debug.LogError("[PlayerCombat] 'Initial Weapon' is not assigned in the Inspector. " +
                               "Drag a MagicWand (or any IWeapon MonoBehaviour) into the field.", this);
                return;
            }

            // Attempt to cast the MonoBehaviour reference to IWeapon.
            // Using MonoBehaviour in the Inspector field gives us a drag-and-drop
            // UX while still enforcing the interface contract at runtime.
            if (initialWeapon is IWeapon weapon)
            {
                _equippedWeapon = weapon;
            }
            else
            {
                Debug.LogError($"[PlayerCombat] The assigned 'Initial Weapon' ({initialWeapon.name}) " +
                               $"does not implement IWeapon. Attach a MagicWand or another IWeapon component.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Hot-swaps the equipped weapon at runtime.
        ///
        /// USAGE EXAMPLE:
        /// <code>
        /// playerCombat.SetWeapon(newStaffComponent);  // Swap to a new weapon
        /// playerCombat.SetWeapon(null);               // Unequip (holster)
        /// </code>
        ///
        /// ► SO : Could also accept a WeaponDataSO and instantiate the matching
        ///        MonoBehaviour prefab from a WeaponFactory.
        /// </summary>
        public void SetWeapon(IWeapon newWeapon)
        {
            _equippedWeapon = newWeapon;
            Debug.Log($"[PlayerCombat] Weapon swapped to: {newWeapon?.GetType().Name ?? "None"}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  NEW INPUT SYSTEM – MESSAGE CALLBACK
        //  (Called automatically by PlayerInput in "Send Messages" mode)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Receives the Attack action (Button) from the Player action map.
        ///
        /// METHOD NAMING CONTRACT:
        /// The method name "OnAttack" must exactly match the Input Action name
        /// "Attack" (PlayerInput prepends "On" and calls the method via reflection).
        /// The action must be of type Button in the Input Asset.
        ///
        /// FLOW:
        /// [Mouse Left Button] → PlayerInput (Send Messages) → OnAttack()
        ///    → CanAttack gate → _equippedWeapon.ExecuteAttack()
        ///    → MagicWand.ExecuteAttack() → fire-rate gate → pool.Get()
        ///    → Projectile launches toward mouse target
        /// </summary>
        public void OnAttack(InputValue value)
        {
            // Only react to the press event, not the release.
            if (!value.isPressed) return;

            // Global gate — FSM or other systems can disable attacks externally.
            if (!canAttack) return;

            // Null-guard: if no weapon is equipped, silently skip.
            if (_equippedWeapon == null)
            {
                Debug.LogWarning("[PlayerCombat] OnAttack called but no weapon is equipped.", this);
                return;
            }

            // Delegate entirely to the strategy — PlayerCombat doesn't know HOW.
            _equippedWeapon.ExecuteAttack();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  NOTE ON OnAttack IN PlayerController3D
        // ─────────────────────────────────────────────────────────────────────
        // PlayerController3D also has an OnAttack stub. Since both scripts live
        // on the same GameObject, PlayerInput (Send Messages) will call OnAttack
        // on BOTH components. The stub in PlayerController3D does nothing (early
        // return), so there is no conflict. You may also remove the stub from
        // PlayerController3D entirely to keep it perfectly clean — it is safe
        // to have OnAttack handled exclusively here in PlayerCombat.
    }
}
