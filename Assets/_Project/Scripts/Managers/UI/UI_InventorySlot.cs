// =============================================================================
//  UI_InventorySlot.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  A self-contained visual component for a single inventory slot in the HUD.
//  Responsible ONLY for displaying or clearing an icon sprite inside its
//  Image component. It has no knowledge of the player, inventory system, or
//  game state — it simply reacts to the data pushed into it.
//
//  ARCHITECTURE (Single Responsibility)
//  ─────────────────────────────────────
//  • This is the "View" in a passive MVC pattern.
//  • UI_InventoryHUD (the "Presenter") calls UpdateSlot() whenever a slot
//    changes. This script never subscribes to any event itself.
//  • Null safety: if itemData is null (item dropped / slot cleared), the icon
//    is disabled so no ghost sprite ever lingers on screen.
//
//  ATTACH TO
//  ─────────
//  One of the three child slot GameObjects inside the inventory HUD container.
//  Assign the Image component that displays the item icon to _iconImage.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TopDownShooter.Inventory;

namespace TopDownShooter.UI
{
    /// <summary>
    /// Visual component for a single inventory slot.
    /// Call <see cref="UpdateSlot"/> to set or clear the displayed icon.
    /// </summary>
    public sealed class UI_InventorySlot : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Slot Visuals")]
        [Tooltip("The Image component that renders the item icon inside this slot. " +
                 "Assign the child Image that sits over the slot background art.")]
        [SerializeField] private Image _iconImage;

        // ─────────────────────────────────────────────────────────────────────
        //  UNITY LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_iconImage == null)
            {
                Debug.LogError($"[UI_InventorySlot] '{gameObject.name}': " +
                               "_iconImage is not assigned. Assign it in the Inspector.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates this slot's visual to reflect the given item blueprint.
        /// </summary>
        /// <param name="itemData">
        /// The item to display. Pass <c>null</c> to clear the slot
        /// (e.g. when the item was consumed or dropped).
        /// </param>
        public void UpdateSlot(ItemDataSO itemData)
        {
            if (_iconImage == null) return;

            if (itemData == null)
            {
                // Slot is empty — hide the icon to avoid lingering ghost art.
                _iconImage.sprite  = null;
                _iconImage.enabled = false;
            }
            else
            {
                // Display the item's icon. Enabled even if Icon is null so the
                // designer can spot missing sprite assignments easily at runtime.
                _iconImage.sprite  = itemData.Icon;
                _iconImage.enabled = true;
            }
        }
    }
}
