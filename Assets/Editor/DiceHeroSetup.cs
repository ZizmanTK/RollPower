using System;
using System.IO;
using DiceHero;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Editor utilities: scene + player setup, builds, preview renders and batch playtests (usable from batch mode).</summary>
public static class DiceHeroSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string BaseMatPath = "Assets/Materials/Base.mat";
    public const string Version = "2.0.0";

    [MenuItem("Roll Power/Rebuild Main Scene")]
    public static void Setup()
    {
        ConfigurePlayer();
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
        cam.farClipPlane = 250f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = World.SpaceColor;
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
        Debug.Log("[RollPower] Main scene created at " + ScenePath);
    }

    /// <summary>Product identity, input axes and platform settings (safe to run repeatedly).</summary>
    [MenuItem("Roll Power/Configure Player Settings")]
    public static void ConfigurePlayer()
    {
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.productName = "Roll Power";
        PlayerSettings.companyName = "ZizmanTK";
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.zizmantk.rollpower");
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.WebGL, "com.zizmantk.rollpower");
        PlayerSettings.runInBackground = true;
        PlayerSettings.visibleInBackground = true;

        // Windows
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;

        // WebGL for itch.io: gzip with a JS decompression fallback works whatever headers the host sends.
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.template = "APPLICATION:Default";

        AddDPadAxes();
        AssetDatabase.SaveAssets();
        Debug.Log("[RollPower] player settings configured, version " + Version);
    }

    /// <summary>Adds DPadX / DPadY joystick axes (XInput axes 6 and 7) used by Controls.</summary>
    static void AddDPadAxes()
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0];
        var so = new SerializedObject(asset);
        var axes = so.FindProperty("m_Axes");
        void Add(string name, int axis)
        {
            for (int i = 0; i < axes.arraySize; i++)
                if (axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue == name) return;
            axes.arraySize++;
            var a = axes.GetArrayElementAtIndex(axes.arraySize - 1);
            a.FindPropertyRelative("m_Name").stringValue = name;
            a.FindPropertyRelative("descriptiveName").stringValue = "";
            a.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
            a.FindPropertyRelative("negativeButton").stringValue = "";
            a.FindPropertyRelative("positiveButton").stringValue = "";
            a.FindPropertyRelative("altNegativeButton").stringValue = "";
            a.FindPropertyRelative("altPositiveButton").stringValue = "";
            a.FindPropertyRelative("gravity").floatValue = 0f;
            a.FindPropertyRelative("dead").floatValue = 0.2f;
            a.FindPropertyRelative("sensitivity").floatValue = 1f;
            a.FindPropertyRelative("snap").boolValue = false;
            a.FindPropertyRelative("invert").boolValue = false;
            a.FindPropertyRelative("type").intValue = 2; // joystick axis
            a.FindPropertyRelative("axis").intValue = axis;
            a.FindPropertyRelative("joyNum").intValue = 0;
        }
        Add("DPadX", 5);
        Add("DPadY", 6);
        so.ApplyModifiedProperties();
    }

    [MenuItem("Roll Power/Build Windows")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, GetArg("-buildOut") ?? "Builds/RollPower-Windows/RollPower.exe");

    [MenuItem("Roll Power/Build WebGL")]
    public static void BuildWebGL() => Build(BuildTarget.WebGL, GetArg("-buildOut") ?? "Builds/RollPower-WebGL");

    static void Build(BuildTarget target, string path)
    {
        ConfigurePlayer();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = path,
            target = target,
            options = BuildOptions.None,
        });
        var s = report.summary;
        Debug.Log($"[RollPower] Build {target} {s.result}: {s.totalErrors} errors, {s.totalSize / (1024f * 1024f):0.0} MB, {s.totalTime.TotalSeconds:0}s -> {s.outputPath}");
        if (Application.isBatchMode && s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    /// <summary>Batch mode: builds the world in edit mode and renders the main camera to a PNG (-previewOut path).</summary>
    public static void Preview()
    {
        string outPath = GetArg("-previewOut") ?? Path.GetFullPath("preview.png");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boot = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
        boot.BuildWorld();
        RenderCamera(Camera.main, outPath, 1600, 900);
        Debug.Log("[RollPower] Preview written to " + outPath);
    }

    /// <summary>
    /// Batch mode: the autopilot plays the real wave game (upgrades picked automatically) for up to
    /// -playSeconds of game time (default 240), logging waves, bombs and the boss, and saving a frame
    /// every 2 seconds (-previewOut folder). Pass -godmode to keep the dice alive.
    /// </summary>
    public static void PlayTest()
    {
        string outDir = GetArg("-previewOut") ?? Path.GetFullPath("playtest");
        float seconds = float.TryParse(GetArg("-playSeconds"), out float ps) ? ps : 240f;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boot = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
        boot.BuildWorld();
        var loop = boot.Loop;
        var game = loop.Game;
        loop.AutoPickUpgrades = true;
        game.Invincible = Array.IndexOf(Environment.GetCommandLineArgs(), "-godmode") >= 0;
        var bot = new Autopilot(boot.Controller, loop.Weapons, game);
        var cam = Camera.main.GetComponent<CameraFollow>();
        int rolls = 0, shots = 0, lastWave = 0, frame = 0, bombs = 0;
        boot.Controller.TopChanged += (a, b) => rolls++;
        loop.Weapons.Fired += d => shots++;
        game.WaveCleared += w => Debug.Log($"[RollPower] {frame / 30f:0.0}s wave {w} cleared, hp {game.Hp}/{game.MaxHp}, score {game.Score}, bombs disposed {game.BombsDisposed}/{bombs}");

        const float dt = 1f / 30f;
        for (; frame < 30 * seconds && !game.Lost; frame++)
        {
            int before = game.Bombs.All.Count;
            bot.Step(dt);
            loop.Step(dt, true);
            if (game.Bombs.All.Count > before) bombs += game.Bombs.All.Count - before;
            if (game.Wave != lastWave)
            {
                lastWave = game.Wave;
                Debug.Log($"[RollPower] {frame / 30f:0.0}s wave {game.Wave} started{(game.BossWave ? " (BOSS)" : "")}, hp {game.Hp}");
            }
            if (frame % 60 == 0) Shot(cam, outDir, frame / 60);
        }
        Debug.Log($"[RollPower] PlayTest end after {frame / 30f:0}s: wave {game.Wave}, lost {game.Lost}, hp {game.Hp}, rolls {rolls}, shots {shots}, " +
                  $"kills {game.Kills}, bombs {bombs} (disposed {game.BombsDisposed}), best combo x{game.BestCombo}, score {game.Score}");
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
        Debug.Log("[RollPower] " + Sound.ExportWavs(dir).Replace("\n", "\n[RollPower] "));
    }

    /// <summary>Debug: renders red/green/blue lit and glowing spheres and logs their material state.</summary>
    public static void ColorTest()
    {
        string outPath = GetArg("-previewOut") ?? Path.GetFullPath("colortest.png");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var boot = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
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
            Debug.Log($"[RollPower] {glow.name}: shader {glow.shader.name}, kw [{string.Join(",", glow.shaderKeywords)}], emission {glow.GetColor("_EmissionColor")}, base {glow.GetColor("_BaseColor")}");
        }
        Debug.Log($"[RollPower] pipeline: {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name}, cam type {cam.GetType()}");
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
