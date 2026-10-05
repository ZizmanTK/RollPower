using Unity.VectorGraphics.Editor;
using UnityEditor;

/// <summary>Imports every SVG under Resources/Icons as a 256 px texture, so IMGUI can draw it.</summary>
public class IconImport : AssetPostprocessor
{
    void OnPreprocessAsset()
    {
        if (!assetPath.EndsWith(".svg") || !assetPath.Contains("/Resources/Icons/")) return;
        if (assetImporter is SVGImporter svg)
        {
            svg.SvgType = SVGType.Texture2D;
            svg.KeepTextureAspectRatio = false;
            svg.TextureWidth = 256;
            svg.TextureHeight = 256;
            svg.SampleCount = 4;
            svg.FilterMode = UnityEngine.FilterMode.Bilinear;
        }
    }
}
