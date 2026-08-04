


using UnityEngine;

namespace TopDownShooter.Inventory
{
    /// <summary>
    /// Objeto físico en el suelo. Se adjunta al prefab del objeto junto con un disparador
    /// <see cref="SphereCollider"/>. Consultado por <see cref="PlayerInventory"/>
    /// a través de <see cref="Physics.OverlapSphere"/> en la entrada Interact.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class ItemPickup : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CAMPOS DEL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("Item Data")]
        [Tooltip("La plantilla ScriptableObject que describe este objeto (tipo, estadísticas, icono, prefab de caída). Asigne el asset WeaponDataSO, RelicDataSO o ConsumableDataSO correspondiente aquí.")]
        [SerializeField] private ItemDataSO _itemData;

        [Header("Hover Animation (optional)")]
        [Tooltip("Amplitud de la flotación senoidal en unidades de mundo. 0 = deshabilitado.")]
        [SerializeField] private float _hoverAmplitude = 0.15f;

        [Tooltip("Velocidad de la oscilación de flotación en ciclos por segundo.")]
        [SerializeField] private float _hoverFrequency = 1.2f;

        [Tooltip("Grados por segundo para el giro inactivo en el eje Y.")]
        [SerializeField] private float _spinSpeed = 45f;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO PRIVADO
        // ─────────────────────────────────────────────────────────────────────

        // Referencia almacenada en caché para rendimiento (evita el acceso repetido a propiedades).
        private Transform _transform;

        // Posición Y en espacio de mundo registrada al aparecer; la flotación oscila a su alrededor.
        private float _baseY;

        // ─────────────────────────────────────────────────────────────────────
        //  CICLO DE VIDA DE UNITY
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _transform = transform;
            _baseY     = _transform.position.y;

            ValidateSetup();
        }

        private void Update()
        {
            // Flotación + giro — puramente cosmético, independiente de la lógica del juego.
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
        //  API PÚBLICA  (llamada por PlayerInventory)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve la plantilla <see cref="ItemDataSO"/> para este objeto en el suelo.
        /// <para>
        /// <see cref="PlayerInventory"/> lee esto para determinar qué ranura de inventario
        /// llenar y qué estadísticas lleva el objeto.
        /// </para>
        /// </summary>
        /// <returns>
        /// El <see cref="ItemDataSO"/> asignado, o <c>null</c> si está mal configurado.
        /// </returns>
        public ItemDataSO GetItemData() => _itemData;

        /// <summary>
        /// Elimina este objeto del mundo de la escena después de haber sido recolectado.
        /// Llamado por <see cref="PlayerInventory"/> inmediatamente después de que se hayan
        /// leído los datos del objeto y se hayan colocado en una ranura de inventario.
        ///
        /// GANCHO DE POOLING: Reemplace <c>Destroy(gameObject)</c> con una llamada Release
        /// del pool en la Parte 2 si el nivel utiliza pooling de objetos.
        /// </summary>
        public void DestroyPickup()
        {
            // ► GANCHO DE POOL: objectPool?.Release(this);
            Destroy(gameObject);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDACIÓN
        // ─────────────────────────────────────────────────────────────────────

        private void ValidateSetup()
        {
            if (_itemData == null)
            {
                Debug.LogError($"[ItemPickup] '{name}': No ItemDataSO assigned! " +
                               "This pickup cannot be collected. Assign a WeaponDataSO, " +
                               "RelicDataSO, or ConsumableDataSO in the Inspector.", this);
            }

            // Verificar que el SphereCollider esté configurado como disparador (trigger).
            var col = GetComponent<SphereCollider>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning($"[ItemPickup] '{name}': The SphereCollider is NOT set as a Trigger. " +
                                 "PlayerInventory uses Physics.OverlapSphere, which works regardless, " +
                                 "but the trigger should be enabled to avoid blocking player movement.", this);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GIZMOS DE EDITOR
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
