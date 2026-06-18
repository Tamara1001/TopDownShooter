using UnityEngine;

public class IdleConsumable : MonoBehaviour
{
    [Header("Flotación (Arriba - Abajo)")]
    public float floatAmplitude = 0.25f; // Qué tanto sube y baja
    public float floatSpeed = 2f;        // Qué tan rápido flota

    [Header("Rotación (Giro sobre su eje)")]
    // Si tu juego es 2D, normalmente rotarás en Z (ej. 0, 0, 100).
    // Si es 3D top-down, normalmente rotarás en Y (ej. 0, 100, 0).
    public Vector3 rotationSpeed = new Vector3(0f, 100f, 0f);

    [Header("Squash & Stretch (Aplastarse y Estirarse)")]
    public float squashAmount = 0.1f; // Qué tanto se deforma
    public float squashSpeed = 4f;    // Qué tan rápido se deforma

    private Vector3 startPos;
    private Vector3 startScale;

    void Start()
    {
        // Guardamos la posición y escala iniciales al spawnear el objeto
        startPos = transform.localPosition;
        startScale = transform.localScale;
    }

    void Update()
    {
        // 1. FLOTACIÓN
        // Usamos Mathf.Sin para crear un movimiento suave de vaivén basado en el tiempo
        float newY = startPos.y + (Mathf.Sin(Time.time * floatSpeed) * floatAmplitude);
        transform.localPosition = new Vector3(startPos.x, newY, startPos.z);

        // 2. ROTACIÓN
        // Giramos el objeto constantemente según la velocidad asignada
        transform.Rotate(rotationSpeed * Time.deltaTime);

        // 3. SQUASH & STRETCH
        // Para que se vea natural, cuando se estira hacia arriba (Y), 
        // debe aplastarse a los lados (X y Z), y viceversa.
        float stretch = Mathf.Sin(Time.time * squashSpeed) * squashAmount;
        transform.localScale = new Vector3(
            startScale.x - stretch,
            startScale.y + stretch,
            startScale.z - stretch
        );
    }
}