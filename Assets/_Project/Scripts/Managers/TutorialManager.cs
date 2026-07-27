// =============================================================================
//  TutorialManager.cs
//  Project : TopDownShooter – Tutorial System
//
//  PROPÓSITO
//  ---------
//  Componente raíz del prefab Tutorial_Map. Tiene exactamente dos
//  responsabilidades, sin solapamiento con otros sistemas:
//
//  1. HORNEADO DEL NAVMESH
//     El prefab se instancia dinámicamente en runtime, por lo que la
//     superficie de navegación no puede quedar pre-horneada en el Editor.
//     Start() dispara BuildNavMesh() para que los enemigos del tutorial
//     puedan navegar correctamente desde el primer frame útil.
//
//  2. TELETRANSPORTE SEGURO DEL JUGADOR
//     El jugador se registra en GameManager un frame después (según el orden
//     de ejecución), por lo que se usa una Coroutine que espera hasta que
//     la referencia esté disponible antes de moverlo al punto de spawn.
//     Se deshabilita temporalmente el NavMeshAgent para evitar que el
//     componente rechace el reposicionamiento instantáneo.
//
//  USO
//  ----
//  Adjuntar al GameObject raíz del prefab Tutorial_Map.
//  Asignar _navMeshSurface y _playerSpawnPoint en el Inspector.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Componente raíz del prefab Tutorial_Map.
/// Hornea el NavMesh en runtime y teletransporta al jugador al spawn point
/// del tutorial de forma segura, respetando el orden de inicialización.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

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

    private void Start()
    {
        // ── Responsabilidad 1: Horneado del NavMesh ──────────────────────────
        BakeNavMesh();

        // ── Responsabilidad 2: Posicionar al jugador ─────────────────────────
        // Se delega a una coroutine porque GameManager.Instance.PlayerTransform
        // puede no estar disponible en este frame exacto (depende del Script
        // Execution Order entre PlayerRegistration y TutorialManager).
        StartCoroutine(SetupPlayerPosition());
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
