using UnityEngine;

/// <summary>
/// Puente visual directo entre la FSM (Máquina de Estados) del GameManager y la capa de la Interfaz de Usuario (UI).
/// Escucha los cambios de estado globales para activar o desactivar los paneles correspondientes.
/// 
/// Reglas de Arquitectura:
/// - No contiene lógica dura de juego ni manipula el tiempo directamente.
/// - Se comunica con el GameManager de forma unidireccional a través de eventos.
/// </summary>
public class UIManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Campos del Inspector — Paneles Base
    // -------------------------------------------------------------------------
    [Header("Paneles Principales de la UI")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject playingHUDPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Paneles Superpuestos (Overlays)")]
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject pausePanel;

    // -------------------------------------------------------------------------
    // Ciclo de Vida de Unity
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
        // Aseguramos que el juego arranque mostrando únicamente el menú principal
        ShowMainMenu();
    }

    // -------------------------------------------------------------------------
    // Manejador de Eventos de la FSM
    // -------------------------------------------------------------------------
    private void HandleStateChanged(GameManager.GameState newState)
    {
        // Limpieza: Cerramos pantallas superpuestas (como opciones) ante cualquier cambio de estado
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
            default:
                Debug.LogWarning($"[UIManager] GameState no contemplado: {newState}");
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Métodos de Control de Paneles (Privados)
    // -------------------------------------------------------------------------
    private void ShowMainMenu()
    {
        mainMenuPanel?.SetActive(true);
        playingHUDPanel?.SetActive(false);
        pausePanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
    }

    private void ShowPlayingHUD()
    {
        mainMenuPanel?.SetActive(false);
        playingHUDPanel?.SetActive(true);
        pausePanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
    }

    private void ShowPause()
    {
        mainMenuPanel?.SetActive(false);
        playingHUDPanel?.SetActive(false);
        pausePanel?.SetActive(true);
        gameOverPanel?.SetActive(false);
    }

    private void ShowGameOver()
    {
        mainMenuPanel?.SetActive(false);
        playingHUDPanel?.SetActive(false);
        pausePanel?.SetActive(false);
        gameOverPanel?.SetActive(true);
    }

    private void CloseOptionsPanel()
    {
        optionsPanel?.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Callbacks Públicos para Botones (UI Event Triggers)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Vinculado al botón "Jugar" o "Nueva Partida" del Menú Principal.
    /// </summary>
    public void OnPlayClicked()
    {
        GameManager.Instance.StartNewGame();
    }

    /// <summary>
    /// Vinculado al botón "Reanudar" dentro del menú de Pausa.
    /// </summary>
    public void OnResumeButtonClicked()
    {
        GameManager.Instance.ResumeFromPause();
    }

    /// <summary>
    /// Vinculado al botón "Reintentar" de la pantalla de Game Over.
    /// </summary>
    public void OnRestartButtonClicked()
    {
        GameManager.Instance.StartNewGame();
    }

    /// <summary>
    /// Vinculado al botón "Volver al Menú" desde Pausa o Game Over.
    /// </summary>
    public void OnReturnToMenuClicked()
    {
        GameManager.Instance.ReturnToMainMenu();
    }

    /// <summary>
    /// Vinculado opcionalmente a un botón de pausa en pantalla dentro del HUD.
    /// </summary>
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

    /// <summary>
    /// Vinculado al botón "Salir" en el Menú Principal.
    /// </summary>
    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        Debug.Log("[UIManager] OnQuitClicked — Application.Quit() suprimido en el Editor.");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}