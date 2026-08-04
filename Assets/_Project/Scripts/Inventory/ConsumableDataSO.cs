


using UnityEngine;

namespace TopDownShooter.Inventory
{
    /// <summary>
    /// Plantilla ScriptableObject para objetos consumibles de un solo uso.
    /// Se consume a través de la acción de entrada Consume (tecla Q); vacía la ranura al usarse.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewConsumableData",
        menuName = "TopDownShooter/Inventory/Consumable Data")]
    public sealed class ConsumableDataSO : ItemDataSO
    {
        // ─────────────────────────────────────────────────────────────────────
        //  QUEST ITEM FLAG  (Part 3)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Quest Item")]
        [Tooltip("Cuando es verdadero, este consumible es un objeto de misión (ej. una Llave). PlayerInventory bloqueará la ruta de consumo con la tecla Q para que no se destruya accidentalmente. Use E para interactuar con objetos del mundo que requieran este objeto.")]
        [SerializeField] private bool _isQuestItem = false;

        // ─────────────────────────────────────────────────────────────────────
        //  CONSUMABLE EFFECT PARAMETERS  (Part 2 expansion stubs)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Consumable Effect")]
        [Tooltip("Puntos de vida planos restaurados inmediatamente al usar. 0 = sin curación (ej. solo aumento de velocidad). Limitado a MaxHealth.")]
        [Min(0)]
        [SerializeField] private int _healAmount = 30;

        [Tooltip("Duración en segundos para cualquier efecto temporal (ej. aumento de velocidad). 0 = instantáneo (pociones de curación, potenciadores de un solo disparo).")]
        [Min(0f)]
        [SerializeField] private float _effectDuration = 0f;

        [Tooltip("Porcentaje de aumento de velocidad aplicado durante EffectDuration segundos. 0 = sin cambio de velocidad. Leído por PlayerStats en la Parte 2.")]
        [Range(0f, 5f)]
        [SerializeField] private float _speedBoostMultiplier = 0f;

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC GETTERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cuando es verdadero, este objeto es un objeto de misión (ej. una Llave) y no puede ser
        /// consumido a través de la tecla Q. Use E para interactuar con objetos del mundo.
        /// </summary>
        public bool  IsQuestItem         => _isQuestItem;

        /// <summary>Puntos de salud planos restaurados cuando se usa este consumible.</summary>
        public int   HealAmount          => _healAmount;

        /// <summary>Duración en segundos para cualquier potenciador/efecto temporal. 0 = instantáneo.</summary>
        public float EffectDuration      => _effectDuration;

        /// <summary>Multiplicador fraccionario de aumento de velocidad para efectos temporales.</summary>
        public float SpeedBoostMultiplier => _speedBoostMultiplier;
    }
}
