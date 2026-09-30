using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Creates the URP pipeline asset and the post-processing profile (bloom etc.) used by the game.</summary>
public static class UrpSetup
{
    const string Dir = "Assets/Settings";
    const string RendererPath = Dir + "/URP_Renderer.asset";
    const string AssetPath = Dir + "/URP_Pipeline.asset";
    public const string ProfilePath = Dir + "/PostFX.asset";

    public static void Configure()
    {
        Directory.CreateDirectory(Dir);

        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);
        }
        if (rendererData.postProcessData == null)
        {
            // Without this the renderer silently skips all post-processing (no bloom). The getter is internal.
            var getter = typeof(PostProcessData).GetMethod("GetDefaultPostProcessData",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            rendererData.postProcessData = getter?.Invoke(null, null) as PostProcessData;
            EditorUtility.SetDirty(rendererData);
            Debug.Log("[DiceHero] post-process data assigned: " + (rendererData.postProcessData != null));
        }

        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
        if (asset == null)
        {
            asset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        asset.supportsHDR = true;
        asset.msaaSampleCount = 4;
        asset.shadowDistance = 45f;
        asset.shadowCascadeCount = 2;
        asset.maxAdditionalLightsCount = 8;
        // All materials are created from script at runtime; with the SRP Batcher on, batch-mode renders
        // showed every material with the first one's colours. The scene is small, so per-draw binding is fine.
        asset.useSRPBatcher = false;
        EditorUtility.SetDirty(asset);

        GraphicsSettings.defaultRenderPipeline = asset;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = asset;
        }
        QualitySettings.SetQualityLevel(current, false);

        BuildProfile();
        AssetDatabase.SaveAssets();
        Debug.Log("[DiceHero] URP configured");
    }

    static void BuildProfile()
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) != null) AssetDatabase.DeleteAsset(ProfilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(1.7f);
        bloom.threshold.Override(1.0f);
        bloom.scatter.Override(0.72f);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.35f);
        color.contrast.Override(18f);
        color.saturation.Override(22f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.33f);
        vignette.smoothness.Override(0.45f);

        var chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0.05f);

        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
    }

    /// <summary>Adds a global post-processing volume to the open scene and enables post FX on the camera.</summary>
    public static void AddToScene(Camera cam)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        var go = new GameObject("PostFX Volume");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;

        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    }
}
