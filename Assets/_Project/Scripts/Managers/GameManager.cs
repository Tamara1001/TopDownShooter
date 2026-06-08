using System;
using UnityEngine;

/// <summary>
/// Cerebro central del Top-Down Shooter. Maneja la Máquina de Estados Finita (FSM)
/// y transmite los cambios de estado a todos los sistemas (UI, Audio, Spawners) mediante eventos.
///
/// Reglas de Arquitectura:
/// - Singleton con DontDestroyOnLoad.
/// - Ningún otro script puede cambiar Time.timeScale directamente. Todo pasa por acá.
/// - No contiene lógica de UI. La UI debe suscribirse a OnStateChanged para mostrar/ocultar paneles.
/// </summary>
public class GameManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------
    public static GameManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // FSM (Finite State Machine)
    // -------------------------------------------------------------------------
    public enum GameState
    {
        MainMenu,
        Playing,
        Pause,
        GameOver
    }

    /// <summary>Estado actual del juego. Solo puede ser modificado internamente.</summary>
    public GameState CurrentState { get; private set; }

    // -------------------------------------------------------------------------
    // Eventos
    // -------------------------------------------------------------------------
    /// <summary>
    /// Se dispara cada vez que el estado cambia.
    /// UIManager, AudioManager y WaveManager deben suscribirse acá.
    /// </summary>
    public static event Action<GameState> OnStateChanged;

    // -------------------------------------------------------------------------
    // Variables Internas
    // -------------------------------------------------------------------------

    // Temporizador de la partida. Solo avanza durante el estado 'Playing'.
    private float _sessionTimer;

    // Guarda el estado en el que estábamos antes de pausar (útil si hay estados extra luego).
    private GameState _stateBeforePause;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        // Protección del Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Estado inicial explícito para evitar lecturas de valores nulos al arrancar.
        CurrentState = GameState.MainMenu;
    }

    private void Update()
    {
        // El tiempo de sesión solo avanza si estamos jugando activamente.
        if (CurrentState == GameState.Playing)
        {
            _sessionTimer += Time.deltaTime;
        }
    }

    // -------------------------------------------------------------------------
    // API Pública de Control de Estados
    // -------------------------------------------------------------------------

    /// <summary>
    /// Transiciona la FSM a un nuevo estado y notifica a los suscriptores.
    /// También maneja el congelamiento del tiempo al pausar.
    /// </summary>
    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState)
        {
            Debug.LogWarning($"[GameManager] Intento de cambiar al estado actual ({newState}). Ignorado.");
            return;
        }

        // --- Manejo del TimeScale ---
        if (newState == GameState.Pause)
        {
            _stateBeforePause = CurrentState;
            Time.timeScale = 0f; // Congela el juego
        }
        else if (CurrentState == GameState.Pause)
        {
            Time.timeScale = 1f; // Descongela el juego
        }

        // --- Transición ---
        GameState previous = CurrentState;
        CurrentState = newState;

        Debug.Log($"[GameManager] Cambio de Estado: {previous} → {CurrentState}");

        // Dispara el evento para que los demás scripts reaccionen
        OnStateChanged?.Invoke(CurrentState);
    }

    /// <summary>
    /// Inicia una nueva partida desde cero.
    /// Ideal para llamar desde el botón "Jugar" en el Main Menu o "Reintentar" en Game Over.
    /// </summary>
    public void StartNewGame()
    {
        _sessionTimer = 0f;
        Time.timeScale = 1f;
        ChangeState(GameState.Playing);
        Debug.Log("[GameManager] Nueva partida iniciada. Temporizador reseteado.");
    }

    /// <summary>
    /// Vuelve al estado guardado antes de pausar.
    /// </summary>
    public void ResumeFromPause()
    {
        if (CurrentState != GameState.Pause)
        {
            Debug.LogWarning("[GameManager] ResumeFromPause llamado, pero el juego no está pausado.");
            return;
        }

        ChangeState(_stateBeforePause);
    }

    /// <summary>
    /// Devuelve el juego al Menú Principal y limpia el entorno.
    /// </summary>
    public void ReturnToMainMenu()
    {
        _sessionTimer = 0f;
        Time.timeScale = 1f;
        ChangeState(GameState.MainMenu);
    }

    // -------------------------------------------------------------------------
    // Accesos Públicos
    // -------------------------------------------------------------------------

    /// <summary>
    /// Devuelve los segundos transcurridos en la partida actual.
    /// </summary>
    public float SessionTime => _sessionTimer;
}