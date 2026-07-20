// =============================================================================
//  PlayerStatsModifier.cs
//  Project : TopDownShooter – Dungeon Master System
//
//  PROPÓSITO
//  ---------
//  Un "Mega-Modificador" impulsado por datos que permite a los diseñadores
//  crear cualquier tipo de buff o debuff para el jugador combinando variables.
//  Reemplaza la necesidad de tener scripts individuales para cada efecto.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using TopDownShooter.Player;
using TopDownShooter.Combat;

namespace TopDownShooter.DungeonMaster
{
    [CreateAssetMenu(fileName = "New_PlayerStatsModifier", menuName = "TopDownShooter/Modifiers/Player Stats Modifier")]
    public sealed class PlayerStatsModifier : DungeonModifierSO
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CONFIGURACIÓN DATA-DRIVEN (Toggles)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Movement Speed")]
        [SerializeField] private bool _modifySpeed;
        [Tooltip("Fracción aditiva. 0.4 = +40% (Buff). -0.3 = -30% (Debuff).")]
        [SerializeField] private float _speedBonus;

        [Header("Mana Consumption")]
        [SerializeField] private bool _modifyManaCost;
        [Tooltip("Multiplicador. 1.0 = Normal. 2.0 = Coste Doble (Debuff). 0.5 = Mitad de Coste (Buff).")]
        [SerializeField] private float _manaCostMultiplier = 1f;

        [Header("Combat Stats")]
        [SerializeField] private bool _modifyDamage;
        [Tooltip("Multiplicador de Daño. 2.0 = Doble Daño. 0.5 = Mitad de Daño.")]
        [SerializeField] private float _damageMultiplier = 1f;

        [SerializeField] private bool _modifyCooldown;
        [Tooltip("Multiplicador de Cooldown. 0.5 = Ataca el doble de rápido. 2.0 = Ataca a la mitad de velocidad.")]
        [SerializeField] private float _cooldownMultiplier = 1f;

        // ─────────────────────────────────────────────────────────────────────
        //  APPLY
        // ─────────────────────────────────────────────────────────────────────

        public override void ApplyModifier(GameObject player, List<GameObject> roomEnemies)
        {
            if (player == null) return;

            if (_modifySpeed && player.TryGetComponent<PlayerStatsComponent>(out PlayerStatsComponent stats))
            {
                stats.SetDungeonSpeedModifier(_speedBonus);
            }

            if (_modifyManaCost && player.TryGetComponent<PlayerResourceComponent>(out PlayerResourceComponent resources))
            {
                resources.SetManaCostMultiplier(_manaCostMultiplier);
            }

            if ((_modifyDamage || _modifyCooldown) && player.TryGetComponent<PlayerCombat>(out PlayerCombat combat))
            {
                float dmg = _modifyDamage ? _damageMultiplier : 1f;
                float cd = _modifyCooldown ? _cooldownMultiplier : 1f;
                combat.SetWeaponDungeonMultipliers(dmg, cd);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  REVERT
        // ─────────────────────────────────────────────────────────────────────

        public override void RevertModifier(GameObject player, List<GameObject> roomEnemies)
        {
            if (player == null) return;

            if (_modifySpeed && player.TryGetComponent<PlayerStatsComponent>(out PlayerStatsComponent stats))
            {
                stats.SetDungeonSpeedModifier(0f);
            }

            if (_modifyManaCost && player.TryGetComponent<PlayerResourceComponent>(out PlayerResourceComponent resources))
            {
                resources.SetManaCostMultiplier(1f);
            }

            if ((_modifyDamage || _modifyCooldown) && player.TryGetComponent<PlayerCombat>(out PlayerCombat combat))
            {
                combat.SetWeaponDungeonMultipliers(1f, 1f);
            }
        }
    }
}
