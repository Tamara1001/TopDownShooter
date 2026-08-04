
using UnityEngine;

/// <summary>
/// Componente ligero adjunto al prefab del Jugador.
/// Registra y desregistra el <see cref="Transform"/> del jugador
/// en el <see cref="GameManager"/> para que todos los sistemas (EnemyBrain,
/// minimapa, plataformas de cámara) puedan obtener una referencia canónica al
/// jugador que sea independiente de su orden de aparición.
/// </summary>
public class PlayerRegistration : MonoBehaviour
{
    // ----------------------------------------------------------
    // CICLO DE VIDA DE UNITY
    // ----------------------------------------------------------

    /// <summary>
    /// Publica este Transform en el GameManager lo antes posible
    /// (Awake se ejecuta antes de Start en todos los demás scripts en el mismo frame).
    ///
    /// Utiliza una protección contra nulos en <see cref="GameManager.Instance"/> para que el
    /// script se degrade suavemente en escenas de prueba aisladas que no tienen
    /// GameManager — se registra una advertencia pero nada se rompe.
    /// </summary>
    private void Awake()
    {
        if (GameManager.Instance == null)
        {
            // Degradación suave: la búsqueda de Nivel 2 (por etiqueta) de EnemyBrain
            // seguirá encontrando al jugador de forma normal.
            Debug.LogWarning("[PlayerRegistration] GameManager.Instance is null. " +
                             "Player will NOT be registered centrally. " +
                             "Add a GameManager to the scene for full multi-system support.");
            return;
        }

        GameManager.Instance.RegisterPlayer(transform);
    }

    /// <summary>
    /// Limpia la referencia central del jugador cuando este GameObject se
    /// destruye permanentemente (por ejemplo, fin del juego sin reaparición inmediata).
    ///
    /// NOTA SOBRE POOLING: Si el jugador se desactiva (se devuelve al pool) en lugar de
    /// destruirse, reemplace esto con una llamada explícita a UnregisterPlayer()
    /// en la llamada de retorno de liberación de su pool para mantener la línea de tiempo predecible.
    /// </summary>
    private void OnDestroy()
    {
        // Guardia: solo desregistrar si NOSOTROS somos el jugador registrado actualmente.
        // Esto evita que un jugador recién reaparecido sea desregistrado
        // por el OnDestroy de la instancia antigua que se dispara un frame más tarde.
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.PlayerTransform == transform)
        {
            GameManager.Instance.UnregisterPlayer();
        }
    }
}
