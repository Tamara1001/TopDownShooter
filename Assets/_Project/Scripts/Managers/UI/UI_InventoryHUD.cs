// =============================================================================
//  UI_InventoryHUD.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Presenter / listener that bridges PlayerInventory events to the three
//  UI_InventorySlot visual components. It holds no game logic — it only
//  routes incoming ItemDataSO references to the correct slot's UpdateSlot().
//
//  ARCHITECTURE
//  ─────────────
//  Observer Pattern (two tiers):
//    Tier 1 — GameManager.OnPlayerRegistered:
//      Fired whenever a new player Transform is registered. This allows the
//      HUD to bind correctly whether the player exists before or after the UI
//      initialises, and handles respawn / scene-reload scenarios.
//
//    Tier 2 — PlayerInventory.OnWeaponChanged / OnRelicChanged / OnConsumableChanged:
//      Fine-grained slot events. Subscribed in BindToPlayer() and safely
//      unsubscribed before re-binding or on destroy.
//
//  LIFECYCLE
//  ─────────
//    OnEnable  → subscribe to GameManager.OnPlayerRegistered
//    Start     → attempt immediate bind if player is already registered
//    OnDisable → unsubscribe from GameManager.OnPlayerRegistered
//    OnDestroy → unsubscribe from any bound PlayerInventory (prevents callbacks
//                on a destroyed UI object)
//
//  ATTACH TO
//  ─────────
//  The parent container GameObject that holds the three slot child objects.
// =============================================================================

using UnityEngine;
using TopDownShooter.Inventory;
using TopDownShooter.Player;

namespace TopDownShooter.UI
{
    /// <summary>
    /// Listens to <see cref="PlayerInventory"/> slot events and routes
    /// the updated <see cref="ItemDataSO"/> to the matching <see cref="UI_InventorySlot"/>.
    /// Handles dynamic player registration so the HUD works regardless of
    /// scene load order or player respawn.
    /// </summary>
    public sealed class UI_InventoryHUD : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Inventory Slots")]
        [Tooltip("Slot component for the Weapon inventory slot.")]
        [SerializeField] private UI_InventorySlot _weaponSlot;

        [Tooltip("Slot component for the Relic inventory slot.")]
        [SerializeField] private UI_InventorySlot _relicSlot;

