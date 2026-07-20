using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Controlador central de audio del proyecto.
///
/// Reglas de Arquitectura:
/// - Singleton con DontDestroyOnLoad. Los duplicados se autodestruyen en Awake.
/// - Se suscribe a GameManager.OnStateChanged para reaccionar a los estados globales.
/// - Usa diccionarios para buscar clips en tiempo O(1) y evitar iteraciones por frame.
/// - Las transiciones de BGM usan corrutinas de crossfade para evitar cortes abruptos.
/// </summary>
public class AudioManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------
    public static AudioManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Constants — AudioMixer parameters and PlayerPrefs keys
    // -------------------------------------------------------------------------
    private const string MIXER_PARAM_MUSIC = "MusicVolume";
    private const string MIXER_PARAM_SFX = "SFXVolume";

    public const string PREF_KEY_MUSIC = "Volume_Music";
    public const string PREF_KEY_SFX = "Volume_SFX";

    private const float DEFAULT_VOLUME = 1f;

    // -------------------------------------------------------------------------
    // Serializable Mapping Structs
    // -------------------------------------------------------------------------
    [Serializable]
    public struct SFXEntry
    {
        [Tooltip("Unique string ID used to play this effect (e.g. 'sfx_laser', 'sfx_coin').")]
        public string id;
        [Tooltip("The AudioClip to play for this entry.")]
        public AudioClip clip;
    }

    [Serializable]
    public struct BGMEntry
    {
        [Tooltip("Unique string ID for this background music track (e.g. 'bgm_dungeon', 'bgm_boss').")]
        public string id;
        [Tooltip("The AudioClip for this BGM entry.")]
        public AudioClip clip;
    }

    // -------------------------------------------------------------------------
    // Inspector — Audio Mixer Channels
    // -------------------------------------------------------------------------
    [Header("Audio Mixer Groups")]
    [SerializeField] private AudioMixerGroup _musicMixerGroup;
    [SerializeField] private AudioMixerGroup _sfxMixerGroup;

    // -------------------------------------------------------------------------
    // Inspector — Sound Libraries
    // -------------------------------------------------------------------------
    [Header("Sound Libraries")]
    [SerializeField] private List<SFXEntry> _sfxLibrary = new List<SFXEntry>();
    [SerializeField] private List<BGMEntry> _bgmLibrary = new List<BGMEntry>();

    [Tooltip("Music that plays automatically on the Main Menu.")]
    [SerializeField] private AudioClip _mainMenuMusic;

    [Header("Transition Settings")]
    [SerializeField][Range(0f, 1f)] private float _musicTargetVolume = 1f;

    // -------------------------------------------------------------------------
    // Runtime — Audio Sources and Internal State
    // -------------------------------------------------------------------------
    private AudioSource _musicSource;
    private AudioSource _sfxSource;
    private Coroutine _fadeCoroutine;

    // O(1) lookup dictionaries — populated in Awake from the serialized libraries.
    private Dictionary<string, AudioClip> _sfxDict;
    private Dictionary<string, AudioClip> _bgmDict;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        // Singleton guard — destroy any duplicate that loads after the first instance.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // AudioSources are created dynamically to keep the prefab hierarchy clean.
        _musicSource = CreateAudioSource("MusicSource", _musicMixerGroup, loop: true);
        _sfxSource = CreateAudioSource("SFXSource", _sfxMixerGroup, loop: false);

        _musicSource.volume = 0f;

        BuildLookupDictionaries();
    }

    private void Start()
    {
        // Volumes are loaded in Start (one frame after Awake) to ensure the Mixer is fully initialised.
        LoadSavedVolumePreferences();

        if (_mainMenuMusic != null)
        {
            PlayMusicWithCrossfade(_mainMenuMusic);
        }
    }

    private void OnEnable()
    {
        GameManager.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleStateChanged;
    }

    // -------------------------------------------------------------------------
    // FSM Event Handler
    // -------------------------------------------------------------------------
    private void HandleStateChanged(GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.MainMenu:
                PlayMusicWithCrossfade(_mainMenuMusic);
                break;

            case GameManager.GameState.Playing:
                // Punto de extension: el WaveManager o el cargador de escena debe llamar a PlayBGM().
                break;

            case GameManager.GameState.Pause:
                // Punto de extension: bajar volumen con un filtro low-pass o mantener el BGM actual.
                break;

            case GameManager.GameState.GameOver:
                // Punto de extension: detener musica o transicionar a un track de derrota.
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Public Playback API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reproduce una musica de fondo buscandola por su ID en el diccionario de BGM.
    /// Usa crossfade suave para evitar cortes entre pistas.
    /// </summary>
    public void PlayBGM(string bgmId, float fadeDuration = 1f)
    {
        if (!_bgmDict.TryGetValue(bgmId, out AudioClip clip))
        {
            Debug.LogWarning($"[AudioManager] PlayBGM: No BGM registered with ID '{bgmId}'.");
            return;
        }

        PlayMusicWithCrossfade(clip, fadeDuration);
    }

    /// <summary>
    /// Desvanece y detiene la musica actual con una transicion suave.
    /// </summary>
    public void StopBGM(float fadeDuration = 1f)
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeOutAndStop(fadeDuration));
    }

    /// <summary>
    /// Reproduce un efecto de sonido (OneShot) buscandolo por ID. Permite solapamiento de instancias.
    /// </summary>
    public void PlaySFX(string sfxId)
    {
        if (_sfxDict.TryGetValue(sfxId, out AudioClip clip))
        {
            _sfxSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] PlaySFX: No SFX registered with ID '{sfxId}'.");
        }
    }

    // -------------------------------------------------------------------------
    // Public Options API (UI Slider mapping)
    // -------------------------------------------------------------------------
    public void SetMusicVolume(float linearValue)
    {
        ApplyVolume(MIXER_PARAM_MUSIC, PREF_KEY_MUSIC, linearValue);
    }

    public void SetSFXVolume(float linearValue)
    {
        ApplyVolume(MIXER_PARAM_SFX, PREF_KEY_SFX, linearValue);
    }

    // -------------------------------------------------------------------------
    // Private Transition and Interpolation Logic
    // -------------------------------------------------------------------------
    private void PlayMusicWithCrossfade(AudioClip clip, float fadeDuration = 1f)
    {
        if (_musicSource.clip == clip && _musicSource.isPlaying)
            return;

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        if (clip == null)
        {
            _fadeCoroutine = StartCoroutine(FadeOutAndStop(fadeDuration));
        }
        else if (!_musicSource.isPlaying)
        {
            _fadeCoroutine = StartCoroutine(FadeIn(clip, fadeDuration));
        }
        else
        {
            _fadeCoroutine = StartCoroutine(Crossfade(clip, fadeDuration));
        }
    }

    private IEnumerator Crossfade(AudioClip nextClip, float fadeDuration)
    {
        yield return StartCoroutine(FadeVolume(_musicSource.volume, 0f, fadeDuration));

        _musicSource.clip = nextClip;
        _musicSource.Play();

        yield return StartCoroutine(FadeVolume(0f, _musicTargetVolume, fadeDuration));
        _fadeCoroutine = null;
    }

    private IEnumerator FadeIn(AudioClip clip, float fadeDuration)
    {
        _musicSource.clip = clip;
        _musicSource.volume = 0f;
        _musicSource.Play();

        yield return StartCoroutine(FadeVolume(0f, _musicTargetVolume, fadeDuration));
        _fadeCoroutine = null;
    }

    private IEnumerator FadeOutAndStop(float fadeDuration)
    {
        yield return StartCoroutine(FadeVolume(_musicSource.volume, 0f, fadeDuration));

        _musicSource.Stop();
        _musicSource.clip = null;
        _fadeCoroutine = null;
    }

    private IEnumerator FadeVolume(float fromVolume, float toVolume, float duration)
    {
        if (duration <= 0f)
        {
            _musicSource.volume = toVolume;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // unscaledDeltaTime ensures fades work correctly even when Time.timeScale is 0 (Pause screen).
            elapsed += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(fromVolume, toVolume, elapsed / duration);
            yield return null;
        }
        _musicSource.volume = toVolume;
    }

    // -------------------------------------------------------------------------
    // Helpers and Initialisation
    // -------------------------------------------------------------------------
    private AudioSource CreateAudioSource(string sourceName, AudioMixerGroup mixerGroup, bool loop)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform);

        AudioSource source = child.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = mixerGroup;
        source.loop = loop;
        source.playOnAwake = false;

        return source;
    }

    private void BuildLookupDictionaries()
    {
        _sfxDict = new Dictionary<string, AudioClip>(_sfxLibrary.Count);
        foreach (SFXEntry entry in _sfxLibrary)
        {
            if (!string.IsNullOrEmpty(entry.id)) _sfxDict[entry.id] = entry.clip;
        }

        _bgmDict = new Dictionary<string, AudioClip>(_bgmLibrary.Count);
        foreach (BGMEntry entry in _bgmLibrary)
        {
            if (!string.IsNullOrEmpty(entry.id)) _bgmDict[entry.id] = entry.clip;
        }
    }

    private void ApplyVolume(string mixerParam, string prefsKey, float linearValue)
    {
        linearValue = Mathf.Clamp(linearValue, 0.0001f, 1f);
        float dB = Mathf.Log10(linearValue) * 20f;

        if (_musicMixerGroup != null && _musicMixerGroup.audioMixer != null)
        {
            _musicMixerGroup.audioMixer.SetFloat(mixerParam, dB);
        }

        PlayerPrefs.SetFloat(prefsKey, linearValue);
    }

    private void LoadSavedVolumePreferences()
    {
        float music = PlayerPrefs.GetFloat(PREF_KEY_MUSIC, DEFAULT_VOLUME);
        float sfx = PlayerPrefs.GetFloat(PREF_KEY_SFX, DEFAULT_VOLUME);

        ApplyVolume(MIXER_PARAM_MUSIC, PREF_KEY_MUSIC, music);
        ApplyVolume(MIXER_PARAM_SFX, PREF_KEY_SFX, sfx);
    }
}