using System.IO;
using UnityEditor;
using UnityEngine;

namespace JumpJump.EditorTools
{
    /// <summary>
    /// 플레이스홀더 스프라이트를 PNG 에셋으로 굽습니다.
    /// 나중에 진짜 아트가 나오면 같은 파일을 덮어쓰기만 하면 됩니다.
    /// </summary>
    public static class JumpJumpArtGenerator
    {
        public const string ArtFolder = "Assets/JumpJump/Art";
        public const string RoundPath = ArtFolder + "/ui_round.png";
        public const string CirclePath = ArtFolder + "/ui_circle.png";
        public const string BackdropPath = ArtFolder + "/bg_gradient.png";

        const int RoundSize = 96;
        const int RoundRadius = 24;

        [MenuItem("Tools/JumpJump/Regenerate Placeholder Art")]
        public static void RegenerateMenu()
        {
            Generate(force: true);
            Debug.Log("[JumpJump] 플레이스홀더 아트를 다시 만들었습니다.");
        }

        public static void Generate(bool force)
        {
            Directory.CreateDirectory(ArtFolder);

            if (force || !File.Exists(RoundPath))
            {
                WritePng(RoundPath, RoundedRect(RoundSize, RoundSize, RoundRadius), RoundSize, RoundSize);
                ImportSprite(RoundPath, new Vector4(RoundRadius, RoundRadius, RoundRadius, RoundRadius));
            }

            if (force || !File.Exists(CirclePath))
            {
                WritePng(CirclePath, RoundedRect(RoundSize, RoundSize, RoundSize * 0.5f), RoundSize, RoundSize);
                ImportSprite(CirclePath, Vector4.zero);
            }

            if (force || !File.Exists(BackdropPath))
            {
                WritePng(BackdropPath, VerticalGradient(8, 512,
                    new Color32(0x1C, 0x27, 0x55, 0xFF),   // 아래 (지면 쪽)
                    new Color32(0x06, 0x09, 0x1A, 0xFF)), 8, 512); // 위 (우주 쪽)
                ImportSprite(BackdropPath, Vector4.zero);
            }

            AssetDatabase.Refresh();
        }

        static Color[] RoundedRect(int w, int h, float radius)
        {
            var pixels = new Color[w * h];
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(halfW, halfH));

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - halfW) - (halfW - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - halfH) - (halfH - radius), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float alpha = Mathf.Clamp01(0.5f - dist); // 1px 안티에일리어싱
                    pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            return pixels;
        }

        static Color[] VerticalGradient(int w, int h, Color bottom, Color top)
        {
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, (float)y / (h - 1));
                for (int x = 0; x < w; x++) pixels[y * w + x] = c;
            }
            return pixels;
        }

        static void WritePng(string path, Color[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void ImportSprite(string path, Vector4 border)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
