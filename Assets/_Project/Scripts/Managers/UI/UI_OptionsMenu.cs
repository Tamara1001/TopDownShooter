using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla los sliders de volumen dentro del panel de Opciones.
///
/// Responsabilidades:
/// - En el Start(), lee los valores guardados de PlayerPrefs para establecer la posición
///   visual de cada slider de modo que coincida con los ajustes de la última sesión.
/// - Registra oyentes onValueChanged que reenvían el nuevo valor del slider
///   a AudioManager.Instance.SetXxxVolume(), el cual maneja la conversión logarítmica
///   y la persistencia en PlayerPrefs.
///
/// Configuración:
/// 1. Adjunte este script al GameObject raíz del panel de Opciones (o a un hijo).
/// 2. Arrastre los tres Sliders de UI a los campos del Inspector que se muestran a continuación.
/// 3. Cada Slider DEBE tener Min Value = 0.0001 y Max Value = 1.
///    (Un Min Value de exactamente 0 produciría -Infinito dB mediante Log10).
///
/// Reglas de arquitectura (context.md):
/// - Sin referencias directas al AudioMixer — toda la lógica de volumen está centralizada
///   en AudioManager, manteniendo este script puramente como una vinculación de UI.
/// - Sin corrutinas, sin DOTween, sin cambios en Time.timeScale.
/// - Utiliza las constantes públicas PREF_KEY de AudioManager para leer PlayerPrefs,
///   evitando cadenas mágicas duplicadas.
/// </summary>
public class UI_OptionsMenu : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Campos del Inspector — arrastre los componentes Slider correspondientes aquí
    // -------------------------------------------------------------------------

    [Header("Sliders de Volumen")]
    [Tooltip("Referencia al componente Slider de volumen de Música.\nEl Min Value debe ser 0.0001, el Max Value debe ser 1.")]
    [SerializeField] private Slider _musicSlider;

    [Tooltip("Referencia al componente Slider de volumen de SFX.\nEl Min Value debe ser 0.0001, el Max Value debe ser 1.")]
    [SerializeField] private Slider _sfxSlider;


    // -------------------------------------------------------------------------
    // Ciclo de Vida de Unity
    // -------------------------------------------------------------------------

    /// <summary>
    /// Inicializa las posiciones de los sliders a partir de las preferencias guardadas y conecta
    /// los oyentes onValueChanged.
    ///
    /// Importante: los oyentes se añaden DESPUÉS de establecer .value para que la asignación inicial
    /// no dispare una llamada SetXxxVolume() redundante — el AudioManager ya aplicó estos valores durante su propio Awake().
    /// </summary>
    private void Start()
    {
        // --- Leer preferencias guardadas (por defecto 1 = volumen máximo) -------------
        float savedMusic = PlayerPrefs.GetFloat(AudioManager.PREF_KEY_MUSIC, 1f);
        float savedSFX   = PlayerPrefs.GetFloat(AudioManager.PREF_KEY_SFX,   1f);

        // --- Establecer visuales de sliders para que coincidan con los valores guardados --------------------
        // Listeners are not connected yet, so this won't fire SetXxxVolume().
        if (_musicSlider != null)
            _musicSlider.value = savedMusic;

        if (_sfxSlider != null)
            _sfxSlider.value = savedSFX;

        // --- Registrar oyentes DESPUÉS de la asignación de valor inicial ----------------
        // Each listener simply forwards the float to the AudioManager singleton.
        if (_musicSlider != null)
            _musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);

        if (_sfxSlider != null)
            _sfxSlider.onValueChanged.AddListener(OnSFXSliderChanged);

    }

    /// <summary>
    /// Elimina todos los oyentes añadidos por este script cuando el GameObject es
    /// destruido, previniendo llamadas de retorno fantasma si el AudioManager sobrevive
    /// a este elemento de UI (lo cual hará, gracias a DontDestroyOnLoad).
    /// </summary>
    private void OnDestroy()
    {
        if (_musicSlider != null)
            _musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);

        if (_sfxSlider != null)
            _sfxSlider.onValueChanged.RemoveListener(OnSFXSliderChanged);

    }

    // -------------------------------------------------------------------------
    // Slider Callbacks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Llamado cada vez que cambia el valor del slider de Música.
    /// Reenvía el valor lineal [0.0001, 1] al AudioManager.
    /// </summary>
    /// <param name="value">Nuevo valor del slider.</param>
    private void OnMusicSliderChanged(float value)
    {
        AudioManager.Instance.SetMusicVolume(value);
    }

    /// <summary>
    /// Llamado cada vez que cambia el valor del slider de SFX.
    /// Reenvía el valor lineal [0.0001, 1] al AudioManager.
    /// </summary>
    /// <param name="value">Nuevo valor del slider.</param>
    private void OnSFXSliderChanged(float value)
    {
        AudioManager.Instance.SetSFXVolume(value);
    }

    /// <summary>
    /// Llamado cada vez que cambia el valor del slider de Voz.
    /// Reenvía el valor lineal [0.0001, 1] al AudioManager.
    /// </summary>
    /// <param name="value">Nuevo valor del slider.</param>
}
