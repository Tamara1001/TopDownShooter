// =============================================================================
//  ItemPickup.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Represents a physical item lying on the ground, waiting to be picked up.
//  This component is the "world body" of an item — it connects the trigger
//  collider volume to its associated ItemDataSO data blueprint.
//
//  RESPONSIBILITIES (Single Responsibility Principle):
//  • Store a reference to the ItemDataSO that describes this item.
//  • Expose GetItemData() so PlayerInventory can read what it is.
//  • Expose DestroyPickup() to cleanly remove the world object after pickup.
//  • Optionally: play a hover animation / highlight glow (visual only).
//
//  THIS SCRIPT MUST NOT:
//  • Know anything about the player's inventory slots.
//  • Apply any stat modifiers or trigger any game logic.
//  • Detect its own pickup (that is PlayerInventory's job, via OverlapSphere).
//
//  REQUIRED SETUP (per prefab):
//  • SphereCollider with "Is Trigger = true" for proximity detection.
//  • A visible mesh/renderer for the item model.
//  • Assign the matching ItemDataSO to the _itemData field.
//
//  POOLING NOTE (Part 2):
//  Replace Destroy(gameObject) in DestroyPickup() with a pool.Release() call
//  if the item system is pooled. The API contract (void DestroyPickup()) stays
//  identical — PlayerInventory does not need to change.
// =============================================================================

using UnityEngine;

namespace TopDownShooter.Inventory
{
    /// <summary>
    /// Physical ground item. Attach to the item prefab alongside a trigger
    /// <see cref="SphereCollider"/>. Queried by <see cref="PlayerInventory"/>
    /// via <see cref="Physics.OverlapSphere"/> on the Interact input.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class ItemPickup : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Item Data")]
        [Tooltip("The ScriptableObject blueprint that describes this item " +
                 "(type, stats, icon, drop prefab). Assign the matching " +
                 "WeaponDataSO, RelicDataSO, or ConsumableDataSO asset here.")]
        [SerializeField] private ItemDataSO _itemData;

        [Header("Hover Animation (optional)")]
        [Tooltip("Amplitude of the sine-wave hover in world units. 0 = disabled.")]
        [SerializeField] private float _hoverAmplitude = 0.15f;

        [Tooltip("Speed of the hover oscillation in cycles per second.")]
        [SerializeField] private float _hoverFrequency = 1.2f;

        [Tooltip("Degrees per second for the idle Y-axis spin.")]
        [SerializeField] private float _spinSpeed = 45f;

        // ─────────────────────────────────────────────────────────────────────
        //  PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────

        // Cached reference for performance (avoids repeated property access).
        private Transform _transform;

        // World-space Y position recorded at spawn; hover oscillates around it.
        private float _baseY;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _transform = transform;
            _baseY     = _transform.position.y;

            ValidateSetup();
        }

        private void Update()
        {
            // Hover + spin — purely cosmetic, independent of game logic.
            if (_hoverAmplitude > 0f)
            {
                Vector3 pos = _transform.position;
                pos.y = _baseY + Mathf.Sin(Time.time * _hoverFrequency * Mathf.PI * 2f)
                        * _hoverAmplitude;
                _transform.position = pos;
            }

            if (_spinSpeed != 0f)
                _transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API  (called by PlayerInventory)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the <see cref="ItemDataSO"/> blueprint for this ground item.
        /// <para>
        /// <see cref="PlayerInventory"/> reads this to determine which inventory
        /// slot to fill and what stats the item carries.
        /// </para>
        /// </summary>
        /// <returns>
        /// The assigned <see cref="ItemDataSO"/>, or <c>null</c> if misconfigured.
        /// </returns>
        public ItemDataSO GetItemData() => _itemData;

        /// <summary>
        /// Removes this world object from the scene after it has been collected.
        /// Called by <see cref="PlayerInventory"/> immediately after the item
        /// data has been read and placed into an inventory slot.
        ///
        /// POOLING HOOK: Replace <c>Destroy(gameObject)</c> with a pool Release
        /// call in Part 2 if the level uses item pooling.
        /// </summary>
        public void DestroyPickup()
        {
            // ► POOL HOOK: objectPool?.Release(this);
            Destroy(gameObject);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        private void ValidateSetup()
        {
            if (_itemData == null)
            {
                Debug.LogError($"[ItemPickup] '{name}': No ItemDataSO assigned! " +
                               "This pickup cannot be collected. Assign a WeaponDataSO, " +
                               "RelicDataSO, or ConsumableDataSO in the Inspector.", this);
            }

            // Verify the SphereCollider is set as a trigger.
            var col = GetComponent<SphereCollider>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning($"[ItemPickup] '{name}': The SphereCollider is NOT set as a Trigger. " +
                                 "PlayerInventory uses Physics.OverlapSphere, which works regardless, " +
                                 "but the trigger should be enabled to avoid blocking player movement.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EDITOR GIZMOS
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<SphereCollider>();
            if (col == null) return;

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.3f);
            Gizmos.DrawSphere(transform.position, col.radius);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, col.radius);

            if (_itemData != null)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * (col.radius + 0.2f),
                    _itemData.DisplayName);
            }
        }
#endif
    }
}
