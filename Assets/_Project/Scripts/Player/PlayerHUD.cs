// =============================================================================
//  PlayerHUD.cs
//  Project : TopDownShooter
//
//  PURPOSE
//  -------
//  Connects player-side data components (HealthComponent, PlayerResourceComponent)
//  to their visual fill-bar Image counterparts in the HUD.
//  Purely reactive — subscribes to events and routes normalized values to bars.
//
//  ARCHITECTURE (Observer Pattern)
//  ─────────────────────────────────
//  • This script contains ZERO game logic.
//  • It subscribes to events in OnEnable and unsubscribes in OnDisable,
//    making it safe in pooling / scene reload scenarios.
//  • Initial bar values are pushed in OnEnable so bars are correct
//    from the first frame, even if components initialized before the HUD.
//
//  BARS MANAGED
//  ─────────────
//  • Health  — driven by HealthComponent.OnHealthChanged   (normalized float)
//  • Mana    — driven by PlayerResourceComponent.OnManaChanged   (normalized float)
//  • Energy  — driven by PlayerResourceComponent.OnEnergyChanged (normalized float)
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TopDownShooter.Player;

/// <summary>
/// Bridges <see cref="HealthComponent"/> and <see cref="PlayerResourceComponent"/>
/// events to their corresponding HUD fill-bar Images.
/// Contains no game logic — subscribe, route, unsubscribe.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  INSPECTOR FIELDS — DATA SOURCES
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Data Sources")]
    [Tooltip("HealthComponent on the Player. Provides OnHealthChanged events.")]
    [SerializeField] private HealthComponent playerHealth;

    [Tooltip("PlayerResourceComponent on the Player. " +
             "Provides OnManaChanged and OnEnergyChanged events.")]
    [SerializeField] private PlayerResourceComponent playerResources;

    // ─────────────────────────────────────────────────────────────────────────
    //  INSPECTOR FIELDS — BAR VISUALS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Bar Fill Images  (Image Type = Filled)")]
    [Tooltip("Fill Image for the health bar. Set Image Type to 'Filled' in the Inspector.")]
    [SerializeField] private Image healthBarFill;

    [Tooltip("Fill Image for the mana bar. Set Image Type to 'Filled' in the Inspector.")]
    [SerializeField] private Image manaBarFill;

    [Tooltip("Fill Image for the energy bar. Set Image Type to 'Filled' in the Inspector.")]
    [SerializeField] private Image energyBarFill;

    // ─────────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // ── Health ────────────────────────────────────────────────────────────
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthBar;

            // Immediate sync: push the current value so the bar is correct from
            // frame 1, even if the component already had damage before this subscribes.
            UpdateHealthBar(playerHealth.GetNormalizedHealth());
        }
        else
        {
            Debug.LogWarning("[PlayerHUD] playerHealth is not assigned. " +
                             "Health bar will not update.", this);
        }

        // ── Mana & Energy ─────────────────────────────────────────────────────
        if (playerResources != null)
        {
            playerResources.OnManaChanged   += UpdateManaBar;
            playerResources.OnEnergyChanged += UpdateEnergyBar;

            // Immediate sync for both resource bars.
            UpdateManaBar  (playerResources.GetNormalizedMana());
            UpdateEnergyBar(playerResources.GetNormalizedEnergy());
        }
        else
        {
            Debug.LogWarning("[PlayerHUD] playerResources is not assigned. " +
                             "Mana and Energy bars will not update.", this);
        }
    }

    private void OnDisable()
    {
        // ── Health ────────────────────────────────────────────────────────────
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealthBar;

        // ── Mana & Energy ─────────────────────────────────────────────────────
        if (playerResources != null)
        {
            playerResources.OnManaChanged   -= UpdateManaBar;
            playerResources.OnEnergyChanged -= UpdateEnergyBar;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PRIVATE BAR UPDATE HELPERS
    //  Each method is a thin, single-purpose router: incoming normalized float
    //  → fillAmount on the appropriate Image. Nothing more.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the health bar fill to the given normalized [0, 1] value.
    /// Subscribed to <see cref="HealthComponent.OnHealthChanged"/>.
    /// </summary>
    private void UpdateHealthBar(float normalized)
    {
        if (healthBarFill != null)
            healthBarFill.fillAmount = normalized;
    }

    /// <summary>
    /// Sets the mana bar fill to the given normalized [0, 1] value.
    /// Subscribed to <see cref="PlayerResourceComponent.OnManaChanged"/>.
    /// </summary>
    private void UpdateManaBar(float normalized)
    {
        if (manaBarFill != null)
            manaBarFill.fillAmount = normalized;
    }

    /// <summary>
    /// Sets the energy bar fill to the given normalized [0, 1] value.
    /// Subscribed to <see cref="PlayerResourceComponent.OnEnergyChanged"/>.
    /// </summary>
    private void UpdateEnergyBar(float normalized)
    {
        if (energyBarFill != null)
            energyBarFill.fillAmount = normalized;
    }
}