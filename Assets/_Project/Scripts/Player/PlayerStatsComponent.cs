
using System.Collections;
using UnityEngine;
using TopDownShooter.Inventory;

namespace TopDownShooter.Player
{
    /// <summary>
    /// Escucha a <see cref="PlayerInventory.OnRelicChanged"/> y recalcula
    /// los multiplicadores de estadísticas pasivas de la reliquia equipada, y aplica aumentos
    /// temporales de velocidad de consumibles a través de <see cref="ApplyTemporarySpeedBoost"/>.
    /// Los sistemas consumidores (por ejemplo, <see cref="PlayerController3D"/>) solo leen el
    /// float combinado <see cref="MoveSpeedMultiplier"/> — están completamente desacoplados
    /// de las fuentes subyacentes.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerStatsComponent : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  PROPIEDADES PÚBLICAS  (calculadas; de solo lectura desde el exterior)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Multiplicador de velocidad de movimiento combinado: valor base 1.0 más el bono de reliquia
        /// más cualquier bono de consumible activo.<br/>
        /// Ejemplos: 1.0 = sin cambios, 1.2 = +20%, 1.5 = reliquia +20% y poción +30%.
        /// </summary>
        public float MoveSpeedMultiplier => 1f + _relicSpeedModifier + _consumableSpeedModifier + _dungeonSpeedModifier;

        // ─────────────────────────────────────────────────────────────────────
        //  ESTADO PRIVADO
        // ─────────────────────────────────────────────────────────────────────

        // Referencia guardada en caché al inventario hermano — requerida por el atributo.
        private PlayerInventory _inventory;

        // Bono de velocidad fraccional de la reliquia equipada actualmente (permanente mientras esté equipada).
        // 0f = sin bono. Mutado solo por HandleRelicChanged().
        private float _relicSpeedModifier = 0f;

        // Bono de velocidad fraccional de un efecto de consumible activo (temporal, temporizado).
        // 0f = sin efecto activo. Mutado solo por SpeedBuffRoutine().
        private float _consumableSpeedModifier = 0f;

        // Referencia a la corrutina de aumento de velocidad en ejecución, o nulo si no hay ninguna activa.
        // Guardado para que un nuevo efecto pueda cancelar uno en progreso antes de comenzar uno nuevo.
        private Coroutine _activeBuffCoroutine;

        // Modificador de velocidad persistente inyectado por el sistema D20 Dungeon Master.
        // A diferencia del consumable (temporizado), este dura hasta que el Director
        // llame explícitamente a SetDungeonSpeedModifier(0f) al despejar la sala.
        private float _dungeonSpeedModifier = 0f;

        // ─────────────────────────────────────────────────────────────────────
        //  VFX EXPUESTOS EN EL INSPECTOR
        // ─────────────────────────────────────────────────────────────────────

        [Header("VFX")]
        [Tooltip("Sistema de partículas reproducido mientras un aumento de velocidad está activo. Déjelo sin asignar para omitir (seguro contra nulos).")]
        [SerializeField] private ParticleSystem _speedAuraParticles;

        // ─────────────────────────────────────────────────────────────────────
        //  CICLO DE VIDA DE UNITY
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // GetComponent es seguro aquí: RequireComponent garantiza la presencia.
            _inventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable()
        {
            _inventory.OnRelicChanged += HandleRelicChanged;

            // Sincronizar inmediatamente en caso de que ya hubiera una reliquia equipada
            // antes de que este componente fuera habilitado (por ejemplo, cargado desde un guardado).
            HandleRelicChanged(_inventory.CurrentRelic);
        }

