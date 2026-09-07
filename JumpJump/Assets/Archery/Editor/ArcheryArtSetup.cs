using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Archery.EditorTools
{
    /// <summary>
    /// 그림을 교체해도 **크기가 그대로 유지되게** 하는 장치입니다.
    /// 점프점프의 JumpJumpArtSetup 과 같은 역할입니다.
    ///
    /// 왜 필요하냐면 — 스프라이트의 세계 크기는 `픽셀 수 ÷ PPU` 로 정해집니다.
    /// PPU 를 고정해 두면 2400px 짜리 그림을 넣었을 때 화면을 뒤덮을 만큼 커집니다.
    /// 그래서 반대로 **원하는 세계 크기를 먼저 정하고 PPU 를 역산**합니다.
    ///
    /// | 그림 | 맞추는 것 | 피벗 |
    /// | --- | --- | --- |
    /// | Arrow.png  | 세로 길이 = `ArcheryConfig.arrowLength` | 아래끝 가운데 (촉 = 위치 + 길이) |
    /// | Bow.png    | 가로 폭 = `ArcheryConfig.bowWidth`     | 아래끝 가운데 (활 밑동이 bowY 에) |
    /// | Target.png | (TargetController 가 매 프레임 다시 잽니다) | 한가운데 |
    ///
    /// **그림의 투명 여백은 먼저 잘라 냅니다.** 여백이 남아 있으면 "그림 크기"와
    /// "실제로 보이는 크기"가 달라져서, 과녁은 보이는 곳과 맞는 곳이 어긋나고
    /// 활·화살은 의도한 것보다 작게 보입니다.
    ///
    /// 계산에 쓴 값을 userData 에 적어 두기 때문에, **그림을 갈아 끼우거나 설정값을 바꾸면
    /// 다음 빌드에서 알아서 다시 계산합니다.** 손으로 메뉴를 누를 필요가 없습니다.
    /// </summary>
    public static class ArcheryArtSetup
    {
        [MenuItem("Tools/Archery/Setup Art Assets", priority = 11)]
        public static void SetupMenu()
        {
            Setup(force: true);
        }

        public static void Setup(bool force)
        {
            var config = AssetDatabase.LoadAssetAtPath<ArcheryConfig>(ArcheryStageCsv.ConfigPath);
            if (config == null) return;

            var report = new StringBuilder("[Archery] 아트 임포트 설정\n");
            bool changed = false;

            // 화살 : 세로 길이를 arrowLength 로. 피벗은 아래끝이라 "촉 = 위치 + 길이" 가 성립합니다.
            changed |= Configure(ArcheryArtGenerator.ArrowPath, force, report,
                                 box => box.y / Mathf.Max(0.01f, config.arrowLength),
                                 new Vector2(0.5f, 0f),
                                 $"높이={config.arrowLength}");

            // 활 : 가로 폭을 bowWidth 로. 피벗은 아래끝이라 활 밑동이 bowY 에 놓입니다.
            changed |= Configure(ArcheryArtGenerator.BowPath, force, report,
                                 box => box.x / Mathf.Max(0.01f, config.bowWidth),
                                 new Vector2(0.5f, 0f),
                                 $"폭={config.bowWidth}");

            // 과녁 : 크기는 TargetController 가 매 프레임 다시 잽니다. 피벗만 한가운데면 됩니다.
            // (피벗이 어긋나면 보이는 자리와 맞는 자리가 달라집니다)
            changed |= Configure(ArcheryArtGenerator.TargetPath, force, report,
                                 box => 100f, new Vector2(0.5f, 0.5f), "피벗만");

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(report.ToString());
            }
        }

        /// <summary>
        /// 여백을 잘라 낸 뒤 PPU 와 피벗을 잡습니다.
        /// 이미 같은 조건으로 잡혀 있으면 아무것도 하지 않습니다.
        /// </summary>
        static bool Configure(string path, bool force, StringBuilder report,
                              System.Func<Vector2Int, float> pixelsPerUnit, Vector2 pivot, string note)
        {
            if (!File.Exists(path)) return false;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return false;

            bool trimmed = TrimTransparentEdges(path);
            if (trimmed) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var size = ReadSize(path);
            if (size.x <= 0) return false;

            float ppu = Mathf.Max(0.01f, pixelsPerUnit(size));

            // 같은 그림 · 같은 목표 크기라면 다시 손대지 않습니다.
            string marker = $"archery-art-v1|{size.x}x{size.y}|{ppu:0.###}|{pivot.x:0.###},{pivot.y:0.###}";
            if (!force && !trimmed && importer.userData == marker) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point;      // 픽셀아트가 뭉개지지 않도록
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            importer.userData = marker;
            importer.SaveAndReimport();

            report.Append($"  {Path.GetFileName(path),-12} {size.x}x{size.y}px  PPU {ppu:0.0}  "
                          + $"피벗 ({pivot.x:0.##}, {pivot.y:0.##})  [{note}]"
                          + (trimmed ? "  여백 잘라냄" : "") + "\n");
            return true;
        }

        static Vector2Int ReadSize(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) return Vector2Int.zero;
                return new Vector2Int(texture.width, texture.height);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        /// <summary>
        /// 그림 주위의 투명 여백을 잘라 내고 같은 파일에 다시 씁니다.
        /// 자를 여백이 없으면 파일을 건드리지 않습니다(= 몇 번을 돌려도 안전합니다).
        /// </summary>
        static bool TrimTransparentEdges(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) return false;

                int w = texture.width, h = texture.height;
                var pixels = texture.GetPixels32();
                int minX = w, minY = h, maxX = -1, maxY = -1;

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (pixels[row + x].a <= 8) continue;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }

                if (maxX < 0) return false;                                   // 전부 투명
                int cw = maxX - minX + 1, ch = maxY - minY + 1;
                if (cw == w && ch == h) return false;                         // 자를 여백 없음

                var cropped = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                cropped.SetPixels(texture.GetPixels(minX, minY, cw, ch));
                cropped.Apply();
                File.WriteAllBytes(path, cropped.EncodeToPNG());
                Object.DestroyImmediate(cropped);
                return true;
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
