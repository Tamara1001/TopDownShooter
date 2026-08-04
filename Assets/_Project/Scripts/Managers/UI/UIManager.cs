using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puente visual directo entre la FSM de GameManager y la capa de UI.
/// Escucha los cambios de estado globales y activa o desactiva los paneles correspondientes.
///
/// Reglas de Arquitectura:
/// - No contiene logica de juego ni manipula el tiempo directamente.
/// - Se comunica con el GameManager de forma unidireccional a traves de eventos.
/// </summary>
public class UIManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields — Panels
    // -------------------------------------------------------------------------
    [Header("Paneles Principales de UI")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject playingHUDPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    [Header("Paneles de Superposición")]
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Botones Especiales")]
    [Tooltip("Botón 'Continuar' en el Menú Principal. Se desactiva automáticamente cuando no existe una sesión activa (GameManager.HasActiveSession == false).")]
    [SerializeField] private Button continueButton;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------
    private void OnEnable()
    {
        GameManager.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleStateChanged;
    }

    private void Start()
    {
        // Sincronizar con el estado actual de la FSM en lugar de forzar una transición brusca al Menú Principal.
        if (GameManager.Instance != null)
        {
            HandleStateChanged(GameManager.Instance.CurrentState);
        }
        else
        {
            ShowMainMenu();
        }
    }

    // -------------------------------------------------------------------------
    // FSM Event Handler
    // -------------------------------------------------------------------------
    private void HandleStateChanged(GameManager.GameState newState)
    {
        // Limpieza: cerrar cualquier panel de superposición (por ejemplo, opciones) en cada transición de estado.
        CloseOptionsPanel();

        switch (newState)
        {
            case GameManager.GameState.MainMenu:
                ShowMainMenu();
                break;
            case GameManager.GameState.Playing:
                ShowPlayingHUD();
                break;
            case GameManager.GameState.Pause:
                ShowPause();
                break;
            case GameManager.GameState.GameOver:
                ShowGameOver();
                break;
            case GameManager.GameState.Victory:
                ShowVictory();
                break;
            default:
                Debug.LogWarning($"[UIManager] Unhandled GameState: {newState}");
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Panel Control Methods (Private)
    // -------------------------------------------------------------------------
    private void ShowMainMenu()
    {
        mainMenuPanel?.SetActive(true);
        playingHUDPanel?.SetActive(false);
        pausePanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
        victoryPanel?.SetActive(false);

        // Habilitar el botón Continuar solo cuando haya una sesión en curso a la cual regresar.
        if (continueButton != null)
            continueButton.interactable = GameManager.Instance != null &&
                                          GameManager.Instance.HasActiveSession;
    }

    private void ShowPlayingHUD()
    {
        mainMenuPanel?.SetActive(false);
        playingHUDPanel?.SetActive(true);
        pausePanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
        victoryPanel?.SetActive(false);
    }

    private void ShowPause()
    {
        mainMenuPanel?.SetActive(false);
        playingHUDPanel?.SetActive(false);
        pausePanel?.SetActive(true);
        gameOverPanel?.SetActive(false);
        victoryPanel?.SetActive(false);
    }

    private void ShowGameOver()
    {
        mainMenuPanel?.SetActive(false);
        playingHUDPanel?.SetActive(false);
        pausePanel?.SetActive(false);
        gameOverPanel?.SetActive(true);
        victoryPanel?.SetActive(false);
    }

    private void ShowVictory()
    {
        mainMenuPanel?.SetActive(false);
        playingHUDPanel?.SetActive(false);
        pausePanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
        victoryPanel?.SetActive(true);
    }

    private void CloseOptionsPanel()
    {
        optionsPanel?.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Public Button Callbacks (UI Event Triggers)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Vinculado al botón 'Jugar' o 'Nueva Partida' en el Menú Principal.
    /// </summary>
    /// <summary>
    /// Vinculado al botón 'Continuar' en el Menú Principal.
    /// Solo es interactuable cuando <see cref="GameManager.HasActiveSession"/> es verdadero.
    /// </summary>
    public void OnContinueClicked()
    {
        GameManager.Instance.ContinueGame();
    }

    public void OnPlayClicked()
    {
        GameManager.Instance.StartNewGame();
    }

    /// <summary>Vinculado al botón 'Reanudar' dentro del menú de Pausa.</summary>
    public void OnResumeButtonClicked()
    {
        GameManager.Instance.ResumeFromPause();
    }

    /// <summary>Vinculado al botón 'Reintentar' en la pantalla de Game Over.</summary>
    public void OnRestartButtonClicked()
    {
        GameManager.Instance.StartNewGame();
    }

    /// <summary>Vinculado al botón 'Volver al Menú' desde las pantallas de Pausa o Game Over.</summary>
    public void OnReturnToMenuClicked()
    {
        GameManager.Instance.ReturnToMainMenu();
    }

    /// <summary>Vinculado opcionalmente a un botón de pausa en pantalla dentro del HUD.</summary>
    public void OnPauseButtonClicked()
    {
        GameManager.Instance.ChangeState(GameManager.GameState.Pause);
    }

    public void OnOptionsClicked()
    {
        optionsPanel?.SetActive(true);
    }

    public void OnCloseOptionsClicked()
    {
        CloseOptionsPanel();
    }

    /// <summary>Vinculado al botón 'Salir' en el Menú Principal.</summary>
    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        Debug.Log("[UIManager] OnQuitClicked — Application.Quit() suppressed in Editor.");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}