using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Resources/UI altındaki arayüz görselleri (Grease Pencil simgeleri ve panel çerçeveleri) için içe aktarma ayarı:
    /// kenarları kaymasın (Clamp), bulanıklaşmasın (mipmap yok), renkleri bozulmasın (sıkıştırma yok).
    /// </summary>
    public class ArayuzDokuAyari : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/UI/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.isReadable = false;
        }
    }
}