        private void OnDisable()
        {
            _inventory.OnRelicChanged -= HandleRelicChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MANEJADOR DE EVENTOS — RELIQUIA
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Actualiza <see cref="_relicSpeedModifier"/> cada vez que cambia la ranura de reliquia.
        /// Llamado por <see cref="PlayerInventory.OnRelicChanged"/> (<c>null</c> = limpiado).
        /// El getter <see cref="MoveSpeedMultiplier"/> detecta el cambio automáticamente.
        /// </summary>
        /// <param name="relic">La reliquia recién equipada, o <c>null</c> si se limpia.</param>
        private void HandleRelicChanged(RelicDataSO relic)
        {
            _relicSpeedModifier = relic != null ? relic.MoveSpeedModifier : 0f;

            Debug.Log(relic != null
                ? $"[PlayerStatsComponent] Relic '{relic.DisplayName}' equipped. " +
                  $"RelicSpeedModifier = {_relicSpeedModifier:+0.##;-0.##;0}"
                : "[PlayerStatsComponent] Relic unequipped. RelicSpeedModifier reset to 0.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  API PÚBLICA — EFECTOS DE CONSUMIBLES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Aplica un aumento de velocidad fraccional temporal que expira después de
        /// <paramref name="duration"/> segundos.
        /// <para>
        /// Si ya hay un aumento activo, se cancela y se reemplaza — no se acumula.
        /// El aumento es un bono aditivo fraccional: 0.3 = +30% de velocidad.
        /// </para>
        /// </summary>
        /// <param name="boostMultiplier">Bono de velocidad fraccional (por ejemplo, 0.3 para +30%).</param>
        /// <param name="duration">Segundos antes de que expire el aumento.</param>
        public void ApplyTemporarySpeedBoost(float boostMultiplier, float duration)
        {
            // Cancelar cualquier aumento en progreso para que el nuevo tenga pleno efecto de inmediato.
            if (_activeBuffCoroutine != null)
            {
                StopCoroutine(_activeBuffCoroutine);
                _activeBuffCoroutine = null;
            }

            _activeBuffCoroutine = StartCoroutine(SpeedBuffRoutine(boostMultiplier, duration));
        }

        /// <summary>
        /// Establece un modificador de velocidad persistente desde el sistema Dungeon Master.
        /// A diferencia de <see cref="ApplyTemporarySpeedBoost"/>, este NO expira por tiempo:
        /// el llamador es responsable de revertirlo llamando con 0f al limpiar la sala.
        /// </summary>
        /// <param name="modifier">Modificador fraccional aditivo (ej. 0.4 = +40%, -0.3 = -30%).</param>
        public void SetDungeonSpeedModifier(float modifier)
        {
            _dungeonSpeedModifier = modifier;
            Debug.Log($"[PlayerStatsComponent] DungeonSpeedModifier set to {modifier:+0.##;-0.##;0}. " +
                      $"MoveSpeedMultiplier = {MoveSpeedMultiplier:0.##}x");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CORRUTINAS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Establece <see cref="_consumableSpeedModifier"/>, espera y luego lo limpia.
        /// Gestionado exclusivamente a través de <see cref="ApplyTemporarySpeedBoost"/>.
        /// </summary>
        private IEnumerator SpeedBuffRoutine(float boostMultiplier, float duration)
        {
            _consumableSpeedModifier = boostMultiplier;

            // Iniciar el VFX de aura de velocidad (seguro contra nulos — sin error si no está asignado).
            _speedAuraParticles?.Play();

            Debug.Log($"[PlayerStatsComponent] Speed buff active: +{boostMultiplier:P0} for {duration:0.#}s. " +
                      $"MoveSpeedMultiplier = {MoveSpeedMultiplier:0.##}x");

            yield return new WaitForSeconds(duration);

            // Detener el aura antes de limpiar el modificador para que el VFX termine limpiamente.
            _speedAuraParticles?.Stop();

            _consumableSpeedModifier = 0f;
            _activeBuffCoroutine     = null;
            Debug.Log("[PlayerStatsComponent] Speed buff expired. MoveSpeedMultiplier = " +
                      $"{MoveSpeedMultiplier:0.##}x");
        }
    }
}