        [Tooltip("Slot component for the Consumable inventory slot.")]
        [SerializeField] private UI_InventorySlot _consumableSlot;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE RUNTIME STATE
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The currently bound inventory. Cached so it can be safely
        /// unsubscribed before rebinding on respawn or scene reload.
        /// </summary>
        private PlayerInventory _boundInventory;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Tier-1 subscription: know the instant a new player Transform exists.
            GameManager.OnPlayerRegistered += OnPlayerRegistered;
        }

        private void Start()
        {
            ValidateSlots();

            // If the player was registered before this UI was enabled (e.g. player
            // Awake runs before the HUD's Start), bind immediately without waiting
            // for the OnPlayerRegistered event.
            if (GameManager.Instance != null && GameManager.Instance.PlayerTransform != null)
            {
                BindToPlayer(GameManager.Instance.PlayerTransform);
            }
        }

        private void OnDisable()
        {
            // Tier-1 unsubscribe. Safe to call even if OnEnable never fired
            // because C# delegate -= on a non-subscribed method is a no-op.
            GameManager.OnPlayerRegistered -= OnPlayerRegistered;
        }

        private void OnDestroy()
        {
            // Tier-2 cleanup: prevent callbacks arriving on a destroyed object.
            UnbindCurrentInventory();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TIER-1 EVENT HANDLER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="GameManager.OnPlayerRegistered"/> whenever a new
        /// player Transform is published — including on respawn after a scene reload.
        /// </summary>
        private void OnPlayerRegistered(Transform playerTransform)
        {
            BindToPlayer(playerTransform);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  BINDING LOGIC
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the <see cref="PlayerInventory"/> on <paramref name="player"/>,
        /// safely unsubscribes from the previous inventory (respawn safety),
        /// subscribes to all three slot events, and immediately syncs the UI
        /// to the inventory's current state.
        /// </summary>
        /// <param name="player">The player's root Transform. Must not be null.</param>
        private void BindToPlayer(Transform player)
        {
            if (player == null)
            {
                Debug.LogWarning("[UI_InventoryHUD] BindToPlayer called with a null Transform.");
                return;
            }

            if (!player.TryGetComponent<PlayerInventory>(out PlayerInventory inventory))
            {
                Debug.LogWarning("[UI_InventoryHUD] No PlayerInventory found on the " +
                                 $"registered player '{player.name}'. " +
                                 "Ensure PlayerInventory is on the Player root.", player);
                return;
            }

            // ── Unsubscribe from any previously bound inventory ───────────────
            // Critical for respawn: prevents the old (now-destroyed) inventory
            // from double-triggering events on the still-live HUD.
            UnbindCurrentInventory();

            // ── Tier-2: Subscribe to the new inventory ────────────────────────
            _boundInventory = inventory;
            _boundInventory.OnWeaponChanged     += OnWeaponChanged;
            _boundInventory.OnRelicChanged      += OnRelicChanged;
            _boundInventory.OnConsumableChanged += OnConsumableChanged;

            // ── Immediate sync: push current slot state to the UI ─────────────
            // Without this, the HUD would show empty slots until the next event,
            // which is wrong when the player already holds items (e.g. on Continue).
            _weaponSlot?    .UpdateSlot(_boundInventory.CurrentWeapon);
            _relicSlot?     .UpdateSlot(_boundInventory.CurrentRelic);
            _consumableSlot?.UpdateSlot(_boundInventory.CurrentConsumable);

            Debug.Log($"[UI_InventoryHUD] Bound to PlayerInventory on '{player.name}'.");
        }

        /// <summary>
        /// Safely unsubscribes from all events on <see cref="_boundInventory"/>
        /// and clears the reference. Called before rebinding and on destroy.
        /// </summary>
        private void UnbindCurrentInventory()
        {
            if (_boundInventory == null) return;

            _boundInventory.OnWeaponChanged     -= OnWeaponChanged;
            _boundInventory.OnRelicChanged      -= OnRelicChanged;
            _boundInventory.OnConsumableChanged -= OnConsumableChanged;
            _boundInventory = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TIER-2 EVENT HANDLERS
        //  Each handler receives the new ItemDataSO (or null on clear) and
        //  routes it to the correct UI_InventorySlot. No game logic here.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Invoked by <see cref="PlayerInventory.OnWeaponChanged"/>.
        /// Passes the weapon data (or null) to the weapon slot visual.
        /// </summary>
        private void OnWeaponChanged(WeaponDataSO weaponData)
        {
            _weaponSlot?.UpdateSlot(weaponData);
        }

        /// <summary>
        /// Invoked by <see cref="PlayerInventory.OnRelicChanged"/>.
        /// Passes the relic data (or null) to the relic slot visual.
        /// </summary>
        private void OnRelicChanged(RelicDataSO relicData)
        {
            _relicSlot?.UpdateSlot(relicData);
        }

        /// <summary>
        /// Invoked by <see cref="PlayerInventory.OnConsumableChanged"/>.
        /// Passes the consumable data (or null) to the consumable slot visual.
        /// </summary>
        private void OnConsumableChanged(ConsumableDataSO consumableData)
        {
            _consumableSlot?.UpdateSlot(consumableData);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        private void ValidateSlots()
        {
            if (_weaponSlot == null)
                Debug.LogError("[UI_InventoryHUD] _weaponSlot is not assigned. " +
                               "Assign it in the Inspector.", this);

            if (_relicSlot == null)
                Debug.LogError("[UI_InventoryHUD] _relicSlot is not assigned. " +
                               "Assign it in the Inspector.", this);

            if (_consumableSlot == null)
                Debug.LogError("[UI_InventoryHUD] _consumableSlot is not assigned. " +
                               "Assign it in the Inspector.", this);
        }
    }
}
