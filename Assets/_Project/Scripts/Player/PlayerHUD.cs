// =============================================================================
//  PlayerHUD.cs
//  (Modificado para sistema de Corazones en cuartos)
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
    [Header("Data Sources")]
    [SerializeField] private HealthComponent playerHealth;
    [SerializeField] private PlayerResourceComponent playerResources;

    [Header("Visuales de Salud (Corazones)")]
    [Tooltip("El objeto padre que agrupa a todos los corazones (para la animación de curación).")]
    [SerializeField] private RectTransform heartsContainer;

    [Tooltip("Las imágenes individuales de cada corazón en la UI.")]
    [SerializeField] private Image[] corazonesUI;

    [Tooltip("Orden de los sprites: 0=Vacío, 1=Un cuarto, 2=Mitad, 3=Tres cuartos, 4=Lleno")]
    [SerializeField] private Sprite[] estadosCorazon;

    [Header("Bar Fill Images (Maná y Energía)")]
    [SerializeField] private Image manaBarFill;
    [SerializeField] private Image energyBarFill;

    [Header("Juice Settings")]
    [SerializeField] private float _healPunchScale = 1.2f;
    [SerializeField] private float _punchDuration = 0.15f;
    [SerializeField] private float _flashDuration = 0.15f;
    [SerializeField] private Color _errorFlashColor = Color.red;

    [Header("Wallet UI")]
    [SerializeField] private TextMeshProUGUI _coinText;
    [SerializeField] private float _pulseScale = 1.4f;
    [SerializeField] private float _pulseDuration = 0.2f;

    private PlayerWallet _wallet;
    private Coroutine _pulseCoroutine;
    private Vector3 _originalScale;

    private float _previousHealth = -1f;
    private Color _originalManaColor;
    private Color _originalEnergyColor;

    private Coroutine _healthFlash;
    private Coroutine _manaFlash;
    private Coroutine _energyFlash;

    private void Awake()
    {
        _wallet = FindObjectOfType<PlayerWallet>();

        if (_coinText != null)
        {
            _originalScale = _coinText.transform.localScale;
            _coinText.text = "0";
        }

        _originalManaColor = manaBarFill != null ? manaBarFill.color : Color.white;
        _originalEnergyColor = energyBarFill != null ? energyBarFill.color : Color.white;
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthBar;
            UpdateHealthBar(playerHealth.GetNormalizedHealth());
        }

        if (playerResources != null)
        {
            playerResources.OnManaChanged += UpdateManaBar;
            playerResources.OnEnergyChanged += UpdateEnergyBar;
            UpdateManaBar(playerResources.GetNormalizedMana());
            UpdateEnergyBar(playerResources.GetNormalizedEnergy());
        }

        PlayerCombat.OnManaDepleted += HandleManaDepleted;
        PlayerCombat.OnEnergyDepleted += HandleEnergyDepleted;
        PlayerController3D.OnEnergyDepleted += HandleEnergyDepleted;

        if (_wallet != null)
        {
            _wallet.OnCoinsChanged += HandleCoinsChanged;
            if (_coinText != null) _coinText.text = _wallet.Coins.ToString();
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= UpdateHealthBar;
        if (playerResources != null)
        {
            playerResources.OnManaChanged -= UpdateManaBar;
            playerResources.OnEnergyChanged -= UpdateEnergyBar;
        }

        PlayerCombat.OnManaDepleted -= HandleManaDepleted;
        PlayerCombat.OnEnergyDepleted -= HandleEnergyDepleted;
        PlayerController3D.OnEnergyDepleted -= HandleEnergyDepleted;

        if (_wallet != null) _wallet.OnCoinsChanged -= HandleCoinsChanged;
    }

    private void UpdateHealthBar(float normalized)
    {
        if (corazonesUI == null || corazonesUI.Length == 0 || playerHealth == null) return;

        // Calculamos los corazones basándonos en la vida exacta (números enteros)
        int vidaActual = playerHealth.CurrentHealth;

        for (int i = 0; i < corazonesUI.Length; i++)
        {
            int vidaDeEsteCorazon = vidaActual - (i * 4);

            if (vidaDeEsteCorazon >= 4) corazonesUI[i].sprite = estadosCorazon[4];
            else if (vidaDeEsteCorazon == 3) corazonesUI[i].sprite = estadosCorazon[3];
            else if (vidaDeEsteCorazon == 2) corazonesUI[i].sprite = estadosCorazon[2];
            else if (vidaDeEsteCorazon == 1) corazonesUI[i].sprite = estadosCorazon[1];
            else corazonesUI[i].sprite = estadosCorazon[0];
        }

        // Mantuvimos el "Juice" (animaciones) de tu programadora
        if (_previousHealth >= 0f)
        {
            if (normalized < _previousHealth)
            {
                if (_healthFlash != null) StopCoroutine(_healthFlash);
                _healthFlash = StartCoroutine(FlashHeartsRoutine());
            }

            if (normalized > _previousHealth && heartsContainer != null)
                StartCoroutine(PunchScaleRoutine(heartsContainer));
        }

        _previousHealth = normalized;
    }

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

    private void HandleCoinsChanged(int amount)
    {
        if (_coinText == null) return;
        _coinText.text = amount.ToString();
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = StartCoroutine(PulseText());
    }

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

    private IEnumerator FlashBarRoutine(Image bar, Color originalColor)
    {
        if (bar == null) yield break;
        bar.color = _errorFlashColor;
        yield return new WaitForSeconds(_flashDuration);
        bar.color = originalColor;
    }

    private IEnumerator FlashHeartsRoutine()
    {
        foreach (var img in corazonesUI) img.color = _errorFlashColor;
        yield return new WaitForSeconds(_flashDuration);
        foreach (var img in corazonesUI) img.color = Color.white;
    }

    private IEnumerator PunchScaleRoutine(RectTransform rt)
    {
        if (rt == null) yield break;
        rt.localScale = new Vector3(_healPunchScale, _healPunchScale, _healPunchScale);
        float elapsed = 0f;
        while (elapsed < _punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _punchDuration);
            rt.localScale = Vector3.Lerp(new Vector3(_healPunchScale, _healPunchScale, _healPunchScale), Vector3.one, t);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}