#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace IsoCore.Foundation.Editor
{
    sealed class AtmosphereTextureImporter : AssetPostprocessor
    {
        const string AssetPrefix = "Assets/Resources/VFX/Atmosphere/Alenia/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AssetPrefix, System.StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.None;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 16384;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
#endif
