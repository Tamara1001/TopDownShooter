using UnityEngine;
using System.Collections;

/// <summary>
/// Singleton simple para manejar un panel negro de transición.
/// </summary>
public class UI_ScreenFader : MonoBehaviour
{
    public static UI_ScreenFader Instance { get; private set; }

    [Tooltip("El CanvasGroup asociado a la imagen negra que tapa la pantalla")]
    [SerializeField] private CanvasGroup _fadeGroup;

    private void Awake()
    {
        Instance = this;
        // Nos aseguramos de que arranque transparente y sin bloquear clics
        if (_fadeGroup != null)
        {
            _fadeGroup.alpha = 0f;
            _fadeGroup.blocksRaycasts = false;
        }
    }

    public void FadeTo(float targetAlpha, float duration)
    {
        if (_fadeGroup == null) return;

        // Bloqueamos clics si estamos oscureciendo la pantalla
        _fadeGroup.blocksRaycasts = targetAlpha > 0f;

        StopAllCoroutines();
        StartCoroutine(FadeRoutine(targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        float startAlpha = _fadeGroup.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            _fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }

        _fadeGroup.alpha = targetAlpha;
    }
}