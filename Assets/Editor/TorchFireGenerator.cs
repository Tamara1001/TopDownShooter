using UnityEngine;
using UnityEditor;

public class TorchFireGenerator
{
    [MenuItem("Tools/Dungeon/Create Torch Fire Particles")]
    public static void CreateTorchFire()
    {
        // 1. Material Generation
        string folderPath = "Assets/GeneratedAssets";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "GeneratedAssets");
        }

        string materialPath = folderPath + "/TorchFireMaterial.mat";
        Material fireMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        
        if (fireMaterial == null)
        {
            Shader urpParticleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpParticleUnlit == null)
            {
                Debug.LogError("URP Particle Unlit shader not found. Ensure URP is installed.");
                return;
            }

            fireMaterial = new Material(urpParticleUnlit);
            
            // Configure URP Particle Material properties
            fireMaterial.SetFloat("_Surface", 1); // Transparent
            fireMaterial.SetFloat("_Blend", 2); // Additive
            fireMaterial.SetColor("_EmissionColor", Color.white * 2.0f); // HDR Emission
            
            // Set keywords
            fireMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fireMaterial.EnableKeyword("_BLENDMODE_ADD");
            fireMaterial.EnableKeyword("_EMISSION");
            
            // Set Render Queue to Transparent
            fireMaterial.renderQueue = 3000;

            AssetDatabase.CreateAsset(fireMaterial, materialPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created Torch Fire Material at " + materialPath);
        }

        // 2. Particle System Creation
        GameObject torchObj = new GameObject("TorchFire_Particles");
        ParticleSystem ps = torchObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = torchObj.GetComponent<ParticleSystemRenderer>();
        
        psr.material = fireMaterial;

        // 3. Module Configurations
        
        // Main Module
        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        
        // Note: ParticleSystem rotation properties in script use radians.
        main.startRotation = new ParticleSystem.MinMaxCurve(-180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        // Emission Module
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 25f;

        // Shape Module
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.05f;

        // Color over Lifetime Module
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 1f, 0.8f), 0.0f),  // White/Bright Yellow
                new GradientColorKey(new Color(1f, 0.8f, 0f), 0.4f),  // Intense Yellow
                new GradientColorKey(new Color(1f, 0.4f, 0f), 0.7f),  // Orange
                new GradientColorKey(new Color(0.5f, 0f, 0f), 1.0f)   // Dark Red
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.1f),
                new GradientAlphaKey(1.0f, 0.7f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        // Size over Lifetime Module
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0.0f, 1.0f),
            new Keyframe(1.0f, 0.0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

        // Rotation over Lifetime Module
        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        // Rotation over lifetime in script also uses radians.
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(45f * Mathf.Deg2Rad);

        // Select the object in the editor
        Selection.activeGameObject = torchObj;
        
        // Mark scene as dirty so the new object is saved if we are in the editor
        if (!Application.isPlaying)
        {
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.isDirty == false && activeScene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        Debug.Log("Torch Fire Particle System created successfully.");
    }
}
