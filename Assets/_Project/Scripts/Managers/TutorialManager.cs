
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using TopDownShooter.Dungeon;   // RoomSocket, DoorController

/// <summary>
/// Componente raíz del prefab Tutorial_Map.
/// Gestiona el enlace manual de sockets/puertas, el horneado del NavMesh
/// y el teletransporte seguro del jugador al spawn point del tutorial.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Nested Types
    // -------------------------------------------------------------------------

    /// <summary>
    /// Par serializable que vincula un <see cref="RoomSocket"/> con el
    /// <see cref="DoorController"/> que lo ocupa físicamente en el nivel
    /// tutorial. Rellena la función que el generador procedural realiza
    /// de forma automática en las partidas normales.
    /// </summary>
    [System.Serializable]
    public struct SocketDoorLink
    {
        [Tooltip("Socket de la sala artesanal al que pertenece la puerta.")]
        public RoomSocket Socket;

        [Tooltip("DoorController ya colocado en la escena que ocupa este socket.")]
        public DoorController Door;
    }

    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Manual Door Links")]
    [Tooltip("Lista de vínculos Socket ↔ DoorController definidos a mano para el nivel tutorial. " +
             "Reemplaza el enlace automático que realiza el DungeonGenerator en las partidas normales.")]
    [SerializeField] private List<SocketDoorLink> _doorLinks = new List<SocketDoorLink>();

    [Header("Navegación")]
    [Tooltip("Superficie de NavMesh que cubre las salas del nivel tutorial. " +
             "Debe ser horneada en runtime porque el prefab se instancia dinámicamente.")]
    [SerializeField] private NavMeshSurface _navMeshSurface;

    [Header("Spawn del Jugador")]
    [Tooltip("Transform que marca la posición y rotación inicial del jugador " +
             "dentro del nivel tutorial.")]
    [SerializeField] private Transform _playerSpawnPoint;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // ── Responsabilidad 1: Enlace manual de sockets y puertas ────────────
        // Se ejecuta en Awake para que los vínculos estén establecidos antes
        // de que cualquier Start() (propio o de las puertas) los consulte.
        ApplyDoorLinks();
    }

    private void Start()
    {
        // ── Responsabilidad 2: Horneado del NavMesh ──────────────────────────
        BakeNavMesh();

        // ── Responsabilidad 3: Posicionar al jugador ─────────────────────────
        // Se delega a una coroutine porque GameManager.Instance.PlayerTransform
        // puede no estar disponible en este frame exacto (depende del Script
        // Execution Order entre PlayerRegistration y TutorialManager).
        StartCoroutine(SetupPlayerPosition());
    }

    // -------------------------------------------------------------------------
    // Door Linking
    // -------------------------------------------------------------------------

    /// <summary>
    /// Itera la lista <see cref="_doorLinks"/> e invoca
    /// <see cref="RoomSocket.AssignDoor"/> en cada par válido.
    /// Los pares con <c>Socket</c> o <c>Door</c> nulos se omiten con una
    /// advertencia para facilitar la detección de errores de configuración.
    /// </summary>
    private void ApplyDoorLinks()
    {
        if (_doorLinks == null || _doorLinks.Count == 0)
        {
            // Lista vacía es válida: el nivel tutorial puede no tener puertas.
            Debug.Log("[TutorialManager] No hay SocketDoorLinks configurados. " +
                      "Si el nivel tiene puertas, asígnalos en el Inspector.");
            return;
        }

        int linked = 0;

        for (int i = 0; i < _doorLinks.Count; i++)
        {
            SocketDoorLink link = _doorLinks[i];

            // Validar ambos extremos del vínculo antes de operar.
            if (link.Socket == null || link.Door == null)
            {
                Debug.LogWarning($"[TutorialManager] _doorLinks[{i}] tiene un campo nulo " +
                                 $"(Socket={link.Socket}, Door={link.Door}). " +
                                 "Revisa la asignación en el Inspector.",
                                 this);
                continue; // Saltar este par y continuar con los demás
            }

            // Registrar la puerta en el socket; el socket notifica al
            // DoorController su posición para el estado inicial correcto.
            link.Socket.AssignDoor(link.Door);
            linked++;
        }

        Debug.Log($"[TutorialManager] {linked}/{_doorLinks.Count} vínculos Socket↔Door aplicados.");
    }

    // -------------------------------------------------------------------------
    // NavMesh Baking
    // -------------------------------------------------------------------------

    /// <summary>
    /// Hornea la superficie de NavMesh asignada al Inspector.
    /// Centralizado en su propio método para facilitar pruebas unitarias
    /// y depuración independiente de la lógica de spawn.
    /// </summary>
    private void BakeNavMesh()
    {
        if (_navMeshSurface == null)
        {
            // Sin superficie asignada los enemigos no pueden navegar,
            // pero no es un error fatal que deba detener la ejecución.
            Debug.LogWarning("[TutorialManager] _navMeshSurface no está asignado. " +
                             "Los agentes de IA no podrán navegar en el tutorial.",
                             this);
            return;
        }

        Debug.Log("[TutorialManager] Horneando NavMesh para el nivel tutorial...");
        _navMeshSurface.BuildNavMesh();
        Debug.Log("[TutorialManager] NavMesh horneado correctamente.");
    }

    // -------------------------------------------------------------------------
    // Player Teleportation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Coroutine que espera hasta que el jugador esté registrado en
    /// <see cref="GameManager"/> y luego lo teletransporta al punto de spawn.
    /// <para>
    /// La espera es necesaria porque <c>PlayerRegistration.Start()</c> (en el
    /// prefab del jugador) puede ejecutarse un frame después que este Start(),
    /// dependiendo del Script Execution Order del proyecto.
    /// </para>
    /// </summary>
    private IEnumerator SetupPlayerPosition()
    {
        // ── Validar el spawn point antes de esperar innecesariamente ─────────
        if (_playerSpawnPoint == null)
        {
            Debug.LogError("[TutorialManager] _playerSpawnPoint no está asignado. " +
                           "El jugador no podrá ser reposicionado en el tutorial.",
                           this);
            yield break;
        }

        // ── Esperar a que GameManager y el jugador estén disponibles ─────────
        // El bucle cede un frame por iteración (yield return null),
        // garantizando que no se bloquea el hilo principal.
        while (GameManager.Instance == null || GameManager.Instance.PlayerTransform == null)
        {
            yield return null; // Esperar al siguiente frame
        }

        // ── Teletransporte seguro ─────────────────────────────────────────────
        Transform playerTransform = GameManager.Instance.PlayerTransform;

        // El NavMeshAgent rechaza los cambios de posición directos mientras
        // está activo. Se deshabilita momentáneamente para permitir el salto.
        NavMeshAgent agent = null;
        bool agentWasEnabled = false;

        if (playerTransform.TryGetComponent(out agent))
        {
            agentWasEnabled = agent.enabled;

            if (agentWasEnabled)
            {
                agent.enabled = false; // Desactivar temporalmente para evitar conflictos
            }
        }

        // Mover el jugador a la posición y orientación del spawn point.
        playerTransform.SetPositionAndRotation(
            _playerSpawnPoint.position,
            _playerSpawnPoint.rotation);

        // Re-habilitar el agente en su nuevo origen una vez reposicionado.
        if (agent != null && agentWasEnabled)
        {
            agent.enabled = true;  // El agente re-muestrea el NavMesh desde la nueva posición
        }

        Debug.Log($"[TutorialManager] Jugador '{playerTransform.name}' teletransportado " +
                  $"a '{_playerSpawnPoint.name}' → {_playerSpawnPoint.position}.");
    }
}
