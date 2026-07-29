using UnityEngine;

[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    public float minIntensity = 1.5f;
    public float maxIntensity = 2.5f;
    public float flickerSpeed = 3.0f;

    private Light torchLight;
    private float randomOffset;

    void Start()
    {
        torchLight = GetComponent<Light>();
        
        // Generate a random offset so multiple torches don't flicker perfectly in sync
        randomOffset = Random.Range(0.0f, 100.0f);
    }

    void Update()
    {
        // Calculate noise value using PerlinNoise based on time and our random offset.
        // Using 1D noise (y=0) is more stable in WebGL with large Time.time values.
        float noise = Mathf.PerlinNoise((Time.time + randomOffset) * flickerSpeed, 0f);
        
        // Interpolate between min and max intensity based on the noise value
        torchLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }
}
