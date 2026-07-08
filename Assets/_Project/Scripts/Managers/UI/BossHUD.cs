// =============================================================================
// BossHUD.cs
// -----------------------------------------------------------------------------
// PURPOSE:
//   Manages the visual presentation of the Boss's health and name.
//   Fades in smoothly when a boss is encountered and fades out on death.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDownShooter.Combat;

namespace TopDownShooter.Managers.UI
{
    public class BossHUD : MonoBehaviour
    {
        public static BossHUD Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _healthFill;
        [SerializeField] private TextMeshProUGUI _bossNameText;

        [Header("Settings")]
        [SerializeField] private float _fadeDuration = 1f;

        private HealthComponent _currentBossHealth;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        public void ShowBossUI(string bossName, HealthComponent bossHealth)
        {
            if (_canvasGroup == null || _healthFill == null || _bossNameText == null || bossHealth == null) return;

            _currentBossHealth = bossHealth;

            _bossNameText.text = bossName;
            _healthFill.fillAmount = 1f;

            _currentBossHealth.OnHealthChanged += UpdateFill;
            _currentBossHealth.OnDied += HideBossUI;

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeCanvasGroup(1f));
        }

        private void UpdateFill(float normalized)
        {
            if (_healthFill != null)
            {
                _healthFill.fillAmount = normalized;
            }
        }

        private void HideBossUI()
        {
            if (_currentBossHealth != null)
            {
                _currentBossHealth.OnHealthChanged -= UpdateFill;
                _currentBossHealth.OnDied -= HideBossUI;
                _currentBossHealth = null;
            }

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f));
        }

        private IEnumerator FadeCanvasGroup(float targetAlpha)
        {
            float startAlpha = _canvasGroup.alpha;
            float time = 0f;

            while (time < _fadeDuration)
            {
                time += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / _fadeDuration);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
        }
    }
}
