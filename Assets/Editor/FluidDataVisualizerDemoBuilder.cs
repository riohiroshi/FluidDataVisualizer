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
    private const float GroundY = -0.52f;
    private const float SphereDiameter = 5.35f;
    private static readonly Vector3 SphereCenter = new Vector3(0f, GroundY + SphereDiameter * 0.5f, 0f);

    public static void BuildDemo()
    {
        ConfigureRenderPipeline();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "FluidDataVisualizerDemo";

        var camera = CreateCamera();
        CreateLight();
        CreateCityscape();
        CreateSphereDistrict();
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
        cameraObject.transform.position = new Vector3(0f, 4.35f, -15.8f);
        cameraObject.transform.LookAt(new Vector3(0f, 1.55f, 0f));

        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.005f, 0.006f, 0.012f, 1f);
        camera.fieldOfView = 31f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.allowHDR = true;
        camera.allowMSAA = false;
        return camera;
    }

    private static void CreateLight()
    {
        var lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.42f;

        var glowObject = new GameObject("Sphere Glow Light");
        glowObject.transform.position = SphereCenter;

        var glow = glowObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.38f, 0.72f, 1f);
        glow.intensity = 6.5f;
        glow.range = 13.5f;
    }

    private static void CreateCityscape()
    {
        var cityObject = new GameObject("CityGenerator");
        var city = cityObject.AddComponent<CityscapeGenerator>();
        city.ApplyDemoPreset();
        city.Generate();
    }

    private static void CreateSphereDistrict()
    {
        var roadMaterial = CreateSceneMaterial("Sphere District Road", new Color(0.015f, 0.016f, 0.019f, 1f), false);
        var cyanMaterial = CreateSceneMaterial("Sphere District Cyan Lights", new Color(0.05f, 0.75f, 1f, 1f), true);
        var amberMaterial = CreateSceneMaterial("Sphere District Amber Lights", new Color(1f, 0.58f, 0.16f, 1f), true);

        CreateRing("Sphere Plaza Road", 3.0f, 3.78f, GroundY + 0.012f, roadMaterial);
        CreateRing("Sphere Plaza Cyan Rim", 3.78f, 3.86f, GroundY + 0.018f, cyanMaterial);
        CreateRing("Sphere Plaza Inner Glow", 2.9f, 2.98f, GroundY + 0.02f, amberMaterial);

        CreateRoadStrip("North Strip Road", new Vector3(0f, GroundY + 0.014f, 8.7f), new Vector3(0.7f, 0.02f, 9.4f), roadMaterial);
        CreateRoadStrip("South Strip Road", new Vector3(0f, GroundY + 0.014f, -8.7f), new Vector3(0.7f, 0.02f, 9.4f), roadMaterial);
        CreateRoadStrip("East Strip Road", new Vector3(8.7f, GroundY + 0.014f, 0f), new Vector3(9.4f, 0.02f, 0.7f), roadMaterial);
        CreateRoadStrip("West Strip Road", new Vector3(-8.7f, GroundY + 0.014f, 0f), new Vector3(9.4f, 0.02f, 0.7f), roadMaterial);

        CreateRoadStrip("North Cyan Lane", new Vector3(0f, GroundY + 0.028f, 8.7f), new Vector3(0.08f, 0.025f, 9.4f), cyanMaterial);
        CreateRoadStrip("East Amber Lane", new Vector3(8.7f, GroundY + 0.03f, 0f), new Vector3(9.4f, 0.025f, 0.08f), amberMaterial);

        CreateSkylineLandmarks(roadMaterial, cyanMaterial, amberMaterial);
    }

    private static GameObject CreateDisplayObject()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            throw new FileNotFoundException($"Could not find material at {MaterialPath}.");
        }

        var display = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        display.name = "Interactive Fluid Sphere";
        display.transform.position = SphereCenter;
        display.transform.rotation = Quaternion.Euler(45f, 90f, 0f);
        display.transform.localScale = Vector3.one * SphereDiameter;

        var renderer = display.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        var sphereCollider = display.GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            Object.DestroyImmediate(sphereCollider);
        }

        var meshCollider = display.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = display.GetComponent<MeshFilter>().sharedMesh;

        var visualizer = display.AddComponent<FluidDataVisualizer>();
        visualizer.rippleSpeed = 0.75f;
        visualizer.maxRippleStrength = 0.045f;
        visualizer.rippleFrequency = 12f;
        visualizer.rippleRingWidth = 0.055f;
        visualizer.rippleDecay = 0.1f;

        return display;
    }

    private static Material CreateSceneMaterial(string name, Color color, bool unlit)
    {
        var shader = unlit ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        var material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color);
            material.EnableKeyword("_EMISSION");
        }

        return material;
    }

    private static void CreateRing(string name, float innerRadius, float outerRadius, float y, Material material)
    {
        const int segments = 144;
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);

            vertices[i * 2] = new Vector3(cos * outerRadius, y, sin * outerRadius);
            vertices[i * 2 + 1] = new Vector3(cos * innerRadius, y, sin * innerRadius);

            int nextOuter = ((i + 1) % segments) * 2;
            int nextInner = nextOuter + 1;
            int tri = i * 6;

            triangles[tri] = i * 2;
            triangles[tri + 1] = i * 2 + 1;
            triangles[tri + 2] = nextOuter;
            triangles[tri + 3] = i * 2 + 1;
            triangles[tri + 4] = nextInner;
            triangles[tri + 5] = nextOuter;
        }

        var mesh = new Mesh { name = $"{name} Mesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateRoadStrip(string name, Vector3 position, Vector3 scale, Material material)
    {
        CreateBox(name, position, scale, material);
    }

    private static void CreateSkylineLandmarks(Material buildingMaterial, Material cyanMaterial, Material amberMaterial)
    {
        CreateLandmarkTower(
            "West Skyline Tower",
            new Vector3(-8.1f, GroundY + 1.55f, 5.2f),
            new Vector3(1.45f, 3.1f, 0.55f),
            buildingMaterial,
            amberMaterial);

        CreateLandmarkTower(
            "East Skyline Tower",
            new Vector3(8.2f, GroundY + 1.35f, 5.8f),
            new Vector3(1.7f, 2.7f, 0.55f),
            buildingMaterial,
            amberMaterial);

        CreateVerticalRing("Observation Wheel Rim", new Vector3(-9.5f, 1.45f, 2.3f), 1.16f, 1.24f, cyanMaterial);
        for (int i = 0; i < 8; i++)
        {
            CreateBox(
                $"Observation Wheel Spoke {i}",
                new Vector3(-9.5f, 1.45f, 2.29f),
                new Vector3(2.28f, 0.035f, 0.035f),
                cyanMaterial,
                Quaternion.Euler(0f, 0f, i * 22.5f));
        }
    }

    private static void CreateLandmarkTower(string name, Vector3 position, Vector3 scale, Material buildingMaterial, Material lightMaterial)
    {
        CreateBox(name, position, scale, buildingMaterial);

        float frontZ = position.z - scale.z * 0.5f - 0.018f;
        for (int i = -2; i <= 2; i++)
        {
            CreateBox(
                $"{name} Vertical Light {i}",
                new Vector3(position.x + i * scale.x * 0.18f, position.y, frontZ),
                new Vector3(0.035f, scale.y * 0.82f, 0.035f),
                lightMaterial);
        }

        CreateBox(
            $"{name} Crown Light",
            new Vector3(position.x, position.y + scale.y * 0.42f, frontZ),
            new Vector3(scale.x * 0.85f, 0.045f, 0.035f),
            lightMaterial);
    }

    private static void CreateVerticalRing(string name, Vector3 center, float innerRadius, float outerRadius, Material material)
    {
        const int segments = 96;
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);

            vertices[i * 2] = center + new Vector3(cos * outerRadius, sin * outerRadius, 0f);
            vertices[i * 2 + 1] = center + new Vector3(cos * innerRadius, sin * innerRadius, 0f);

            int nextOuter = ((i + 1) % segments) * 2;
            int nextInner = nextOuter + 1;
            int tri = i * 6;

            triangles[tri] = i * 2;
            triangles[tri + 1] = i * 2 + 1;
            triangles[tri + 2] = nextOuter;
            triangles[tri + 3] = i * 2 + 1;
            triangles[tri + 4] = nextInner;
            triangles[tri + 5] = nextOuter;
        }

        var mesh = new Mesh { name = $"{name} Mesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateBox(string name, Vector3 position, Vector3 scale, Material material)
    {
        CreateBox(name, position, scale, material, Quaternion.identity);
    }

    private static void CreateBox(string name, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
    {
        var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = name;
        strip.transform.position = position;
        strip.transform.rotation = rotation;
        strip.transform.localScale = scale;
        strip.GetComponent<MeshRenderer>().sharedMaterial = material;

        var collider = strip.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
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
            antiAliasing = 1
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
