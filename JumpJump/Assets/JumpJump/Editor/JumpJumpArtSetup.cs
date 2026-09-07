using System.Text;
using UnityEditor;
using UnityEngine;

namespace JumpJump.EditorTools
{
    /// <summary>
    /// 픽셀아트 리소스의 임포트 설정을 잡고, 이미지의 실제 내용 영역을 측정해서
    /// PPU 와 피벗을 자동으로 계산합니다.
    ///
    /// - 캐릭터 5종: 그림마다 여백과 크기가 달라서, 내용 높이가 모두 playerSize.y 가 되도록
    ///   PPU 를 따로 계산합니다. 덕분에 5종의 키가 playerSize.y 로 똑같이 맞습니다.
    ///   피벗은 캐릭터의 "발바닥"에 찍어서 발판 위에 정확히 서고, 스쿼시도 발 기준으로 걸립니다.
    /// - Base.png: 내용 가로폭이 blockWidth 가 되도록 PPU 를 잡고, 세로 비율에서
    ///   GameConfig.blockHeight 를 역산해 에셋에 써 넣습니다.
    /// - Art/Background 의 BG_*.png: Tiled 드로우 모드에 필요한 Full Rect 메시로 설정합니다.
    ///   파일 이름 순서가 곧 높이 구간 순서입니다 (BG_01 = 가장 낮은 구간).
    /// </summary>
    public static class JumpJumpArtSetup
    {
        public const string CharacterFolder = "Assets/JumpJump/Art/Characters";
        public const string BasePath = "Assets/JumpJump/Art/Platforms/Base.png";
        public const string BackgroundFolder = "Assets/JumpJump/Art/Background";
        const string ConfigPath = "Assets/JumpJump/GameConfig.asset";

        const string Marker = "jumpjump-art-v1";

        [MenuItem("Tools/JumpJump/Setup Art Assets", priority = 10)]
        public static void SetupMenu()
        {
            Setup(force: true);
        }

        public static void Setup(bool force)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("[JumpJump] GameConfig.asset 을 찾지 못했습니다. 먼저 Build Game Scene 을 실행하세요.");
                return;
            }

            var report = new StringBuilder("[JumpJump] 아트 임포트 설정\n");
            bool changed = false;

            // ---- 캐릭터: 키를 playerSize.y 로 통일, 피벗은 발바닥 ----
            foreach (var path in CharacterPaths())
            {
                if (!force && IsConfigured(path)) continue;

                var box = Measure(path, out int texW, out int texH);
                if (box.width <= 0) continue;

                float ppu = box.height / Mathf.Max(0.01f, config.playerSize.y);
                var pivot = new Vector2(
                    (box.x + box.width * 0.5f) / texW,
                    box.y / (float)texH);          // 내용의 아래쪽 = 발바닥

                Apply(path, ppu, pivot);
                changed = true;
                report.Append($"  {System.IO.Path.GetFileName(path),-14} 내용 {box.width}x{box.height}px  PPU {ppu:0.0}  피벗 ({pivot.x:0.000}, {pivot.y:0.000})\n");
            }

            // ---- 발판: 가로폭을 blockWidth 로, 피벗은 내용 중앙 ----
            if (force || !IsConfigured(BasePath))
            {
                var box = Measure(BasePath, out int texW, out int texH);
                if (box.width > 0)
                {
                    float ppu = box.width / Mathf.Max(0.01f, config.blockWidth);
                    var pivot = new Vector2(
                        (box.x + box.width * 0.5f) / texW,
                        (box.y + box.height * 0.5f) / texH);

                    Apply(BasePath, ppu, pivot);
                    changed = true;

                    // 타일이 찌그러지지 않도록 blockHeight 를 그림 비율에서 역산합니다.
                    float blockHeight = box.height / ppu;
                    var so = new SerializedObject(config);
                    so.FindProperty("blockHeight").floatValue = blockHeight;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(config);

                    report.Append($"  Base.png       내용 {box.width}x{box.height}px  PPU {ppu:0.0}  -> blockHeight {blockHeight:0.000}\n");
                }
            }

            // ---- 배경: Tiled 드로우 모드용 Full Rect (여러 장 = 높이 구간별) ----
            foreach (var path in BackgroundPaths())
            {
                if (!force && IsConfigured(path)) continue;

                Apply(path, 100f, new Vector2(0.5f, 0.5f));
                changed = true;
                report.Append($"  {System.IO.Path.GetFileName(path),-14} Full Rect / Point 필터 (세로 반복 타일링용)\n");
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(report.ToString());
            }
        }

        /// <summary>Art/Background 의 배경 그림들. 파일 이름 순서 = 낮은 구간 -> 높은 구간.</summary>
        public static string[] BackgroundPaths()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { BackgroundFolder });
            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++) paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            System.Array.Sort(paths, string.CompareOrdinal);   // BG_01, BG_02, BG_03 순서 고정
            return paths;
        }

        public static string[] CharacterPaths()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { CharacterFolder });
            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++) paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            System.Array.Sort(paths, string.CompareOrdinal);   // Char_01 ~ Char_05 순서 고정
            return paths;
        }

        static bool IsConfigured(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            return importer != null && importer.userData == Marker;
        }

        /// <summary>알파가 있는 실제 내용 영역을 픽셀 단위로 잽니다. (좌하단 원점)</summary>
        static RectInt Measure(string path, out int texWidth, out int texHeight)
        {
            texWidth = texHeight = 0;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[JumpJump] 텍스처를 찾지 못했습니다: " + path);
                return new RectInt(0, 0, 0, 0);
            }

            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) return new RectInt(0, 0, 0, 0);

            texWidth = texture.width;
            texHeight = texture.height;

            var pixels = texture.GetPixels32();
            int minX = texWidth, minY = texHeight, maxX = -1, maxY = -1;

            for (int y = 0; y < texHeight; y++)
            {
                int rowStart = y * texWidth;
                for (int x = 0; x < texWidth; x++)
                {
                    if (pixels[rowStart + x].a <= 8) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0) return new RectInt(0, 0, texWidth, texHeight);   // 전부 불투명하지 않음 = 통짜 이미지
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        static void Apply(string path, float pixelsPerUnit, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;   // Tiled 드로우 모드 요구사항
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = Vector4.zero;
            importer.filterMode = FilterMode.Point;              // 픽셀아트가 뭉개지지 않도록
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.isReadable = false;
            importer.userData = Marker;

            importer.SaveAndReimport();
        }
    }
}
