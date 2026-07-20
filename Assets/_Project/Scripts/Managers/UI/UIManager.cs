using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Direct visual bridge between the GameManager FSM and the UI layer.
/// Listens to global state changes and activates or deactivates the corresponding panels.
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
    [Header("Main UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject playingHUDPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    [Header("Overlay Panels")]
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Special Buttons")]
    [Tooltip("'Continue' button on the Main Menu. Automatically disabled " +
             "when no active session exists (GameManager.HasActiveSession == false).")]
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
        // Sync to the current FSM state instead of forcing a hard transition to Main Menu.
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
        // Cleanup: close any overlay panels (e.g. options) on every state transition.
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

        // Enable the Continue button only when there is an ongoing session to return to.
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
    /// Bound to the 'Play' or 'New Game' button on the Main Menu.
    /// </summary>
    /// <summary>
    /// Bound to the 'Continue' button on the Main Menu.
    /// Only interactable when <see cref="GameManager.HasActiveSession"/> is true.
    /// </summary>
    public void OnContinueClicked()
    {
        GameManager.Instance.ContinueGame();
    }

    public void OnPlayClicked()
    {
        GameManager.Instance.StartNewGame();
    }

    /// <summary>Bound to the 'Resume' button inside the Pause menu.</summary>
    public void OnResumeButtonClicked()
    {
        GameManager.Instance.ResumeFromPause();
    }

    /// <summary>Bound to the 'Retry' button on the Game Over screen.</summary>
    public void OnRestartButtonClicked()
    {
        GameManager.Instance.StartNewGame();
    }

    /// <summary>Bound to the 'Return to Menu' button from Pause or Game Over screens.</summary>
    public void OnReturnToMenuClicked()
    {
        GameManager.Instance.ReturnToMainMenu();
    }

    /// <summary>Optionally bound to an on-screen pause button inside the HUD.</summary>
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

    /// <summary>Bound to the 'Quit' button on the Main Menu.</summary>
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