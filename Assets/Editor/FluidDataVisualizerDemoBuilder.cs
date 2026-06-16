using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FluidDataVisualizerDemoBuilder
{
    private const string ScenePath = "Assets/FluidDataVisualizer/Scenes/FluidDataVisualizerDemo.unity";
    private const string MaterialPath = "Assets/FluidDataVisualizer/Materials/Custom_XorFluidRaymarching.mat";
    private const string PipelinePath = "Assets/Settings/URP/Universal Render Pipeline Asset.asset";
    private const string ScreenshotPath = "Documentation/fluid-data-visualizer-demo.png";

    public static void BuildDemo()
    {
        ConfigureRenderPipeline();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "FluidDataVisualizerDemo";

        var camera = CreateCamera();
        CreateLight();
        var display = CreateDisplayObject();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        CaptureScreenshot(camera, display);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ConfigureRenderPipeline()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            Debug.LogWarning($"Could not find URP asset at {PipelinePath}.");
            return;
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
    }

    private static Camera CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -2.85f);
        cameraObject.transform.rotation = Quaternion.identity;

        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.005f, 0.006f, 0.012f, 1f);
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.allowHDR = true;
        return camera;
    }

    private static void CreateLight()
    {
        var lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.75f;
    }

    private static GameObject CreateDisplayObject()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            throw new FileNotFoundException($"Could not find material at {MaterialPath}.");
        }

        var display = GameObject.CreatePrimitive(PrimitiveType.Quad);
        display.name = "Interactive Fluid Surface";
        display.transform.position = Vector3.zero;
        display.transform.localScale = new Vector3(3.6f, 2.05f, 1f);

        var renderer = display.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        var visualizer = display.AddComponent<FluidDataVisualizer>();
        visualizer.rippleSpeed = 0.75f;
        visualizer.maxRippleStrength = 0.08f;
        visualizer.rippleFrequency = 16f;
        visualizer.rippleRingWidth = 0.055f;
        visualizer.rippleDecay = 0.55f;

        return display;
    }

    private static void CaptureScreenshot(Camera camera, GameObject display)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));

        var renderer = display.GetComponent<MeshRenderer>();
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetVector("_RippleCenterUV", new Vector4(0.54f, 0.52f, 0f, 0f));
        block.SetVector("_RippleParams", new Vector4(0.25f, 0.13f, 24f, 0.065f));
        renderer.SetPropertyBlock(block);

        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 2
        };

        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;

        camera.targetTexture = texture;
        RenderTexture.active = texture;
        camera.Render();

        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(ScreenshotPath, image.EncodeToPNG());

        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;

        Object.DestroyImmediate(image);
        texture.Release();
        Object.DestroyImmediate(texture);
    }
}
