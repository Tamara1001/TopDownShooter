
using UnityEngine;
using UnityEngine.UI;
using TopDownShooter.Inventory;

namespace TopDownShooter.UI
{
    /// <summary>
    /// Componente visual para una única ranura de inventario.
    /// Llame a <see cref="UpdateSlot"/> para establecer o limpiar el icono mostrado.
    /// </summary>
    public sealed class UI_InventorySlot : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  INSPECTOR FIELDS
        // ─────────────────────────────────────────────────────────────────────

        [Header("Visuales de Ranura")]
        [Tooltip("El componente Image que renderiza el icono del objeto dentro de esta ranura. Asigne el Image hijo que se superpone al arte de fondo de la ranura.")]
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
        /// Actualiza la visual de esta ranura para reflejar el blueprint del objeto proporcionado.
        /// </summary>
        /// <param name="itemData">
        /// El objeto a mostrar. Pase <c>null</c> para limpiar la ranura
        /// (por ejemplo, cuando el objeto fue consumido o soltado).
        /// </param>
        public void UpdateSlot(ItemDataSO itemData)
        {
            if (_iconImage == null) return;

            if (itemData == null)
            {
                // La ranura está vacía — ocultar el icono para evitar que quede arte fantasma.
                _iconImage.sprite  = null;
                _iconImage.enabled = false;
            }
            else
            {
                // Mostrar el icono del objeto. Se habilita incluso si el Icon es nulo para que
                // el diseñador pueda detectar fácilmente asignaciones de sprites faltantes en tiempo de ejecución.
                _iconImage.sprite  = itemData.Icon;
                _iconImage.enabled = true;
            }
        }
    }
}
