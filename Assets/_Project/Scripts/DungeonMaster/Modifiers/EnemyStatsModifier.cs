// =============================================================================
//  EnemyStatsModifier.cs
//  Project : TopDownShooter – Dungeon Master System
//
//  PROPÓSITO
//  ---------
//  Un "Mega-Modificador" impulsado por datos para alterar múltiples
//  estadísticas de los enemigos al mismo tiempo al entrar en una sala.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TopDownShooter.Enemy;

namespace TopDownShooter.DungeonMaster
{
    [CreateAssetMenu(fileName = "New_EnemyStatsModifier", menuName = "TopDownShooter/Modifiers/Enemy Stats Modifier")]
    public sealed class EnemyStatsModifier : DungeonModifierSO
    {
        // ─────────────────────────────────────────────────────────────────────
        //  CONFIGURACIÓN DATA-DRIVEN (Toggles)
        // ─────────────────────────────────────────────────────────────────────

        [Header("Movement Speed")]
        [SerializeField] private bool _modifySpeed;
        [Tooltip("Multiplicador fraccional. 1.5 = +50% (Buff). 0.5 = -50% (Debuff).")]
        [SerializeField] private float _speedMultiplier = 1f;

        [Header("Max Health")]
        [SerializeField] private bool _modifyMaxHealth;
        [Tooltip("Multiplicador de vida. 0.5 = 50% de la vida (Nerf). 2.0 = Doble de vida (Buff).")]
        [SerializeField] private float _healthMultiplier = 1f;

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
            foreach (GameObject enemy in roomEnemies)
            {
                if (enemy == null) continue;

                if (_modifySpeed && enemy.TryGetComponent<NavMeshAgent>(out NavMeshAgent agent))
                {
                    agent.speed *= _speedMultiplier;
                }

                if (_modifyMaxHealth && enemy.TryGetComponent<HealthComponent>(out HealthComponent health))
                {
                    health.ScaleMaxHealth(_healthMultiplier);
                }

                if ((_modifyDamage || _modifyCooldown) && enemy.TryGetComponent<EnemyBrain>(out EnemyBrain brain))
                {
                    float dmg = _modifyDamage ? _damageMultiplier : 1f;
                    float cd = _modifyCooldown ? _cooldownMultiplier : 1f;
                    brain.SetWeaponDungeonMultipliers(dmg, cd);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  REVERT
        // ─────────────────────────────────────────────────────────────────────

        public override void RevertModifier(GameObject player, List<GameObject> roomEnemies)
        {
            // Sin reversión.
            // Los enemigos mueren o son destruidos cuando la sala se despeja, 
            // por lo que no es necesario revertir su estado para la siguiente sala.
        }
    }
}
