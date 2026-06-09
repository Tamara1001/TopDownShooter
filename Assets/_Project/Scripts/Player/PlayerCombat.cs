// =============================================================================
//  PlayerCombat.cs
//  Author  : [Your Name]
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Decoupled combat input handler for the player character.
//  Acts as the "Context" in the Strategy Pattern: it holds a reference to
//  the currently equipped IWeapon and delegates all attack logic to it.
//
//  PART 2 — INVENTORY INTEGRATION
//  ────────────────────────────────
//  The hardcoded "initialWeapon" field is gone. The player now starts
//  empty-handed. When PlayerInventory fires OnWeaponChanged, PlayerCombat:
//    1. Destroys the old weapon logic child GameObject (cleans up its pool).
//    2. Instantiates WeaponDataSO.WeaponLogicPrefab as a child of this Player.
//    3. Casts to IWeapon and stores as _equippedWeapon.
//    4. If the instance also implements IWeaponConfigurable, calls Configure()
//       so the weapon reads its stats (fire rate, damage, etc.) from the SO.
//
//  PART 4 — RESOURCE GATE
//  ────────────────────────
//  PlayerCombat now caches the current WeaponDataSO and reads its
//  ResourceType/ResourceCost before each attack. If TryConsumeMana or
//  TryConsumeEnergy returns false, the attack is aborted silently.
//  The resource math lives entirely in PlayerResourceComponent — this
//  script only calls the gate and reads the result.
//
//  STRATEGY PATTERN ROLE: Context
//  ────────────────────────────────
//  • PlayerCombat ONLY knows about IWeapon and IWeaponConfigurable.
//  • It has zero knowledge of MagicWand, projectiles, pools, or fire rates.
//  • Switching weapons at runtime = OnWeaponChanged event fires → HandleWeaponChanged().
//
//  CLEAN LIFECYCLE
//  ────────────────
//  • OnDestroy unsubscribes from OnWeaponChanged (no stale delegates).
//  • The live weapon child is destroyed when swapped, so its pool is disposed
//    via MagicWand.OnDestroy — no leaked pool instances.
//
//  INPUT SYSTEM NOTES
//  ──────────────────
//  • Behaviour: "Send Messages" on the PlayerInput component.
//  • The method must be named exactly: OnAttack(InputValue value)
//  • The matching action in the Input Asset must be named exactly: "Attack".
//
//  FUTURE HOOKS
//  ► FSM  : CanAttack property gates the attack (e.g. during cast animations).
//  ► Audio: Trigger a "no weapon" SFX in OnAttack when _equippedWeapon == null.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using TopDownShooter.Inventory;

namespace TopDownShooter.Combat
{
    /// <summary>
    /// Strategy Context: receives attack input and delegates to the active
    /// <see cref="IWeapon"/>. Subscribes to <see cref="Player.PlayerInventory.OnWeaponChanged"/>
    /// and dynamically instantiates the correct weapon logic child at runtime.
    /// Attach this alongside <c>PlayerController3D</c> and <c>PlayerInventory</c>
    /// on the Player root GameObject.
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR-EXPOSED PARAMETERS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Flags – Runtime Control")]
        [Tooltip("Set to false to prevent all attack input (e.g. in menus, cutscenes).")]
        [SerializeField] private bool canAttack = true;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // The currently equipped weapon strategy — IWeapon only; no concrete type.
        private IWeapon _equippedWeapon;

        // The live child GameObject that owns the weapon MonoBehaviour.
        // Kept so we can Destroy it cleanly when swapping (triggers OnDestroy
        // on the weapon, which disposes its ObjectPool).
        private GameObject _liveWeaponObject;

        // The SO blueprint of the currently equipped weapon.
        // Cached here (not re-read from inventory each frame) so the resource
        // gate in OnAttack has O(1) access with no GetComponent overhead.
        private WeaponDataSO _currentWeaponData;

        // Cached reference to the PlayerInventory on the same GameObject.
        private Player.PlayerInventory _playerInventory;

