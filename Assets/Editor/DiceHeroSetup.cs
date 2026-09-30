using System;
using System.IO;
using DiceHero;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Editor utilities: creates the main scene and renders preview screenshots (usable from batch mode).</summary>
public static class DiceHeroSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string BaseMatPath = "Assets/Materials/Base.mat";

    [MenuItem("Dice Hero/Rebuild Main Scene")]
    public static void Setup()
    {
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.productName = "Dice Hero";

        UrpSetup.Configure();

        Directory.CreateDirectory("Assets/Materials");
        Directory.CreateDirectory("Assets/Scenes");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>(BaseMatPath);
        if (baseMat == null || baseMat.shader != lit)
        {
            if (baseMat != null) AssetDatabase.DeleteAsset(BaseMatPath);
            baseMat = new Material(lit) { name = "Base" };
            AssetDatabase.CreateAsset(baseMat, BaseMatPath);
        }

        const string glowPath = "Assets/Materials/Glow.mat";
        var glowMat = AssetDatabase.LoadAssetAtPath<Material>(glowPath);
        if (glowMat == null || glowMat.shader != lit)
        {
            if (glowMat != null) AssetDatabase.DeleteAsset(glowPath);
            glowMat = new Material(lit) { name = "Glow" };
            glowMat.EnableKeyword("_EMISSION");
            glowMat.SetColor("_EmissionColor", Color.cyan * 2f);
            glowMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(glowMat, glowPath);
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<CameraFollow>();
        UrpSetup.AddToScene(cam);

        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;

        var game = new GameObject("Game");
        var boot = game.AddComponent<GameBootstrap>();
        boot.baseMaterial = baseMat;
        boot.glowMaterial = glowMat;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[DiceHero] Main scene created at " + ScenePath);
    }

    [MenuItem("Dice Hero/Build Windows Player")]
    public static void BuildWindows()
    {
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Windows/DiceHero.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        Debug.Log($"[DiceHero] Build {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.outputPath}");
    }

    /// <summary>Batch mode: builds the world in edit mode and renders the main camera to a PNG (-previewOut path).</summary>
    public static void Preview()
    {
        string outPath = GetArg("-previewOut") ?? Path.GetFullPath("preview.png");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boot = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
        boot.BuildWorld();
        RenderCamera(Camera.main, outPath, 1600, 900);
        Debug.Log("[DiceHero] Preview written to " + outPath);
    }

    /// <summary>
    /// Batch mode: the autopilot plays the real wave game for up to two minutes of game time,
    /// logging rolls, waves and health, and saving a frame every 2 seconds (-previewOut folder).
    /// </summary>
    public static void PlayTest()
    {
        string outDir = GetArg("-previewOut") ?? Path.GetFullPath("playtest");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boot = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
        boot.BuildWorld();
        var loop = boot.Loop;
        var game = loop.Game;
        var bot = new Autopilot(boot.Controller, loop.Weapons, game);
        var cam = Camera.main.GetComponent<CameraFollow>();
        int rolls = 0, shots = 0, lastWave = 0, frame = 0;
        boot.Controller.TopChanged += (a, b) => { rolls++; Debug.Log($"[DiceHero] {frame / 30f:0.0}s roll {a}->{b} ({WeaponDef.All[b].name}), wave {game.Wave}, enemies {game.Enemies.Count}"); };
        loop.Weapons.Fired += d => shots++;

        const float dt = 1f / 30f;
        for (; frame < 30 * 180 && !game.Lost; frame++)
        {
            bot.Step(dt);
            loop.Step(dt, true);
            if (game.Wave != lastWave) { lastWave = game.Wave; Debug.Log($"[DiceHero] {frame / 30f:0.0}s wave {game.Wave} started, hp {game.Hp}"); }
            if (frame % 60 == 0) Shot(cam, outDir, frame / 60);
        }
        Debug.Log($"[DiceHero] PlayTest end after {frame / 30f:0}s: wave {game.Wave}, lost {game.Lost}, hp {game.Hp}, rolls {rolls}, shots {shots}, score {game.Score}");
    }

    static void Shot(CameraFollow cam, string dir, int frame)
    {
        cam.SnapToTarget();
        RenderCamera(cam.GetComponent<Camera>(), Path.Combine(dir, $"f{frame:000}.png"), 640, 360);
    }

    /// <summary>Batch mode: synthesizes all sounds + music and writes them as WAV files (-previewOut folder).</summary>
    public static void ExportSounds()
    {
        string dir = GetArg("-previewOut") ?? Path.GetFullPath("Audio");
        Debug.Log("[DiceHero] " + Sound.ExportWavs(dir).Replace("\n", "\n[DiceHero] "));
    }

    /// <summary>Debug: renders red/green/blue lit and glowing spheres and logs their material state.</summary>
    public static void ColorTest()
    {
        string outPath = GetArg("-previewOut") ?? Path.GetFullPath("colortest.png");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boot = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
        var pal = new Palette(boot.baseMaterial, boot.glowMaterial);
        var cam = Camera.main;
        cam.transform.position = new Vector3(0f, 1f, -6f);
        cam.transform.rotation = Quaternion.identity;
        new GameObject("L").AddComponent<Light>().type = LightType.Directional;
        Color[] cols = { Color.red, Color.green, Color.blue };
        for (int i = 0; i < 3; i++)
        {
            var lit = pal.Get("T" + i, cols[i], 0.5f);
            var glow = pal.Glow("G" + i, cols[i], 3f);
            Prim.Make(PrimitiveType.Sphere, "Lit" + i, null, new Vector3(-2f + i * 2f, 2f, 0f), Vector3.one, lit);
            Prim.Make(PrimitiveType.Sphere, "Glow" + i, null, new Vector3(-2f + i * 2f, 0f, 0f), Vector3.one, glow);
            Debug.Log($"[DiceHero] {glow.name}: shader {glow.shader.name}, kw [{string.Join(",", glow.shaderKeywords)}], emission {glow.GetColor("_EmissionColor")}, base {glow.GetColor("_BaseColor")}");
        }
        Debug.Log($"[DiceHero] pipeline: {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name}, cam type {cam.GetType()}");
        RenderCamera(cam, outPath, 800, 450);
    }

    public static void RenderCamera(Camera cam, string path, int w, int h)
    {
        ShaderUtil.allowAsyncCompilation = false; // render real shaders, not async-compile placeholders
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
        if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
        else
        {
            cam.targetTexture = rt;
            cam.Render();
        }
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
    }

    static string GetArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
