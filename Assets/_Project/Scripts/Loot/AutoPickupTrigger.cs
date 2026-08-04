
using UnityEngine;

namespace TopDownShooter.Loot
{
    /// <summary>
    /// Detecta la entrada del disparador (trigger) del jugador y delega la recolección al
    /// <see cref="ICollectible"/> que se encuentra en este GameObject o en sus hijos.
    /// Centraliza estrictamente la destrucción del objeto tras una recolección exitosa.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class AutoPickupTrigger : MonoBehaviour
    {
        // ----------------------------------------------------------
        // ESTADO PRIVADO
        // ----------------------------------------------------------

        /// <summary>
        /// Referencia almacenada en caché de la estrategia ICollectible en este
        /// GameObject o cualquiera de sus hijos. Se resuelve una vez en Awake.
        /// </summary>
        private ICollectible _collectible;

        // ----------------------------------------------------------
        // CICLO DE VIDA DE UNITY
        // ----------------------------------------------------------

        /// <summary>
        /// Resuelve y almacena en caché la estrategia <see cref="ICollectible"/>.
        /// Registra un error si no se encuentra ninguna para que el problema sea inmediatamente
        /// visible en la Consola en lugar de fallar silenciosamente en tiempo de ejecución.
        /// </summary>
        private void Awake()
        {
            // Buscar primero en este GameObject y luego en todos los hijos.
            // Esto permite que la lógica del objeto recolectable viva en un objeto hijo
            // (por ejemplo, una malla visual) sin romper la arquitectura.
            _collectible = GetComponent<ICollectible>() ?? GetComponentInChildren<ICollectible>();

            if (_collectible == null)
            {
                Debug.LogError(
                    $"[AutoPickupTrigger] No ICollectible found on '{gameObject.name}' " +
                    $"or its children. This pickup will be inert. " +
                    $"Add a CoinCollectible, HealthCollectible, or custom ICollectible component.",
                    gameObject
                );
            }
        }

        // ----------------------------------------------------------
        // DETECCIÓN DE DISPARADORES (TRIGGER)
        // ----------------------------------------------------------

        /// <summary>
        /// Llamado por el motor de física de Unity cuando otro Collider entra en
        /// este volumen de disparador. Si el objeto que ingresa tiene la etiqueta "Player"
        /// y se almacena en caché una estrategia <see cref="ICollectible"/> válida,
        /// ejecuta la recolección y luego destruye este GameObject.
        /// </summary>
        /// <param name="other">The Collider that entered the trigger.</param>
        private void OnTriggerEnter(Collider other)
        {
            // Guardia de salida temprana: solo procesar colisiones con el jugador.
            if (!other.CompareTag("Player")) return;

            // Guardia de salida temprana: no hacer nada si la configuración falló en Awake.
            if (_collectible == null) return;

            // Delegar el efecto de recolección a la estrategia concreta.
            // La implementación de ICollectible es responsable únicamente de
            // aplicar su efecto. NO debe llamar a Destroy por sí misma.
            _collectible.Collect(other.gameObject);

            // Destrucción estrictamente centralizada: este es el ÚNICO lugar
            // en todo el sistema de botín donde se llama a Destroy en el
            // objeto de recolección. Todas las implementaciones de ICollectible deben omitir Destroy.
            Destroy(gameObject);
        }
    }
}
