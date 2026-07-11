// =============================================================================
//  PlayerHUD.cs
// =============================================================================
//  PURPOSE:
//    Listens to events from HealthComponent, PlayerResourceComponent, and
//    PlayerWallet, then updates the on-screen HUD.
//
//    ALL THREE resources (Health, Mana, Energy) are now displayed as rows of
//    fractional icons, exactly like the Zelda heart system:
//
//      Resource | Icons | Max value | Points per full icon | Points per quarter
//      ---------|-------|-----------|----------------------|-------------------
//      Health   |   5   |    100    |         20           |         5
//      Mana     |   5   |    100    |         20           |         5
//      Energy   |   5   |    100    |         20           |         5
//
//    A single centralized helper method (UpdateFractionalIcons) owns all the
//    math so there is ZERO duplication between the three resources.
//
//  OBSERVER PATTERN — This script ONLY reads data; it never writes to the
//  source components. All updates are driven by events.
// =============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDownShooter.Player;
using TopDownShooter.Combat;

public class PlayerHUD : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // INSPECTOR FIELDS
    // -------------------------------------------------------------------------

    [Header("Data Sources")]
    [SerializeField] private HealthComponent playerHealth;
    [SerializeField] private PlayerResourceComponent playerResources;

    // -------------------------------------------------------------------------
    // HEALTH — HEART DISPLAY
    // -------------------------------------------------------------------------
    //
    //  Design rules (same as before):
    //    • Max HP = 100, split across 5 hearts.
    //    • Each full heart = 20 HP.
    //    • Each heart has 5 states (sprite index 0–4):
    //
    //        State index | HP in this heart | Sprite suggestion
    //        ------------|------------------|------------------
    //            0       |   0 HP  (empty)  | heart_empty
    //            1       |   5 HP  (1/4)    | heart_quarter
    //            2       |  10 HP  (half)   | heart_half
    //            3       |  15 HP  (3/4)    | heart_three_quarters
    //            4       |  20 HP  (full)   | heart_full
    //
    // -------------------------------------------------------------------------

    [Header("Health Visuals (Hearts)")]
    [Tooltip("The parent RectTransform that groups all heart images. Used for the heal punch-scale animation.")]
    [SerializeField] private RectTransform heartsContainer;

    [Tooltip("The individual Image components for each heart slot, ordered left to right (index 0 = leftmost heart).")]
    [SerializeField] private Image[] corazonesUI;

    [Tooltip(
        "The 5 heart-state sprites in STRICT ORDER:\n" +
        "  [0] Empty       (0 HP)\n" +
        "  [1] Quarter     (5 HP)\n" +
        "  [2] Half        (10 HP)\n" +
        "  [3] Three-Qtr  (15 HP)\n" +
        "  [4] Full        (20 HP)")]
    [SerializeField] private Sprite[] estadosCorazon;

    // -------------------------------------------------------------------------
    // MANA — FLAME ICON DISPLAY
    // -------------------------------------------------------------------------
    //
    //  Same fractional math as hearts, but for Mana:
    //    • Max Mana = 100, split across 5 flame icons.
    //    • Each full flame = 20 Mana.
    //    • Each flame has 5 states (sprite index 0–4):
    //
    //        State index | Mana in this icon | Sprite suggestion
    //        ------------|-------------------|------------------
    //            0       |   0 Mana (empty)  | flame_empty
    //            1       |   5 Mana (1/4)    | flame_quarter
    //            2       |  10 Mana (half)   | flame_half
    //            3       |  15 Mana (3/4)    | flame_three_quarters
    //            4       |  20 Mana (full)   | flame_full
    //
    // -------------------------------------------------------------------------

    [Header("Mana Visuals (Flames)")]
    [Tooltip("The parent RectTransform that groups all mana flame images. Used for the error flash animation.")]
    [SerializeField] private RectTransform manaContainer;

    [Tooltip("The individual Image components for each mana flame slot, ordered left to right.")]
    [SerializeField] private Image[] manaUI;

    [Tooltip(
        "The 5 mana-state sprites in STRICT ORDER:\n" +
        "  [0] Empty       (0 Mana)\n" +
        "  [1] Quarter     (5 Mana)\n" +
        "  [2] Half        (10 Mana)\n" +
        "  [3] Three-Qtr  (15 Mana)\n" +
        "  [4] Full        (20 Mana)")]
    [SerializeField] private Sprite[] manaStates;

    // -------------------------------------------------------------------------
    // ENERGY — LIGHTNING BOLT ICON DISPLAY
    // -------------------------------------------------------------------------
    //
    //  Same fractional math as hearts and flames, but for Energy:
    //    • Max Energy = 100, split across 5 lightning bolt icons.
    //    • Each full bolt = 20 Energy.
    //    • Each bolt has 5 states (sprite index 0–4):
    //
    //        State index | Energy in this icon | Sprite suggestion
    //        ------------|---------------------|------------------
    //            0       |   0 Energy (empty)  | bolt_empty
    //            1       |   5 Energy (1/4)    | bolt_quarter
    //            2       |  10 Energy (half)   | bolt_half
    //            3       |  15 Energy (3/4)    | bolt_three_quarters
    //            4       |  20 Energy (full)   | bolt_full
    //
    // -------------------------------------------------------------------------

    [Header("Energy Visuals (Lightning Bolts)")]
    [Tooltip("The parent RectTransform that groups all energy bolt images. Used for the error flash animation.")]
    [SerializeField] private RectTransform energyContainer;

    [Tooltip("The individual Image components for each energy bolt slot, ordered left to right.")]
    [SerializeField] private Image[] energyUI;

    [Tooltip(
        "The 5 energy-state sprites in STRICT ORDER:\n" +
        "  [0] Empty       (0 Energy)\n" +
        "  [1] Quarter     (5 Energy)\n" +
        "  [2] Half        (10 Energy)\n" +
        "  [3] Three-Qtr  (15 Energy)\n" +
        "  [4] Full        (20 Energy)")]
    [SerializeField] private Sprite[] energyStates;

    // -------------------------------------------------------------------------
    // JUICE — ANIMATION SETTINGS
    // -------------------------------------------------------------------------

    [Header("Juice Settings")]
    [Tooltip("Scale multiplier applied to heartsContainer when the player is healed.")]
    [SerializeField] private float _healPunchScale = 1.2f;

    [Tooltip("How long (seconds) the punch-scale animation takes to return to normal.")]
    [SerializeField] private float _punchDuration = 0.15f;

    [Tooltip("How long (seconds) the flash color stays visible on damage or resource depletion.")]
    [SerializeField] private float _flashDuration = 0.15f;

    [Tooltip("The color the icons flash when the player takes damage or depletes a resource.")]
    [SerializeField] private Color _errorFlashColor = Color.red;

    // -------------------------------------------------------------------------
    // WALLET / COIN UI
    // -------------------------------------------------------------------------

    [Header("Wallet UI")]
    [SerializeField] private TextMeshProUGUI _coinText;

    [Tooltip("Scale multiplier applied to the coin text when coins are added.")]
    [SerializeField] private float _pulseScale = 1.4f;

    [Tooltip("How long (seconds) the coin-text pulse animation takes to shrink back.")]
    [SerializeField] private float _pulseDuration = 0.2f;

    // -------------------------------------------------------------------------
    // ICON MATH CONSTANTS
    // -------------------------------------------------------------------------
    //
    //  These constants are shared by ALL three resource types because they all
    //  follow the exact same fractional icon rules:
    //    MAX_VALUE         = 100  (total resource points)
    //    ICONS_COUNT       =   5  (number of icons in the row)
    //    POINTS_PER_ICON   =  20  (100 / 5)
    //    POINTS_PER_QUARTER =  5  (20 / 4 quarters)
    //
    // -------------------------------------------------------------------------

    /// <summary>
    /// Total resource points that one completely filled icon represents.
    /// (Max 100 / 5 icons = 20 points per icon)
    /// </summary>
    private const int POINTS_PER_ICON = 20;

    /// <summary>
    /// Resource points that one quarter-segment of an icon represents.
    /// (20 points per icon / 4 quarters = 5 points per quarter)
    /// </summary>
    private const int POINTS_PER_QUARTER = 5;

    // -------------------------------------------------------------------------
    // PRIVATE RUNTIME STATE
    // -------------------------------------------------------------------------

    private PlayerWallet _wallet;
    private Coroutine    _pulseCoroutine;
    private Vector3      _originalCoinTextScale;

    // Keeps track of the previous normalized health so we can detect
    // whether the player healed (value went UP) or took damage (value went DOWN).
    private float _previousHealth = -1f;

    // Coroutine handles let us stop a running flash/pulse before starting a new one,
    // which prevents visual glitches when events fire in rapid succession.
    private Coroutine _healthFlash;
    private Coroutine _manaFlash;
    private Coroutine _energyFlash;

    // -------------------------------------------------------------------------
    // UNITY LIFECYCLE
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Find the PlayerWallet that lives somewhere in the scene.
        _wallet = FindObjectOfType<PlayerWallet>();

        // Cache coin text state so we can restore it after the pulse animation.
        if (_coinText != null)
        {
            _originalCoinTextScale = _coinText.transform.localScale;
            _coinText.text = "0";
        }
    }

    private void OnEnable()
    {
        // ── Health ──────────────────────────────────────────────────────────
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthBar;

            // Draw the initial heart state immediately so the HUD is correct
            // from the very first frame (before any damage is taken).
            UpdateHealthBar(playerHealth.GetNormalizedHealth());
        }

        // ── Resources (Mana / Energy) ────────────────────────────────────────
        if (playerResources != null)
        {
            playerResources.OnManaChanged   += UpdateManaBar;
            playerResources.OnEnergyChanged += UpdateEnergyBar;

            // Initialise icon rows with current values on startup.
            UpdateManaBar(playerResources.GetNormalizedMana());
            UpdateEnergyBar(playerResources.GetNormalizedEnergy());
        }

        // ── Depletion flash events (fired by combat/movement systems) ────────
        PlayerCombat.OnManaDepleted         += HandleManaDepleted;
        PlayerCombat.OnEnergyDepleted       += HandleEnergyDepleted;
        PlayerController3D.OnEnergyDepleted += HandleEnergyDepleted;

        // ── Wallet ───────────────────────────────────────────────────────────
        if (_wallet != null)
        {
            _wallet.OnCoinsChanged += HandleCoinsChanged;
            if (_coinText != null) _coinText.text = _wallet.Coins.ToString();
        }
    }

    private void OnDisable()
    {
        // Always unsubscribe in OnDisable to prevent memory leaks and
        // "ghost" callbacks from destroyed objects.
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= UpdateHealthBar;

        if (playerResources != null)
        {
            playerResources.OnManaChanged   -= UpdateManaBar;
            playerResources.OnEnergyChanged -= UpdateEnergyBar;
        }

        PlayerCombat.OnManaDepleted         -= HandleManaDepleted;
        PlayerCombat.OnEnergyDepleted       -= HandleEnergyDepleted;
        PlayerController3D.OnEnergyDepleted -= HandleEnergyDepleted;

        if (_wallet != null) _wallet.OnCoinsChanged -= HandleCoinsChanged;
    }

    // =========================================================================
    // CENTRALIZED ICON MATH HELPER  ← THE KEY NEW FUNCTION
    // =========================================================================

    /// <summary>
    /// Updates a row of fractional resource icons to reflect <paramref name="currentValue"/>.
    ///
    /// HOW THE MATH WORKS (same logic for Health, Mana, and Energy):
    ///
    ///   Each icon in the array is responsible for a 20-point "window":
    ///     Icon 0 →  1 – 20 pts
    ///     Icon 1 → 21 – 40 pts
    ///     Icon 2 → 41 – 60 pts
    ///     Icon 3 → 61 – 80 pts
    ///     Icon 4 → 81 – 100 pts
    ///
    ///   For each icon we calculate how many points "spill" into its window:
    ///     pointsInThisIcon = currentValue - (iconIndex * POINTS_PER_ICON)
    ///     → clamped to [0, 20] so it's never negative or over-full.
    ///
    ///   We then divide by POINTS_PER_QUARTER (5) using integer division to
    ///   get a sprite state index in [0, 4]:
    ///     0 pts  → index 0 → Empty sprite
    ///     1–5    → index 1 → Quarter sprite
    ///     6–10   → index 2 → Half sprite
    ///     11–15  → index 3 → Three-quarter sprite
    ///     16–20  → index 4 → Full sprite
    ///
    ///   EXAMPLE: currentValue = 55 (out of 100)
    ///     i=0 → 55 - (0*20) = 55 → clamp → 20 → /5 = 4 → Full
    ///     i=1 → 55 - (1*20) = 35 → clamp → 20 → /5 = 4 → Full
    ///     i=2 → 55 - (2*20) = 15 → clamp → 15 → /5 = 3 → Three-quarter
    ///     i=3 → 55 - (3*20) = -5 → clamp →  0 → /5 = 0 → Empty
    ///     i=4 → 55 - (4*20) =-25 → clamp →  0 → /5 = 0 → Empty
    ///
    /// </summary>
    /// <param name="currentValue">The current resource value as an integer (e.g. 55).</param>
    /// <param name="uiIcons">The array of Image components to update (one per icon slot).</param>
    /// <param name="states">
    ///   Array of exactly 5 sprites in order: [0]=Empty, [1]=Quarter, [2]=Half,
    ///   [3]=Three-quarter, [4]=Full.
    /// </param>
    private void UpdateFractionalIcons(int currentValue, Image[] uiIcons, Sprite[] states)
    {
        // ── Safety checks ───────────────────────────────────────────────────
        // If any required reference is missing in the Inspector, bail out
        // silently rather than throwing a NullReferenceException at runtime.
        if (uiIcons == null || uiIcons.Length == 0) return;
        if (states  == null || states.Length < 5)   return;

        // ── Loop over every icon slot ───────────────────────────────────────
        for (int i = 0; i < uiIcons.Length; i++)
        {
            // Skip null entries (in case an array slot was left empty).
            if (uiIcons[i] == null) continue;

            // STEP A: How many points "spill" into this icon's 20-pt window?
            int pointsInThisIcon = currentValue - (i * POINTS_PER_ICON);

            // Clamp so we never exceed 20 (full) or go below 0 (empty).
            pointsInThisIcon = Mathf.Clamp(pointsInThisIcon, 0, POINTS_PER_ICON);

            // STEP B: Integer division → sprite state index [0, 4].
            //   0 pts  →  0  (Empty)
            //   1-5    →  1  (Quarter)
            //   6-10   →  2  (Half)
            //   11-15  →  3  (Three-quarter)
            //   16-20  →  4  (Full)
            int stateIndex = pointsInThisIcon / POINTS_PER_QUARTER;

            // Apply the correct sprite to this icon slot's Image component.
            uiIcons[i].sprite = states[stateIndex];
        }
    }

    // =========================================================================
    // HEALTH HEARTS — UPDATE LOGIC
    // =========================================================================

    /// <summary>
    /// Called every time HealthComponent fires OnHealthChanged.
    /// Converts the normalised health float back to an integer HP value,
    /// then delegates all icon-sprite math to UpdateFractionalIcons.
    /// </summary>
    /// <param name="normalized">Health fraction 0.0 (dead) → 1.0 (full).</param>
    private void UpdateHealthBar(float normalized)
    {
        // We need the component to read CurrentHealth as a raw integer.
        if (playerHealth == null) return;

        // Convert normalised float → integer HP.
        // We read CurrentHealth directly (e.g. 65) instead of using the
        // normalised float to avoid any floating-point rounding errors at
        // exact quarter-segment boundaries.
        int currentHP = playerHealth.CurrentHealth;

        // Hand off all math to the shared helper.
        UpdateFractionalIcons(currentHP, corazonesUI, estadosCorazon);

        // ── JUICE — Animate on heal or damage ────────────────────────────────
        // _previousHealth is -1 on the very first call (initialisation),
        // so we skip animations that frame to avoid a false "heal" flash.
        if (_previousHealth >= 0f)
        {
            if (normalized < _previousHealth)
            {
                // Health went DOWN → player took damage → flash all hearts red.
                if (_healthFlash != null) StopCoroutine(_healthFlash);
                _healthFlash = StartCoroutine(FlashIconsRoutine(corazonesUI));
            }
            else if (normalized > _previousHealth && heartsContainer != null)
            {
                // Health went UP → player was healed → punch-scale the container.
                StartCoroutine(PunchScaleRoutine(heartsContainer));
            }
        }

        // Store normalised value for the next comparison.
        _previousHealth = normalized;
    }

    // =========================================================================
    // MANA FLAMES — UPDATE LOGIC
    // =========================================================================

    /// <summary>
    /// Called every time PlayerResourceComponent fires OnManaChanged.
    /// Converts the normalised mana float to an integer and delegates to
    /// UpdateFractionalIcons.
    /// </summary>
    /// <param name="normalized">Mana fraction 0.0 (empty) → 1.0 (full).</param>
    private void UpdateManaBar(float normalized)
    {
        if (playerResources == null) return;

        // Convert normalised float → integer Mana points.
        // PlayerResourceComponent.CurrentMana is already an int property.
        int currentMana = playerResources.CurrentMana;

        // Hand off all math to the shared helper.
        UpdateFractionalIcons(currentMana, manaUI, manaStates);
    }

    // =========================================================================
    // ENERGY BOLTS — UPDATE LOGIC
    // =========================================================================

    /// <summary>
    /// Called every time PlayerResourceComponent fires OnEnergyChanged.
    /// Converts the normalised energy float to an integer and delegates to
    /// UpdateFractionalIcons.
    /// </summary>
    /// <param name="normalized">Energy fraction 0.0 (empty) → 1.0 (full).</param>
    private void UpdateEnergyBar(float normalized)
    {
        if (playerResources == null) return;

        // Convert normalised float → integer Energy points.
        int currentEnergy = playerResources.CurrentEnergy;

        // Hand off all math to the shared helper.
        UpdateFractionalIcons(currentEnergy, energyUI, energyStates);
    }

    // =========================================================================
    // DEPLETION FLASH HANDLERS
    // =========================================================================
    //  These are called by static events when a system TRIES to spend a resource
    //  but there isn't enough. They trigger the error flash "juice" animation.

    private void HandleManaDepleted()
    {
        // Guard: if the array hasn't been set up in the Inspector, do nothing.
        if (manaUI == null || manaUI.Length == 0) return;

        // Stop any flash already in progress before starting a fresh one.
        if (_manaFlash != null) StopCoroutine(_manaFlash);
        _manaFlash = StartCoroutine(FlashIconsRoutine(manaUI));
    }

    private void HandleEnergyDepleted()
    {
        // Guard: if the array hasn't been set up in the Inspector, do nothing.
        if (energyUI == null || energyUI.Length == 0) return;

        if (_energyFlash != null) StopCoroutine(_energyFlash);
        _energyFlash = StartCoroutine(FlashIconsRoutine(energyUI));
    }

    // =========================================================================
    // WALLET / COINS
    // =========================================================================

    private void HandleCoinsChanged(int newAmount)
    {
        if (_coinText == null) return;
        _coinText.text = newAmount.ToString();

        // Stop any already-running pulse so we don't have two competing routines.
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = StartCoroutine(PulseText());
    }

    // =========================================================================
    // COROUTINES — JUICE ANIMATIONS
    // =========================================================================

    /// <summary>
    /// Instantly changes ALL icons in the given array to the error flash colour,
    /// then restores them to white after <see cref="_flashDuration"/> seconds.
    ///
    /// This is the same concept as the old FlashBarRoutine / FlashHeartsRoutine,
    /// but now works with any icon array — so it handles Hearts, Flames, and
    /// Bolts without duplicating the coroutine code.
    /// </summary>
    /// <param name="icons">The icon Image array to flash.</param>
    private IEnumerator FlashIconsRoutine(Image[] icons)
    {
        // Flash all icons red.
        foreach (var img in icons)
            if (img != null) img.color = _errorFlashColor;

        yield return new WaitForSeconds(_flashDuration);

        // Restore all icons to white (default Unity Image color).
        foreach (var img in icons)
            if (img != null) img.color = Color.white;
    }

    /// <summary>
    /// Instantly scales the coin text up to <see cref="_pulseScale"/>, then
    /// smoothly lerps it back to its original scale over <see cref="_pulseDuration"/> seconds.
    /// </summary>
    private IEnumerator PulseText()
    {
        if (_coinText == null) yield break;

        Vector3 bigScale = _originalCoinTextScale * _pulseScale;
        _coinText.transform.localScale = bigScale;

        float elapsed = 0f;
        while (elapsed < _pulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _pulseDuration);
            _coinText.transform.localScale = Vector3.Lerp(bigScale, _originalCoinTextScale, t);
            yield return null;
        }

        _coinText.transform.localScale = _originalCoinTextScale;
        _pulseCoroutine = null;
    }

    /// <summary>
    /// Instantly scales a RectTransform up to <see cref="_healPunchScale"/>,
    /// then smoothly lerps it back to (1, 1, 1) over <see cref="_punchDuration"/> seconds.
    /// Used for the heal animation on the hearts container.
    /// </summary>
    private IEnumerator PunchScaleRoutine(RectTransform rt)
    {
        if (rt == null) yield break;

        Vector3 bigScale = new Vector3(_healPunchScale, _healPunchScale, _healPunchScale);
        rt.localScale = bigScale;

        float elapsed = 0f;
        while (elapsed < _punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _punchDuration);
            rt.localScale = Vector3.Lerp(bigScale, Vector3.one, t);
            yield return null;
        }

        rt.localScale = Vector3.one;
    }
}