        // Cached reference to the resource manager — used to gate attacks.
        // Acquired once in Awake; null = no resource system present (free attacks).
        private Player.PlayerResourceComponent _resourceComponent;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC READ-ONLY PROPERTIES  (for FSM / HUD / achievement queries)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns the currently equipped weapon strategy. Null = empty-handed.</summary>
        public IWeapon CurrentWeapon => _equippedWeapon;

        /// <summary>True when attack input is globally permitted.</summary>
        public bool CanAttack
        {
            get => canAttack;
            set => canAttack = value;   // FSM can disable attacks during animations
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            SubscribeToInventory();

            // Optional dependency — if absent, all weapons fire for free.
            if (!TryGetComponent(out _resourceComponent))
            {
                Debug.LogWarning("[PlayerCombat] No PlayerResourceComponent found on this " +
                                 "GameObject. Weapons will fire without resource cost.", this);
            }
        }

        private void OnDestroy()
        {
            // Always unsubscribe to prevent stale delegate calls after this
            // component is destroyed (e.g. scene unload, player death).
            if (_playerInventory != null)
                _playerInventory.OnWeaponChanged -= HandleWeaponChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INITIALISATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the <see cref="Player.PlayerInventory"/> on this GameObject
        /// and subscribes to <see cref="Player.PlayerInventory.OnWeaponChanged"/>.
        /// Logs a clear error if the component is missing rather than crashing later.
        /// </summary>
        private void SubscribeToInventory()
        {
            if (!TryGetComponent(out _playerInventory))
            {
                Debug.LogError("[PlayerCombat] No PlayerInventory found on this GameObject. " +
                               "Attach PlayerInventory to the same root as PlayerCombat. " +
                               "Attack input will be silently ignored until resolved.", this);
                return;
            }

            _playerInventory.OnWeaponChanged += HandleWeaponChanged;
            Debug.Log("[PlayerCombat] Subscribed to PlayerInventory.OnWeaponChanged. " +
                      "Player starts empty-handed.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  WEAPON SWAP HANDLER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Subscriber to <see cref="Player.PlayerInventory.OnWeaponChanged"/>.
        /// Destroys the old weapon logic child, then instantiates and configures
        /// the new one from the <see cref="WeaponDataSO"/>.
        ///
        /// <para>
        /// ALGORITHM:
        /// <list type="number">
        ///   <item>Tear down: destroy old child → its OnDestroy disposes the pool.</item>
        ///   <item>If newWeapon is null (slot cleared), stop here — player is empty-handed.</item>
        ///   <item>Guard: verify WeaponLogicPrefab is assigned on the SO.</item>
        ///   <item>Instantiate the logic prefab as a child of this Player transform.</item>
        ///   <item>Cast to IWeapon. Log error and clean up if the cast fails.</item>
        ///   <item>Optional: if also IWeaponConfigurable, call Configure(newWeapon).</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="newWeapon">
        /// The <see cref="WeaponDataSO"/> of the newly picked-up weapon,
        /// or <c>null</c> if the weapon slot was cleared.
        /// </param>
        private void HandleWeaponChanged(WeaponDataSO newWeapon)
        {
            // Cache the SO so OnAttack can read resource costs without any
            // GetComponent call. Must be updated BEFORE TearDown clears the old one.
            _currentWeaponData = newWeapon;

            // ── Step 1: Tear down the old weapon ───────────────────────────
            TearDownCurrentWeapon();

            // ── Step 2: Null check — player is now empty-handed ────────────
            if (newWeapon == null)
            {
                Debug.Log("[PlayerCombat] Weapon slot cleared. Player is empty-handed.");
                return;
            }

            // ── Step 3: Validate the SO's logic prefab reference ───────────
            if (newWeapon.WeaponLogicPrefab == null)
            {
                Debug.LogError($"[PlayerCombat] WeaponDataSO '{newWeapon.DisplayName}' has no " +
                               "WeaponLogicPrefab assigned. Cannot equip this weapon. " +
                               "Assign an IWeapon MonoBehaviour prefab in the SO.", this);
                return;
            }

            // ── Step 4: Instantiate as a child of this Player ──────────────
            // Spawning as a child means the weapon inherits the player's
            // world-space position and rotation automatically, so fire-point
            // Transforms stay correct without any manual syncing.
            _liveWeaponObject = Instantiate(
                newWeapon.WeaponLogicPrefab.gameObject,
                transform.position,
                transform.rotation,
                transform);   // ← parent = this Player's transform

            // ── Step 5: Cast the root MonoBehaviour to IWeapon ────────────
            // GetComponent<IWeapon>() finds the first IWeapon on the root or
            // any child. We use the root MonoBehaviour type for the cast since
            // WeaponLogicPrefab is guaranteed to be on the root.
            _equippedWeapon = _liveWeaponObject.GetComponent<IWeapon>();

            if (_equippedWeapon == null)
            {
                Debug.LogError($"[PlayerCombat] The instantiated prefab for '{newWeapon.DisplayName}' " +
                               "does not have an IWeapon component. " +
                               "Ensure the prefab's root script implements IWeapon.", this);

                // Clean up the orphaned child to avoid a dangling GameObject.
                Destroy(_liveWeaponObject);
                _liveWeaponObject = null;
                return;
            }

            // ── Step 6: Optional stat injection via IWeaponConfigurable ────
            // This is the ONLY place where the SO data flows into the logic.
            // The weapon reads what it needs; PlayerCombat stays data-agnostic.
            if (_equippedWeapon is IWeaponConfigurable configurable)
            {
                configurable.Configure(newWeapon);
                Debug.Log($"[PlayerCombat] Configured '{newWeapon.DisplayName}' via IWeaponConfigurable.");
            }
            else
            {
                Debug.Log($"[PlayerCombat] '{newWeapon.DisplayName}' does not implement " +
                          "IWeaponConfigurable — using its hardcoded Inspector values.");
            }

            Debug.Log($"[PlayerCombat] Weapon equipped: '{newWeapon.DisplayName}' " +
                      $"({_equippedWeapon.GetType().Name}).");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TEARDOWN HELPER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears the current weapon state and destroys the live child GameObject.
        /// Destroying the child triggers <c>MagicWand.OnDestroy</c> (or any
        /// weapon's OnDestroy), which disposes the ObjectPool cleanly.
        /// </summary>
        private void TearDownCurrentWeapon()
        {
            if (_liveWeaponObject != null)
            {
                string oldName = _equippedWeapon?.GetType().Name ?? "Unknown";
                Destroy(_liveWeaponObject);
                _liveWeaponObject = null;
                Debug.Log($"[PlayerCombat] Destroyed old weapon logic: '{oldName}'.");
            }

            _equippedWeapon = null;
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

            // Global gate — FSM or cutscene systems can disable attacks externally.
            if (!canAttack) return;

            // Graceful no-op when empty-handed.
            if (_equippedWeapon == null) return;

            // ── Resource Gate (Part 4) ────────────────────────────────────────
            // Read the cost from the cached SO and attempt to spend it.
            // If the component is missing we treat all weapons as free.
            if (_resourceComponent != null && _currentWeaponData != null)
            {
                switch (_currentWeaponData.ResourceType)
                {
                    case WeaponResourceType.Mana:
                        if (!_resourceComponent.TryConsumeMana(_currentWeaponData.ResourceCost))
                        {
                            Debug.Log("[PlayerCombat] Not enough Mana to attack. " +
                                      $"Required: {_currentWeaponData.ResourceCost}.");
                            return;   // Abort — do NOT fire
                        }
                        break;

                    case WeaponResourceType.Energy:
                        if (!_resourceComponent.TryConsumeEnergy(_currentWeaponData.ResourceCost))
                        {
                            Debug.Log("[PlayerCombat] Not enough Energy to attack. " +
                                      $"Required: {_currentWeaponData.ResourceCost}.");
                            return;   // Abort — do NOT fire
                        }
                        break;

                    // WeaponResourceType.None — falls through; no cost, no gate.
                }
            }

            // Delegate entirely to the strategy — PlayerCombat doesn't know HOW.
            _equippedWeapon.ExecuteAttack();
        }
    }
}
