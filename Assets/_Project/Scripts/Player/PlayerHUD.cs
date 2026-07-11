// =============================================================================
//  PlayerHUD.cs
// =============================================================================
//  PURPOSE:
//    Listens to events from HealthComponent, PlayerResourceComponent, and
//    PlayerWallet and updates the on-screen HUD accordingly.
//
//    Health is displayed as a ROW OF HEARTS with fractional states (like Zelda
//    or The Binding of Isaac). All other elements (Mana bar, Energy bar, Coins)
//    continue to work exactly as before.
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
    //  Design rules:
    //    • Max HP = 100, split across 5 hearts.
    //    • Each full heart = 20 HP.
    //    • Each heart has 5 states (0 = empty … 4 = full):
    //
    //        State index | HP in this heart | Sprite name suggestion
    //        ------------|------------------|-----------------------
    //            0       |   0 HP  (empty)  | heart_empty
    //            1       |   5 HP  (1/4)    | heart_quarter
    //            2       |  10 HP  (half)   | heart_half
    //            3       |  15 HP  (3/4)    | heart_three_quarters
    //            4       |  20 HP  (full)   | heart_full
    //
    //  Constants that encode those rules:
    //    HP_PER_HEART  = 20   (total HP represented by one completely full heart)
    //    HP_PER_QUARTER = 5   (HP represented by one quarter-segment of a heart)
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
    // MANA & ENERGY BARS
    // -------------------------------------------------------------------------

    [Header("Bar Fill Images (Mana & Energy)")]
    [SerializeField] private Image manaBarFill;
    [SerializeField] private Image energyBarFill;

    // -------------------------------------------------------------------------
    // JUICE — ANIMATION SETTINGS
    // -------------------------------------------------------------------------

    [Header("Juice Settings")]
    [Tooltip("Scale multiplier applied to heartsContainer when the player is healed.")]
    [SerializeField] private float _healPunchScale = 1.2f;

    [Tooltip("How long (seconds) the punch-scale animation takes to return to normal.")]
    [SerializeField] private float _punchDuration = 0.15f;

    [Tooltip("How long (seconds) the flash color stays visible on damage.")]
    [SerializeField] private float _flashDuration = 0.15f;

    [Tooltip("The color the hearts and bars flash when taking damage or depleting a resource.")]
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
    // PRIVATE RUNTIME STATE
    // -------------------------------------------------------------------------

    private PlayerWallet _wallet;
    private Coroutine    _pulseCoroutine;
    private Vector3      _originalCoinTextScale;

    // Keeps track of the previous normalized health so we can detect
    // whether the player healed (normalized went UP) or took damage (went DOWN).
    private float _previousHealth = -1f;

    // Original bar colors are cached so the flash animation can restore them.
    private Color _originalManaColor;
    private Color _originalEnergyColor;

    // Coroutine handles let us stop a running flash/pulse before starting a new one.
    private Coroutine _healthFlash;
    private Coroutine _manaFlash;
    private Coroutine _energyFlash;

    // -------------------------------------------------------------------------
    // HEART MATH CONSTANTS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Total HP that one completely filled heart represents.
    /// (MaxHP 100 / 5 hearts = 20 HP per heart)
    /// </summary>
    private const int HP_PER_HEART = 20;

    /// <summary>
    /// HP that one quarter-segment of a heart represents.
    /// (20 HP per heart / 4 quarters = 5 HP per quarter)
    /// </summary>
    private const int HP_PER_QUARTER = 5;

    // -------------------------------------------------------------------------
    // UNITY LIFECYCLE
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Find the PlayerWallet that lives somewhere in the scene.
        _wallet = FindObjectOfType<PlayerWallet>();

        // Cache coin text state.
        if (_coinText != null)
        {
            _originalCoinTextScale = _coinText.transform.localScale;
            _coinText.text = "0";
        }

        // Cache original bar colors for the flash-then-restore animation.
        _originalManaColor   = manaBarFill   != null ? manaBarFill.color   : Color.white;
        _originalEnergyColor = energyBarFill != null ? energyBarFill.color : Color.white;
    }

    private void OnEnable()
    {
        // ── Health ──────────────────────────────────────────────────────────
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthBar;

            // Draw the initial heart state immediately so the HUD is correct
            // from the very first frame.
            UpdateHealthBar(playerHealth.GetNormalizedHealth());
        }

        // ── Resources (Mana / Energy) ────────────────────────────────────────
        if (playerResources != null)
        {
            playerResources.OnManaChanged   += UpdateManaBar;
            playerResources.OnEnergyChanged += UpdateEnergyBar;

            // Initialise bars.
            UpdateManaBar(playerResources.GetNormalizedMana());
            UpdateEnergyBar(playerResources.GetNormalizedEnergy());
        }

        // ── Depletion flash events ───────────────────────────────────────────
        PlayerCombat.OnManaDepleted             += HandleManaDepleted;
        PlayerCombat.OnEnergyDepleted           += HandleEnergyDepleted;
        PlayerController3D.OnEnergyDepleted     += HandleEnergyDepleted;

        // ── Wallet ───────────────────────────────────────────────────────────
        if (_wallet != null)
        {
            _wallet.OnCoinsChanged += HandleCoinsChanged;
            if (_coinText != null) _coinText.text = _wallet.Coins.ToString();
        }
    }

    private void OnDisable()
    {
        // Always unsubscribe to prevent memory leaks and ghost callbacks.
        if (playerHealth != null)   playerHealth.OnHealthChanged   -= UpdateHealthBar;
        if (playerResources != null)
        {
            playerResources.OnManaChanged   -= UpdateManaBar;
            playerResources.OnEnergyChanged -= UpdateEnergyBar;
        }

        PlayerCombat.OnManaDepleted             -= HandleManaDepleted;
        PlayerCombat.OnEnergyDepleted           -= HandleEnergyDepleted;
        PlayerController3D.OnEnergyDepleted     -= HandleEnergyDepleted;

        if (_wallet != null) _wallet.OnCoinsChanged -= HandleCoinsChanged;
    }

    // -------------------------------------------------------------------------
    // HEALTH HEARTS — UPDATE LOGIC
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called every time HealthComponent fires OnHealthChanged.
    /// Converts the normalised health float back to an integer HP value,
    /// then determines which of the 5 sprite states each heart should display.
    /// </summary>
    /// <param name="normalized">Health fraction 0.0 (dead) → 1.0 (full).</param>
    private void UpdateHealthBar(float normalized)
    {
        // Safety: bail out if the arrays haven't been wired up in the Inspector.
        if (corazonesUI == null || corazonesUI.Length == 0) return;
        if (estadosCorazon == null || estadosCorazon.Length < 5) return;
        if (playerHealth == null) return;

        // ── STEP 1: Get the raw integer HP from HealthComponent ──────────────
        //
        //  We read CurrentHealth directly (e.g. 65) instead of working with
        //  the normalised float, because integer math gives us exact quarter-
        //  segment boundaries without any floating-point rounding errors.
        //
        int currentHP = playerHealth.CurrentHealth;

        // ── STEP 2: Loop over every heart slot ──────────────────────────────
        for (int i = 0; i < corazonesUI.Length; i++)
        {
            // ── STEP 2a: How much HP does this heart "own"? ──────────────────
            //
            //  Each heart is responsible for a 20-HP window:
            //    Heart 0 → HP  1 – 20
            //    Heart 1 → HP 21 – 40
            //    Heart 2 → HP 41 – 60
            //    Heart 3 → HP 61 – 80
            //    Heart 4 → HP 81 – 100
            //
            //  We calculate how much HP "spills" into this heart's window by
            //  subtracting all the HP that already filled the previous hearts.
            //
            //  Example with currentHP = 65:
            //    i=0 → hpInThisHeart = 65 - (0 * 20) = 65  → clamped to 20 (Full)
            //    i=1 → hpInThisHeart = 65 - (1 * 20) = 45  → clamped to 20 (Full)
            //    i=2 → hpInThisHeart = 65 - (2 * 20) = 25  → clamped to 20 (Full)
            //    i=3 → hpInThisHeart = 65 - (3 * 20) =  5  → 5 HP → 1 quarter (1/4)
            //    i=4 → hpInThisHeart = 65 - (4 * 20) = -15 → clamped to  0 (Empty)
            //
            int hpInThisHeart = currentHP - (i * HP_PER_HEART);

            // Clamp so we never go above 20 (full) or below 0 (empty).
            hpInThisHeart = Mathf.Clamp(hpInThisHeart, 0, HP_PER_HEART);

            // ── STEP 2b: Convert HP → sprite state index ─────────────────────
            //
            //  We divide the HP amount in this heart by HP_PER_QUARTER (5),
            //  rounding DOWN (integer division), to get the state index 0–4.
            //
            //  HP in heart | HP / 5 (integer) | State
            //  ------------|------------------|-------
            //       0      |        0         | Empty        (estadosCorazon[0])
            //     1–5      |        1         | Quarter      (estadosCorazon[1])
            //     6–10     |        2         | Half         (estadosCorazon[2])
            //    11–15     |        3         | Three-Qtr   (estadosCorazon[3])
            //    16–20     |        4         | Full         (estadosCorazon[4])
            //
            //  Note: because HP_PER_HEART (20) / HP_PER_QUARTER (5) = 4, and
            //  "hpInThisHeart" is already clamped to [0, 20], the result is
            //  always in [0, 4] — exactly our five sprite indices.
            //
            int stateIndex = hpInThisHeart / HP_PER_QUARTER;

            // Apply the correct sprite to this heart's Image component.
            corazonesUI[i].sprite = estadosCorazon[stateIndex];
        }

        // ── STEP 3: JUICE — Animate on heal or damage ────────────────────────
        //
        //  _previousHealth is -1 on the very first call (initialisation),
        //  so we skip animations that frame to avoid a false "heal" flash.
        //
        if (_previousHealth >= 0f)
        {
            if (normalized < _previousHealth)
            {
                // Health went DOWN → player took damage → flash hearts red.
                if (_healthFlash != null) StopCoroutine(_healthFlash);
                _healthFlash = StartCoroutine(FlashHeartsRoutine());
            }
            else if (normalized > _previousHealth && heartsContainer != null)
            {
                // Health went UP → player was healed → punch-scale the container.
                StartCoroutine(PunchScaleRoutine(heartsContainer));
            }
        }

        // Store normalized value for the next comparison.
        _previousHealth = normalized;
    }

    // -------------------------------------------------------------------------
    // MANA & ENERGY BARS
    // -------------------------------------------------------------------------

    private void UpdateManaBar(float normalized)
    {
        if (manaBarFill != null) manaBarFill.fillAmount = normalized;
    }

    private void UpdateEnergyBar(float normalized)
    {
        if (energyBarFill != null) energyBarFill.fillAmount = normalized;
    }

    private void HandleManaDepleted()
    {
        if (manaBarFill == null) return;
        if (_manaFlash != null) StopCoroutine(_manaFlash);
        _manaFlash = StartCoroutine(FlashBarRoutine(manaBarFill, _originalManaColor));
    }

    private void HandleEnergyDepleted()
    {
        if (energyBarFill == null) return;
        if (_energyFlash != null) StopCoroutine(_energyFlash);
        _energyFlash = StartCoroutine(FlashBarRoutine(energyBarFill, _originalEnergyColor));
    }

    // -------------------------------------------------------------------------
    // WALLET / COINS
    // -------------------------------------------------------------------------

    private void HandleCoinsChanged(int newAmount)
    {
        if (_coinText == null) return;
        _coinText.text = newAmount.ToString();

        // Stop any already-running pulse so we don't have two competing routines.
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = StartCoroutine(PulseText());
    }

    // -------------------------------------------------------------------------
    // COROUTINES — JUICE ANIMATIONS
    // -------------------------------------------------------------------------

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
    /// Instantly changes a bar's colour to the error flash colour,
    /// then restores it after <see cref="_flashDuration"/> seconds.
    /// </summary>
    private IEnumerator FlashBarRoutine(Image bar, Color originalColor)
    {
        if (bar == null) yield break;
        bar.color = _errorFlashColor;
        yield return new WaitForSeconds(_flashDuration);
        bar.color = originalColor;
    }

    /// <summary>
    /// Instantly changes ALL heart images to the error flash colour,
    /// then restores them to white after <see cref="_flashDuration"/> seconds.
    /// </summary>
    private IEnumerator FlashHeartsRoutine()
    {
        foreach (var img in corazonesUI)
            if (img != null) img.color = _errorFlashColor;

        yield return new WaitForSeconds(_flashDuration);

        foreach (var img in corazonesUI)
            if (img != null) img.color = Color.white;
    }

    /// <summary>
    /// Instantly scales a RectTransform up to <see cref="_healPunchScale"/>,
    /// then smoothly lerps it back to (1, 1, 1) over <see cref="_punchDuration"/> seconds.
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