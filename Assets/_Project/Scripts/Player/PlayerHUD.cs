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
//  JUICE ADDITIONS
//  ───────────────
//  • Heal Punch   : Health bar punches to 1.2x scale whenever HP is restored.
//  • Damage Flash : Health bar flashes red when HP decreases.
//  • Mana Flash   : Mana bar flashes red when a mana-gated attack is rejected.
//  • Energy Flash : Energy bar flashes red when a dash or energy-gated attack
//    is rejected.
//  Events are now isolated: PlayerCombat.OnManaDepleted and OnEnergyDepleted,
//  PlayerController3D.OnEnergyDepleted.
//
//  BARS MANAGED
//  ────────────
//  • Health  — driven by HealthComponent.OnHealthChanged   (normalized float)
//  • Mana    — driven by PlayerResourceComponent.OnManaChanged   (normalized float)
//  • Energy  — driven by PlayerResourceComponent.OnEnergyChanged (normalized float)
// =============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDownShooter.Player;
using TopDownShooter.Combat;

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

    // ───────────────────────────────────────────────────────────────────────────
    //  JUICE SETTINGS
    // ───────────────────────────────────────────────────────────────────────────

    [Header("Juice Settings")]
    [Tooltip("Scale the health bar punches to on heal.")]
    [SerializeField] private float _healPunchScale   = 1.2f;

    [Tooltip("Duration in seconds of the heal punch-scale lerp-back.")]
    [SerializeField] private float _punchDuration    = 0.15f;

    [Tooltip("Duration in seconds the resource bars stay red on error.")]
    [SerializeField] private float _flashDuration    = 0.15f;

    [Tooltip("Color the resource bars flash to when a resource error fires.")]
    [SerializeField] private Color _errorFlashColor  = Color.red;

    // ───────────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ───────────────────────────────────────────────────────────────────────────

    [Header("Wallet UI")]
    [Tooltip("Text element to display the coin count.")]
    [SerializeField] private TextMeshProUGUI _coinText;

    [Tooltip("Scale multiplier when coins are collected.")]
    [SerializeField] private float _pulseScale = 1.4f;

    [Tooltip("Duration of the coin text pulse animation.")]
    [SerializeField] private float _pulseDuration = 0.2f;

    private PlayerWallet _wallet;
    private Coroutine _pulseCoroutine;
    private Vector3 _originalScale;

    // The normalized health value from the last UpdateHealthBar call.
    // Sentinel -1 means "not yet set" so the first call never false-triggers a punch.
    private float _previousHealth = -1f;

    // Original fill colors — cached in Awake so the flash coroutine can restore them.
    private Color _originalHealthColor;
    private Color _originalManaColor;
    private Color _originalEnergyColor;

    // Per-bar flash coroutine handles — independent so they don't cancel each other.
    private Coroutine _healthFlash;
    private Coroutine _manaFlash;
    private Coroutine _energyFlash;

    // ─────────────────────────────────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _wallet = FindObjectOfType<PlayerWallet>();
        
        if (_coinText != null)
        {
            _originalScale = _coinText.transform.localScale;
            _coinText.text = "0";
        }

        // Cache the designers' original colors so flash coroutines can restore them.
        _originalHealthColor = healthBarFill != null ? healthBarFill.color : Color.white;
        _originalManaColor   = manaBarFill   != null ? manaBarFill.color   : Color.white;
        _originalEnergyColor = energyBarFill != null ? energyBarFill.color : Color.white;
    }

    private void OnEnable()
    {
        // ── Health ──────────────────────────────────────────────────────────────────
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

        // ── Resource Error Events (static — no instance reference needed) ────────────
        PlayerCombat.OnManaDepleted          += HandleManaDepleted;
        PlayerCombat.OnEnergyDepleted        += HandleEnergyDepleted;
        PlayerController3D.OnEnergyDepleted  += HandleEnergyDepleted;

        // ── Wallet ──────────────────────────────────────────────────────────────────
        if (_wallet != null)
        {
            _wallet.OnCoinsChanged += HandleCoinsChanged;
            if (_coinText != null) _coinText.text = _wallet.Coins.ToString();
        }
    }

    private void OnDisable()
    {
        // ── Health ──────────────────────────────────────────────────────────────────
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealthBar;

        // ── Mana & Energy ─────────────────────────────────────────────────────
        if (playerResources != null)
        {
            playerResources.OnManaChanged   -= UpdateManaBar;
            playerResources.OnEnergyChanged -= UpdateEnergyBar;
        }

        // ── Resource Error Events ───────────────────────────────────────────────────
        PlayerCombat.OnManaDepleted          -= HandleManaDepleted;
        PlayerCombat.OnEnergyDepleted        -= HandleEnergyDepleted;
        PlayerController3D.OnEnergyDepleted  -= HandleEnergyDepleted;

        // ── Wallet ──────────────────────────────────────────────────────────────────
        if (_wallet != null)
        {
            _wallet.OnCoinsChanged -= HandleCoinsChanged;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PRIVATE BAR UPDATE HELPERS
    //  Each method is a thin, single-purpose router: incoming normalized float
    //  → fillAmount on the appropriate Image. Nothing more.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the health bar fill to the given normalized [0, 1] value.
    /// Triggers a punch-scale animation when health increases (healed).
    /// Subscribed to <see cref="HealthComponent.OnHealthChanged"/>.
    /// </summary>
    private void UpdateHealthBar(float normalized)
    {
        if (healthBarFill == null) return;

        healthBarFill.fillAmount = normalized;

        // Guard: sentinel -1 means "first call ever" — skip both visual effects.
        if (_previousHealth >= 0f)
        {
            // Damage flash: health decreased → flash bar red.
            if (normalized < _previousHealth)
            {
                if (_healthFlash != null) StopCoroutine(_healthFlash);
                _healthFlash = StartCoroutine(FlashBarRoutine(healthBarFill, _originalHealthColor));
            }

            // Heal punch: health increased → punch-scale the bar.
            if (normalized > _previousHealth)
                StartCoroutine(PunchScaleRoutine(healthBarFill.rectTransform));
        }

        _previousHealth = normalized;
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

    // ───────────────────────────────────────────────────────────────────────────
    //  JUICE — EVENT HANDLERS
    // ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Handles <see cref="PlayerCombat.OnManaDepleted"/>.
    /// Flashes only the mana bar so the player knows exactly which resource is short.
    /// </summary>
    private void HandleManaDepleted()
    {
        if (manaBarFill == null) return;
        if (_manaFlash != null) StopCoroutine(_manaFlash);
        _manaFlash = StartCoroutine(FlashBarRoutine(manaBarFill, _originalManaColor));
    }

    /// <summary>
    /// Handles <see cref="PlayerCombat.OnEnergyDepleted"/> and
    /// <see cref="PlayerController3D.OnEnergyDepleted"/>.
    /// Flashes only the energy bar.
    /// </summary>
    private void HandleEnergyDepleted()
    {
        if (energyBarFill == null) return;
        if (_energyFlash != null) StopCoroutine(_energyFlash);
        _energyFlash = StartCoroutine(FlashBarRoutine(energyBarFill, _originalEnergyColor));
    }

    /// <summary>
    /// Updates the coin text and triggers a juice pulse.
    /// </summary>
    private void HandleCoinsChanged(int amount)
    {
        if (_coinText == null) return;
        
        _coinText.text = amount.ToString();
        
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = StartCoroutine(PulseText());
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  JUICE — COROUTINES
    // ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantly scales the text up, then smoothly lerps back to original scale.
    /// </summary>
    private IEnumerator PulseText()
    {
        if (_coinText == null) yield break;

        _coinText.transform.localScale = _originalScale * _pulseScale;

        float elapsed = 0f;
        while (elapsed < _pulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _pulseDuration;
            _coinText.transform.localScale = Vector3.Lerp(_originalScale * _pulseScale, _originalScale, t);
            yield return null;
        }

        _coinText.transform.localScale = _originalScale;
        _pulseCoroutine = null;
    }

    /// <summary>
    /// Parameterised single-bar flash: sets the bar's color to
    /// <see cref="_errorFlashColor"/> for <see cref="_flashDuration"/> seconds,
    /// then restores <paramref name="originalColor"/>.
    /// Reusable for any bar (health, mana, energy).
    /// </summary>
    private IEnumerator FlashBarRoutine(Image bar, Color originalColor)
    {
        if (bar == null) yield break;

        bar.color = _errorFlashColor;
        yield return new WaitForSeconds(_flashDuration);
        bar.color = originalColor;
    }

    /// <summary>
    /// Instantly scales <paramref name="rt"/> to <see cref="_healPunchScale"/>, then
    /// lerps it back to <see cref="Vector3.one"/> over <see cref="_punchDuration"/> seconds.
    /// Safe to run concurrently with other instances on different RectTransforms.
    /// </summary>
    private IEnumerator PunchScaleRoutine(RectTransform rt)
    {
        if (rt == null) yield break;

        // Snap to punch scale immediately for an instant visual pop.
        rt.localScale = new Vector3(_healPunchScale, _healPunchScale, _healPunchScale);

        float elapsed = 0f;
        while (elapsed < _punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _punchDuration);
            rt.localScale = Vector3.Lerp(
                new Vector3(_healPunchScale, _healPunchScale, _healPunchScale),
                Vector3.one,
                t);
            yield return null;
        }

        // Guarantee clean landing exactly at 1,1,1.
        rt.localScale = Vector3.one;
    }
}