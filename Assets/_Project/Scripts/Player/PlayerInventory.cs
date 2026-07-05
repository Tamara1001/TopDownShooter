// =============================================================================
//  PlayerInventory.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Manages the player's three fixed inventory slots: Weapon, Relic, Consumable.
//  Handles item pickup (E key) via Physics.OverlapSphere and item consumption
//  (Q key) with a stub that expands in Part 2.
//
//  ARCHITECTURE
//  ─────────────
//  • Slot state is private; external systems read it via events or read-only
//    properties. The HUD subscribes to events — it never polls slots directly.
//  • Picking up follows an atomic swap pattern:
//      1. Find nearest ItemPickup in radius.
//      2. Read its ItemDataSO and determine the slot.
//      3. Drop existing item (spawn dropPrefab) if slot is occupied.
//      4. Assign new item data and call DestroyPickup().
//  • This script never knows about damage, stats, or UI rendering.
//    Those concerns belong to listeners of OnWeaponChanged / OnRelicChanged /
//    OnConsumableChanged.
//
//  INPUT SYSTEM INTEGRATION
//  ─────────────────────────
//  PlayerInput (Send Messages behaviour) on the same GameObject broadcasts:
//    • OnInteract(InputValue)  ← "Interact" action (E key)
//    • OnConsume(InputValue)   ← "Consume"  action (Q key)
//  These method names MUST match the action names in CharacterActions.inputactions
//  exactly (Input System prepends "On" automatically).
//
//  STUBS IN PlayerController3D / PlayerCombat
//  ─────────────────────────────────────────────
//  PlayerInput broadcasts OnInteract and OnConsume to ALL MonoBehaviours on
//  the same GameObject. Add empty stubs in PlayerController3D if the Editor
//  logs "Method not found" warnings — this keeps SRP intact.
//
//  OOP RULES ENFORCED
//  ───────────────────
//  • All slot fields are private — no public mutable state.
//  • Public read-only properties expose slot data for HUD / FSM queries.
//  • Events (C# Action<T>) notify observers without coupling to them.
//  • No string comparisons for type detection — pattern matching on ItemDataSO
//    subclasses is compile-time safe and zero-allocation.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TopDownShooter.Inventory;
using TopDownShooter.Interaction;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Fixed three-slot inventory for the player character.
    /// Handles pickup (E), drop-on-swap, and consumable use (Q).
    /// Attach this MonoBehaviour to the Player GameObject alongside
    /// <see cref="PlayerController3D"/> and <see cref="Combat.PlayerCombat"/>.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Pickup Detection")]
        [Tooltip("World-space radius of the OverlapSphere used to detect nearby " +
                 "ItemPickup objects when the player presses Interact (E). " +
                 "Should be slightly larger than the items' SphereCollider radii.")]
        [SerializeField] private float _pickupRadius = 1.5f;

        [Tooltip("LayerMask for the layer(s) that contain ItemPickup colliders. " +
                 "Assign the 'Pickup' layer for best performance — the overlap " +
                 "sphere skips all other layers entirely.")]
        [SerializeField] private LayerMask _pickupLayerMask;

        [Header("World Interaction")]
        [Tooltip("LayerMask for world objects implementing IWorldInteractable " +
                 "(e.g. VictoryDoor). Assign the 'Interactable' layer. " +
                 "Checked BEFORE the pickup sphere, so doors take priority over " +
                 "floor items.")]
        [SerializeField] private LayerMask _interactableLayerMask;

        [Header("Drop Offset")]
        [Tooltip("Local-space offset from the player's position where dropped " +
                 "items are instantiated. Prevents items from spawning inside " +
                 "the player's collider. (0, 0, 0.8) = just in front of player.")]
        [SerializeField] private Vector3 _dropOffset = new Vector3(0f, 0f, 0.8f);

        [Header("Buffer Settings")]
        [Tooltip("Maximum number of colliders the OverlapSphere records per call. " +
                 "Increase only if many items can overlap simultaneously.")]
        [SerializeField] private int _overlapBufferSize = 8;

        // ─────────────────────────────────────────────────────────────────────
        //  INVENTORY SLOT STATE  (private — never exposed as mutable)
        // ─────────────────────────────────────────────────────────────────────

        // Each slot holds the DATA blueprint (SO) of the currently equipped item.
        // Null means the slot is empty.
        private WeaponDataSO      _currentWeapon;
        private RelicDataSO       _currentRelic;
        private ConsumableDataSO  _currentConsumable;

        // ─────────────────────────────────────────────────────────────────────
        //  EVENTS  (Observer Pattern — HUD and other systems subscribe here)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired whenever the Weapon slot changes (pickup or clear).
        /// Argument is the new <see cref="WeaponDataSO"/>, or <c>null</c> if emptied.
        /// </summary>
        public event Action<WeaponDataSO>     OnWeaponChanged;

        /// <summary>
        /// Fired whenever the Relic slot changes (pickup or clear).
        /// Argument is the new <see cref="RelicDataSO"/>, or <c>null</c> if emptied.
        /// </summary>
        public event Action<RelicDataSO>      OnRelicChanged;

        /// <summary>
        /// Fired whenever the Consumable slot changes (pickup, use, or clear).
        /// Argument is the new <see cref="ConsumableDataSO"/>, or <c>null</c> if emptied.
        /// </summary>
        public event Action<ConsumableDataSO> OnConsumableChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC READ-ONLY PROPERTIES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Currently equipped weapon data. Null if slot is empty.</summary>
        public WeaponDataSO     CurrentWeapon     => _currentWeapon;

        /// <summary>Currently equipped relic data. Null if slot is empty.</summary>
        public RelicDataSO      CurrentRelic      => _currentRelic;

        /// <summary>Currently equipped consumable data. Null if slot is empty.</summary>
        public ConsumableDataSO CurrentConsumable => _currentConsumable;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE RUNTIME STATE
        // ─────────────────────────────────────────────────────────────────────

        // Pre-allocated overlap buffer — zero allocations during pickup.
        private Collider[] _overlapBuffer;

        // Cached transform for position queries inside tight loops.
        private Transform _transform;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _transform     = transform;
            _overlapBuffer = new Collider[_overlapBufferSize];

            ValidateSetup();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INPUT SYSTEM CALLBACKS  (Send Messages — called by PlayerInput)
        //
        //  NAMING CONTRACT:
        //  Method name = "On" + exact Action name in CharacterActions.inputactions
        //  These are called via reflection — any typo silently breaks the binding.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Receives the "Interact" action (E key) from PlayerInput.
        /// Priority order:
        ///   1. Checks for an <see cref="IWorldInteractable"/> (doors, switches) via
        ///      <see cref="_interactableLayerMask"/> — these take precedence over items.
        ///   2. Falls back to <see cref="TryPickupNearestItem"/> for floor pickups.
        /// </summary>
        public void OnInteract(InputValue value)
        {
            // Only respond to the press event, not the release.
            if (!value.isPressed) return;

            // ── Priority 1: World Interactables (doors, switches, NPCs) ─────────
            if (TryWorldInteract()) return;

            // ── Priority 2: Item Pickup fallback ─────────────────────────────────
            TryPickupNearestItem();
        }

        /// <summary>
        /// Receives the "Consume" action (Q key) from PlayerInput.
        /// Uses the currently held consumable if one is equipped.
        /// Quest items (<see cref="ConsumableDataSO.IsQuestItem"/> == true) are
        /// intentionally blocked — they must be used via the E-key interact flow.
        /// </summary>
        public void OnConsume(InputValue value)
        {
            if (!value.isPressed) return;

            if (_currentConsumable == null)
            {
                Debug.Log("[PlayerInventory] OnConsume: Consumable slot is empty.");
                return;
            }

            // Guard: quest items (e.g. Keys) cannot be consumed via Q.
            if (_currentConsumable.IsQuestItem)
            {
                Debug.Log($"[PlayerInventory] Cannot consume quest items. " +
                          $"'{_currentConsumable.DisplayName}' must be used " +
                          "by interacting (E) with the appropriate world object.");
                return;
            }

            ConsumeCurrentItem();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  WORLD INTERACTION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Performs an OverlapSphere using <see cref="_interactableLayerMask"/> and
        /// calls <see cref="IWorldInteractable.Interact"/> on the first matching
        /// component found.
        /// </summary>
        /// <returns><c>true</c> if a world interactable was found and called; <c>false</c> otherwise.</returns>
        private bool TryWorldInteract()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                _transform.position,
                _pickupRadius,
                _overlapBuffer,
                _interactableLayerMask);

            if (hitCount == 0) return false;

            IWorldInteractable interactable = null;

            for (int i = 0; i < hitCount; i++)
            {
                if (_overlapBuffer[i].TryGetComponent<IWorldInteractable>(out IWorldInteractable found))
                {
                    interactable = found;
                    break; // Use the first valid one found.
                }
            }

            // Clear buffer references to prevent GC from retaining dead objects.
            for (int i = 0; i < hitCount; i++)
                _overlapBuffer[i] = null;

            if (interactable == null) return false;

            interactable.Interact(this);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PICKUP LOGIC
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the nearest <see cref="ItemPickup"/> within <see cref="_pickupRadius"/>,
        /// reads its <see cref="ItemDataSO"/>, drops any existing item in the matching
        /// slot, and assigns the new one.
        ///
        /// ALGORITHM:
        ///   1. Physics.OverlapSphereNonAlloc → fills _overlapBuffer, zero allocation.
        ///   2. Iterate hits, TryGetComponent to find ItemPickup instances.
        ///   3. Track the closest one by sqrMagnitude (avoids sqrt until selection).
        ///   4. Pattern-match on ItemDataSO subtype to determine the target slot.
        ///   5. Atomic swap: drop old → assign new → DestroyPickup.
        /// </summary>
        private void TryPickupNearestItem()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                _transform.position,
                _pickupRadius,
                _overlapBuffer,
                _pickupLayerMask);

            if (hitCount == 0) return;

            // ── Find nearest ItemPickup in the results ────────────────────────
            ItemPickup nearest        = null;
            float      nearestSqrDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                if (!_overlapBuffer[i].TryGetComponent<ItemPickup>(out ItemPickup pickup))
                    continue;

                float sqrDist = (_overlapBuffer[i].transform.position
                                 - _transform.position).sqrMagnitude;

                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest        = pickup;
                }
            }

            // Clear buffer references to prevent GC from retaining dead objects.
            for (int i = 0; i < hitCount; i++)
                _overlapBuffer[i] = null;

            if (nearest == null) return;

            // ── Read the item data and route to the correct slot ──────────────
            ItemDataSO itemData = nearest.GetItemData();
            if (itemData == null)
            {
                Debug.LogWarning("[PlayerInventory] Found an ItemPickup with no ItemDataSO assigned. " +
                                 "Configure the pickup in the Inspector.", nearest);
                return;
            }

            ExecutePickup(itemData, nearest);
        }

        /// <summary>
        /// Performs the atomic slot-swap for the given item.
        /// Uses C# pattern matching (is T t) to determine the slot —
        /// no string comparisons, compile-time safe, zero allocations.
        /// </summary>
        /// <param name="itemData">The blueprint of the item being picked up.</param>
        /// <param name="pickup">The world object to destroy after collection.</param>
        private void ExecutePickup(ItemDataSO itemData, ItemPickup pickup)
        {
            if (itemData is WeaponDataSO weapon)
            {
                SwapWeapon(weapon);
            }
            else if (itemData is RelicDataSO relic)
            {
                SwapRelic(relic);
            }
            else if (itemData is ConsumableDataSO consumable)
            {
                SwapConsumable(consumable);
            }
            else
            {
                // Future-proof: unknown subclass. Log but don't crash.
                Debug.LogWarning($"[PlayerInventory] Unknown ItemDataSO subtype: " +
                                 $"'{itemData.GetType().Name}'. Add a new slot or handler.", this);
                return; // Do NOT destroy pickup if we can't process it.
            }

            // The slot is now updated. Remove the world object.
            pickup.DestroyPickup();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SLOT SWAP HELPERS
        //  Each helper: (1) drops existing, (2) assigns new, (3) fires event.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Drops the current weapon (if any) and equips the new one.
        /// </summary>
        private void SwapWeapon(WeaponDataSO newWeapon)
        {
            if (_currentWeapon != null)
            {
                DropItem(_currentWeapon);
                Debug.Log($"[PlayerInventory] Dropped weapon: '{_currentWeapon.DisplayName}'.");
            }

            _currentWeapon = newWeapon;
            Debug.Log($"[PlayerInventory] Equipped weapon: '{_currentWeapon.DisplayName}'.");

            // Notify HUD, PlayerCombat, and any other observer.
            OnWeaponChanged?.Invoke(_currentWeapon);
        }

        /// <summary>
        /// Drops the current relic (if any) and equips the new one.
        /// </summary>
        private void SwapRelic(RelicDataSO newRelic)
        {
            if (_currentRelic != null)
            {
                DropItem(_currentRelic);
                Debug.Log($"[PlayerInventory] Dropped relic: '{_currentRelic.DisplayName}'.");
            }

            _currentRelic = newRelic;
            Debug.Log($"[PlayerInventory] Equipped relic: '{_currentRelic.DisplayName}'.");

            OnRelicChanged?.Invoke(_currentRelic);
        }

        /// <summary>
        /// Drops the current consumable (if any) and picks up the new one.
        /// </summary>
        private void SwapConsumable(ConsumableDataSO newConsumable)
        {
            if (_currentConsumable != null)
            {
                DropItem(_currentConsumable);
                Debug.Log($"[PlayerInventory] Dropped consumable: '{_currentConsumable.DisplayName}'.");
            }

            _currentConsumable = newConsumable;
            Debug.Log($"[PlayerInventory] Picked up consumable: '{_currentConsumable.DisplayName}'.");

            OnConsumableChanged?.Invoke(_currentConsumable);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DROP LOGIC
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Instantiates the item's <see cref="ItemDataSO.DropPrefab"/> at the
        /// player's position (with a configurable local offset) so the dropped
        /// item can be picked up again immediately.
        ///
        /// The drop uses <see cref="_dropOffset"/> in local space (player-relative)
        /// so the item always lands in front of the player regardless of rotation.
        /// </summary>
        /// <param name="item">The item blueprint whose DropPrefab to instantiate.</param>
        private void DropItem(ItemDataSO item)
        {
            if (item.DropPrefab == null)
            {
                Debug.LogWarning($"[PlayerInventory] '{item.DisplayName}' has no DropPrefab assigned. " +
                                 "The item is lost permanently. Assign a DropPrefab in the SO.", this);
                return;
            }

            // Convert local offset to world space using the player's current rotation.
            Vector3 worldDropPosition = _transform.TransformPoint(_dropOffset);

            // Instantiate at the player's Y position to avoid items floating or sinking.
            worldDropPosition.y = _transform.position.y;

            Instantiate(item.DropPrefab, worldDropPosition, Quaternion.identity);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CONSUME LOGIC
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Activates the currently held consumable item and clears the slot.
        ///
        /// Part 1 STUB: Logs the action and fires <see cref="OnConsumableChanged"/>.
        ///
        /// Part 2 EXPANSION:
        /// Read <c>_currentConsumable.HealAmount</c> and call
        /// <c>GetComponent&lt;HealthComponent&gt;().Heal(healAmount)</c>.
        /// Read <c>_currentConsumable.SpeedBoostMultiplier</c> and call
        /// <c>PlayerStats.ApplyTemporarySpeedBoost(...)</c>.
        /// Route VFX and SFX through dedicated managers.
        /// </summary>
        private void ConsumeCurrentItem()
        {
            Debug.Log($"[PlayerInventory] Consuming '{_currentConsumable.DisplayName}'.");

            // Apply healing if the player has a HealthComponent.
            if (TryGetComponent<HealthComponent>(out var health))
            {
                health.Heal(_currentConsumable.HealAmount);
                Debug.Log($"[PlayerInventory] Healed {_currentConsumable.HealAmount} HP.");
            }
            else
            {
                Debug.LogWarning("[PlayerInventory] No HealthComponent found on this GameObject. " +
                                 "Healing effect was skipped.", this);
            }

            // Apply a temporary speed boost if this consumable defines one.
            // Both guards must pass: duration > 0 (timed effect) AND multiplier > 0 (speed type).
            // Plain healing potions (EffectDuration == 0) are intentionally skipped.
            if (TryGetComponent<PlayerStatsComponent>(out var stats))
            {
                if (_currentConsumable.EffectDuration > 0f && _currentConsumable.SpeedBoostMultiplier > 0f)
                {
                    stats.ApplyTemporarySpeedBoost(
                        _currentConsumable.SpeedBoostMultiplier,
                        _currentConsumable.EffectDuration);
                }
            }

            // Clear the slot after use — consumables are single-use.
            _currentConsumable = null;
            OnConsumableChanged?.Invoke(null);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        private void ValidateSetup()
        {
            if (_pickupLayerMask.value == 0)
            {
                Debug.LogWarning("[PlayerInventory] Pickup LayerMask is empty (Everything). " +
                                 "The OverlapSphere will test ALL colliders in the scene. " +
                                 "Assign the 'Pickup' layer for better performance.", this);
            }

            if (_interactableLayerMask.value == 0)
            {
                Debug.LogWarning("[PlayerInventory] Interactable LayerMask is empty. " +
                                 "World interactables (doors, switches) will not be detected. " +
                                 "Assign the 'Interactable' layer in the Inspector.", this);
            }

            if (_overlapBufferSize <= 0)
            {
                Debug.LogError("[PlayerInventory] Overlap buffer size must be > 0. Defaulting to 8.", this);
                _overlapBufferSize = 8;
                _overlapBuffer = new Collider[_overlapBufferSize];
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR GIZMOS
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Pickup radius — green
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.12f);
            Gizmos.DrawSphere(transform.position, _pickupRadius);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, _pickupRadius);

            // Interactable radius — cyan (same radius, different color)
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.12f);
            Gizmos.DrawSphere(transform.position, _pickupRadius);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _pickupRadius);

            // Drop offset position — orange dot
            Vector3 dropWorld = transform.TransformPoint(_dropOffset);
            dropWorld.y = transform.position.y;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
            Gizmos.DrawSphere(dropWorld, 0.08f);
            Gizmos.DrawLine(transform.position, dropWorld);
        }
#endif
    }
}
