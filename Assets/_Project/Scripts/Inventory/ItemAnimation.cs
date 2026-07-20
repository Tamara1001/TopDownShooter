using UnityEngine;

/// <summary>
/// Anima items del mundo con flotacion senoidal, rotacion continua y deformacion
/// de squash-and-stretch para dar vida a los pickups en escena.
/// Pensado para consumibles, monedas y cualquier objeto recogible.
/// </summary>
public class ItemFloatAnimation : MonoBehaviour
{
    [Header("Float (Up - Down)")]
    public float floatAmplitude = 0.25f;
    public float floatSpeed = 2f;

    [Header("Rotation (Spin on axis)")]
    // Para top-down 3D se rota en Y. Cambiar a Z para proyectos 2D.
    public Vector3 rotationSpeed = new Vector3(0f, 100f, 0f);

    [Header("Squash and Stretch")]
    public float squashAmount = 0.1f;
    public float squashSpeed = 4f;

    private Vector3 _startPos;
    private Vector3 _startScale;

    private void Start()
    {
        _startPos   = transform.localPosition;
        _startScale = transform.localScale;
    }

    private void Update()
    {
        // Flotacion: movimiento senoidal suave en el eje Y.
        float newY = _startPos.y + (Mathf.Sin(Time.time * floatSpeed) * floatAmplitude);
        transform.localPosition = new Vector3(_startPos.x, newY, _startPos.z);

        transform.Rotate(rotationSpeed * Time.deltaTime);

        // Squash y Stretch: al estirarse en Y se aplana en X y Z, y viceversa,
        // imitando la deformacion de materiales elasticos.
        float stretch = Mathf.Sin(Time.time * squashSpeed) * squashAmount;
        transform.localScale = new Vector3(
            _startScale.x - stretch,
            _startScale.y + stretch,
            _startScale.z - stretch
        );
    }
}
