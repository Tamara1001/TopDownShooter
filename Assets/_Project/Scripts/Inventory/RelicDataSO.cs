


using UnityEngine;

namespace TopDownShooter.Inventory
{
    /// <summary>
    /// Plantilla ScriptableObject para objetos de reliquia pasiva.
    /// Se equipa en la ranura de Reliquia; otorga modificadores de estadísticas mientras se posea.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewRelicData",
        menuName = "TopDownShooter/Inventory/Relic Data")]
    public sealed class RelicDataSO : ItemDataSO
    {
        // ─────────────────────────────────────────────────────────────────────
        //  RELIC MODIFIERS  (Part 2 expansion stubs)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Passive Modifiers")]
        [Tooltip("Porcentaje de bonificación de velocidad de movimiento mientras esta reliquia está equipada. 0 = sin bonificación, 0.2 = +20%. Leído por PlayerStats en la Parte 2.")]
        [Range(-1f, 5f)]
        [SerializeField] private float _moveSpeedModifier = 0f;

        [Tooltip("Bonificación plana agregada a la salud máxima del jugador mientras está equipada. Puede ser negativa (reliquias malditas). Leído por PlayerStats en la Parte 2.")]
        [SerializeField] private int _maxHealthBonus = 0;

        [Tooltip("Multiplicador de daño porcentual mientras está equipada. 0 = sin cambios, 0.5 = +50% de daño. Leído por PlayerStats en la Parte 2.")]
        [Range(-1f, 5f)]
        [SerializeField] private float _damageModifier = 0f;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC GETTERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Multiplicador fraccionario de velocidad aplicado por PlayerStats mientras está equipada.</summary>
        public float MoveSpeedModifier => _moveSpeedModifier;

        /// <summary>Bonificación plana de salud máxima aplicada por PlayerStats mientras está equipada.</summary>
        public int   MaxHealthBonus    => _maxHealthBonus;

        /// <summary>Multiplicador fraccionario de daño aplicado por PlayerStats mientras está equipada.</summary>
        public float DamageModifier    => _damageModifier;
    }
}
