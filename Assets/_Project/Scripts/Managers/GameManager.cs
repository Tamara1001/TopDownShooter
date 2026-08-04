using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using TopDownShooter.Dungeon;   // DungeonGenerator

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
        GameOver,
        Victory
    }

    /// <summary>Estado actual del juego. Solo puede ser modificado internamente.</summary>
    public GameState CurrentState { get; private set; }

    // -------------------------------------------------------------------------
    // Modo de Juego
    // -------------------------------------------------------------------------

    /// <summary>
    /// Distingue entre una partida procedural estándar (Normal) y
    /// el nivel tutorial prediseñado a mano (Tutorial).
    /// </summary>
    public enum GameMode { Normal, Tutorial }

    /// <summary>
    /// Modo activo en la sesión actual. Solo GameManager puede escribirlo.
    /// Los demás sistemas lo leen para adaptar su comportamiento.
    /// </summary>
    public GameMode CurrentMode { get; private set; } = GameMode.Normal;

    // -------------------------------------------------------------------------
    // Eventos
    // -------------------------------------------------------------------------
    /// <summary>
    /// Se dispara cada vez que el estado cambia.
    /// UIManager, AudioManager y WaveManager deben suscribirse acá.
    /// </summary>
    public static event Action<GameState> OnStateChanged;

    /// <summary>
    /// Se dispara cada vez que se llama a <see cref="RegisterPlayer"/>, incluyendo al
    /// reaparecer. Los sistemas que necesiten una referencia inmediata al jugador
    /// (por ejemplo, EnemyBrain en resolución de Tier-3) pueden suscribirse aquí en lugar
    /// de realizar encuestas en cada frame.
    /// </summary>
    public static event Action<Transform> OnPlayerRegistered;

    // -------------------------------------------------------------------------
    // Prefabs
    // -------------------------------------------------------------------------

    [Header("Prefabs")]
    [Tooltip("Prefab del mapa del Tutorial. Se instancia en la raíz de la escena " +
             "cuando el modo activo es Tutorial. Debe contener un TutorialManager en su raíz.")]
    [SerializeField] private GameObject _tutorialMapPrefab;

    // -------------------------------------------------------------------------
    // Variables Internas
    // -------------------------------------------------------------------------

    // Temporizador de la partida. Solo avanza durante el estado 'Playing'.
    private float _sessionTimer;

    // Guarda el estado en el que estábamos antes de pausar (útil si hay estados extra luego).
    private GameState _stateBeforePause;

    // Bandera establecida por StartNewGame() antes de recargar la escena para que OnSceneLoaded
    // sepa que debe inicializar una nueva sesión una vez que la nueva escena esté lista.
    private bool _pendingRestart = false;

    // -------------------------------------------------------------------------
    // Session State
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verdadero mientras exista una partida activa en la escena cargada.
    /// Se establece en verdadero dentro de <see cref="OnSceneLoaded"/> después de reiniciar la escena,
    /// y en falso cuando finaliza la sesión (GameOver o Victory).
    /// Utilizado por <see cref="UIManager"/> para habilitar/deshabilitar el botón "Continuar".
    /// </summary>
    public bool HasActiveSession { get; private set; }

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Disparado por Unity después de que se completa la carga de cada escena, incluyendo la recarga
    /// iniciada por <see cref="StartNewGame"/>.
    /// Cuando se establece <see cref="_pendingRestart"/>, este es el momento seguro más temprano
    /// para inicializar el estado del juego, ya que todos los objetos de la escena están completamente despiertos.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_pendingRestart) return;

        _pendingRestart  = false;
        _sessionTimer    = 0f;
        HasActiveSession = true;

        // Asegurarse de que el tiempo corra antes de transmitir el estado Playing.
        Time.timeScale = 1f;
        ChangeState(GameState.Playing);

        Debug.Log("[GameManager] Scene fully loaded — new session started.");

        // ── Verificación del modo de juego ────────────────────────────────────
        if (CurrentMode == GameMode.Normal)
        {
            // Modo Normal: disparar la generación procedural del calabozo.
            DungeonGenerator generator = FindAnyObjectByType<DungeonGenerator>();
            if (generator != null)
            {
                generator.Generate();
            }
            else
            {
                Debug.LogWarning("[GameManager] Normal mode active but no DungeonGenerator " +
                                 "was found in the scene. Is the GameObject present?");
            }
        }
        else if (CurrentMode == GameMode.Tutorial)
        {
            // Modo Tutorial: instanciar el mapa prediseñado a mano en el origen de la escena.
            // TutorialManager (en la raíz del prefab) se encarga del NavMesh y del spawn.
            if (_tutorialMapPrefab != null)
            {
                Instantiate(_tutorialMapPrefab, Vector3.zero, Quaternion.identity);
                Debug.Log("[GameManager] Tutorial mode active — Tutorial_Map instantiated.");
            }
            else
            {
                Debug.LogWarning("[GameManager] Tutorial mode active but _tutorialMapPrefab " +
                                 "is not assigned. Drag the Tutorial_Map prefab into the Inspector.");
            }
        }
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
            Debug.LogWarning($"[GameManager] Attempted to change to the current state ({newState}). Ignored.");
            return;
        }

        // --- Manejo del TimeScale ---
        // Pausar en Pause, GameOver, o Victory; reanudar para todo lo demás.
        switch (newState)
        {
            case GameState.Pause:
                _stateBeforePause = CurrentState;
                Time.timeScale = 0f;
                break;
            case GameState.GameOver:
            case GameState.Victory:
                HasActiveSession = false;
                Time.timeScale = 0f;
                break;
            default:
                Time.timeScale = 1f;
                break;
        }

        // --- Transición ---
        GameState previous = CurrentState;
        CurrentState = newState;

        Debug.Log($"[GameManager] State Change: {previous} → {CurrentState}");

        // Dispara el evento para que los demás scripts reaccionen
        OnStateChanged?.Invoke(CurrentState);
    }

    /// <summary>
    /// Inicia una nueva partida desde cero.
    /// Ideal para llamar desde el botón "Jugar" en el Main Menu o "Reintentar" en Game Over.
    /// </summary>
    public void StartNewGame()
    {
        // Establecer el modo Normal antes de recargar la escena para que
        // OnSceneLoaded pueda disparar la generación procedural correctamente.
        CurrentMode = GameMode.Normal;

        // Asegurar que el tiempo corra durante la carga de escena para que
        // la lógica de activación asíncrona de Unity no quede bloqueada.
        Time.timeScale = 1f;

        // Marcar el reinicio ANTES de LoadScene para que OnSceneLoaded se
        // dispare correctamente incluso en escenas que cargan en un solo frame.
        _pendingRestart = true;

        Debug.Log("[GameManager] Reloading scene for a new game (Normal mode)...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Inicia una partida en modo Tutorial, cargando el nivel prediseñado.
    /// La generación procedural queda desactivada en esta sesión.
    /// Ideal para llamarlo desde el botón "Tutorial" en el Menú Principal.
    /// </summary>
    public void StartTutorial()
    {
        // Cambiar al modo Tutorial ANTES de recargar para que OnSceneLoaded
        // sepa que debe omitir el DungeonGenerator.
        CurrentMode = GameMode.Tutorial;

        // Garantizar que el tiempo corra durante la carga de escena.
        Time.timeScale = 1f;

        // Marcar el reinicio ANTES de LoadScene (mismo flujo que StartNewGame).
        _pendingRestart = true;

        Debug.Log("[GameManager] Reloading scene for Tutorial mode...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Reanuda una partida en progreso desde el Menú Principal sin recargar la escena.
    /// Solo es válido cuando <see cref="HasActiveSession"/> es verdadero.
    /// </summary>
    public void ContinueGame()
    {
        if (!HasActiveSession)
        {
            Debug.LogWarning("[GameManager] ContinueGame called with no active session. Ignored.");
            return;
        }

        ChangeState(GameState.Playing);
    }

    /// <summary>
    /// Vuelve al estado guardado antes de pausar.
    /// </summary>
    public void ResumeFromPause()
    {
        if (CurrentState != GameState.Pause)
        {
            Debug.LogWarning("[GameManager] ResumeFromPause called, but the game is not paused.");
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

    // -------------------------------------------------------------------------
    // Player Registry (FIX-2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// El Transform del jugador actual, registrado en tiempo de ejecución por
    /// <see cref="PlayerRegistration"/> a través de <see cref="RegisterPlayer"/>.
    /// <para>
    /// Solo lectura para todos los sistemas externos. Solo <see cref="RegisterPlayer"/>
    /// y <see cref="UnregisterPlayer"/> pueden escribir este valor, asegurando
    /// una referencia única y autoritativa que sobrevive a las recargas de escena,
    /// reapariciones y orden de spawn arbitrario de enemigos.
    /// </para>
    /// </summary>
    public Transform PlayerTransform { get; private set; }

    /// <summary>
    /// Llamado por <see cref="PlayerRegistration"/> (adjunto al prefab del jugador)
    /// en Awake/Start para publicar el Transform del jugador.
    /// Es seguro llamarlo múltiples veces: reaparecer con una nueva instancia simplemente
    /// reemplaza la referencia anterior y dispara <see cref="OnPlayerRegistered"/>
    /// nuevamente para que todos los suscriptores (EnemyBrain, minimapa, etc.) se actualicen.
    /// </summary>
    /// <param name="player">El Transform raíz del jugador. No debe ser nulo.</param>
    public void RegisterPlayer(Transform player)
    {
        if (player == null)
        {
            Debug.LogError("[GameManager] RegisterPlayer called with a null Transform. " +
                           "Check the PlayerRegistration component.");
            return;
        }

        PlayerTransform = player;
        Debug.Log($"[GameManager] Player registered: '{player.name}'.");

        // Notificar a todos los suscriptores (por ejemplo, las corrutinas EnemyBrain.WaitForPlayer)
        // que una referencia válida del jugador ya está disponible.
        OnPlayerRegistered?.Invoke(PlayerTransform);
    }

    /// <summary>
    /// Llamado cuando el jugador es eliminado permanentemente (fin de la partida, sin reaparición).
    /// Limpia la referencia para que los enemigos regresen al estado inactivo de forma segura.
    /// </summary>
    public void UnregisterPlayer()
    {
        if (PlayerTransform == null)
        {
            Debug.LogWarning("[GameManager] UnregisterPlayer called but no player was registered.");
            return;
        }

        Debug.Log($"[GameManager] Player '{PlayerTransform.name}' unregistered.");
        PlayerTransform = null;
    }
}