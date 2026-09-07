using System.IO;
using UnityEditor;
using UnityEngine;

namespace Archery.EditorTools
{
    /// <summary>
    /// **임시(플레이스홀더) 그림**을 코드로 그려서 PNG 로 굽습니다.
    /// 나중에 진짜 아트가 나오면 같은 파일을 덮어쓰기만 하면 됩니다. 코드는 고치지 않아도 됩니다.
    ///
    /// | 파일 | 무엇 |
    /// | --- | --- |
    /// | Art/Target.png     | 과녁 (5링, 가운데가 5점) |
    /// | Art/Arrow.png      | 화살 (아래끝이 피벗이라 촉 위치 계산이 쉽습니다) |
    /// | Art/Bow.png        | 활 (화면 아래 가운데 고정) |
    /// | Art/Sky.png        | 배경 하늘 그라데이션 |
    /// | Art/ui_round.png   | HUD 용 흰색 라운드 사각형 (9-slice) |
    ///
    /// 로비에 쓸 아이콘(@Game_02_활쏘기.png)은 **작업 폴더**에 만듭니다.
    /// 이미 있으면 절대 덮어쓰지 않으므로, 진짜 그림을 넣어 두면 안전합니다.
    /// </summary>
    public static class ArcheryArtGenerator
    {
        public const string ArtFolder = "Assets/Archery/Art";
        public const string TargetPath = ArtFolder + "/Target.png";
        public const string ArrowPath = ArtFolder + "/Arrow.png";
        public const string BowPath = ArtFolder + "/Bow.png";
        public const string SkyPath = ArtFolder + "/Sky.png";
        public const string RoundPath = ArtFolder + "/ui_round.png";

        /// <summary>로비 아이콘의 원본. 작업 폴더(D:\00.JumpJump)에 놓입니다.</summary>
        public const string LobbyIconName = "@Game_02_활쏘기.png";

        const int TargetSize = 512;
        const int ArrowW = 48, ArrowH = 256;
        const int BowW = 320, BowH = 220;
        const int RoundSize = 96, RoundRadius = 24;

        // 과녁 링 색: 가운데(5점) -> 가장자리(1점)
        static readonly Color32[] RingColors =
        {
            new Color32(0xFF, 0xD2, 0x4A, 0xFF),   // 5점 노랑
            new Color32(0xF0, 0x59, 0x5C, 0xFF),   // 4점 빨강
            new Color32(0x5C, 0xC0, 0x8C, 0xFF),   // 3점 초록
            new Color32(0x6E, 0x7F, 0xC0, 0xFF),   // 2점 파랑
            new Color32(0x3E, 0x4A, 0x80, 0xFF),   // 1점 남색
        };
        static readonly Color RingLine = new Color(1f, 1f, 1f, 0.55f);

        [MenuItem("Tools/Archery/Regenerate Placeholder Art", priority = 30)]
        public static void RegenerateMenu()
        {
            Generate(force: true);
            Debug.Log("[Archery] 임시 그림을 다시 만들었습니다.\n" +
                      "  진짜 아트가 나오면 " + ArtFolder + " 의 같은 이름 파일을 덮어쓰면 됩니다.");
        }

        public static void Generate(bool force)
        {
            Directory.CreateDirectory(ArtFolder);

            if (force || !File.Exists(TargetPath))
            {
                WritePng(TargetPath, TargetPixels(TargetSize), TargetSize, TargetSize);
                ImportSprite(TargetPath, 100f, new Vector2(0.5f, 0.5f), Vector4.zero);
            }

            if (force || !File.Exists(ArrowPath))
            {
                WritePng(ArrowPath, ArrowPixels(ArrowW, ArrowH), ArrowW, ArrowH);
                // 화살 길이 1유닛 = 256px. 피벗은 아래끝이라 "촉 = 위치 + 길이" 로 계산됩니다.
                ImportSprite(ArrowPath, ArrowH, new Vector2(0.5f, 0f), Vector4.zero);
            }

            if (force || !File.Exists(BowPath))
            {
                WritePng(BowPath, BowPixels(BowW, BowH), BowW, BowH);
                // PPU 100 = 활 폭 3.2유닛. 피벗은 손잡이(활을 잡는 자리)에 둡니다.
                ImportSprite(BowPath, 100f, new Vector2(0.5f, 0.42f), Vector4.zero);
            }

            if (force || !File.Exists(SkyPath))
            {
                WritePng(SkyPath, VerticalGradient(8, 512,
                    new Color32(0xEC, 0xF5, 0xFC, 0xFF),    // 아래 (밝은 하늘)
                    new Color32(0x7C, 0xB4, 0xE8, 0xFF)), 8, 512);   // 위 (진한 하늘)
                ImportSprite(SkyPath, 100f, new Vector2(0.5f, 0.5f), Vector4.zero);
            }

            if (force || !File.Exists(RoundPath))
            {
                WritePng(RoundPath, RoundedRect(RoundSize, RoundSize, RoundRadius), RoundSize, RoundSize);
                ImportSprite(RoundPath, 100f, new Vector2(0.5f, 0.5f),
                             new Vector4(RoundRadius, RoundRadius, RoundRadius, RoundRadius));
            }

            EnsureLobbyIcon();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 로비 칸에 올라갈 동그란 아이콘을 작업 폴더에 만들어 둡니다.
        ///
        /// **진짜 그림이 이미 있으면 아무것도 하지 않습니다.** 두 곳을 다 확인합니다.
        ///   - 작업 폴더의 `@Game_02_*.png`
        ///   - 프로젝트의 `Assets/Shell/Art/Games/Game_02_*.png` (작업 폴더를 거치지 않고 직접 넣은 경우)
        /// 프로젝트 쪽만 확인하지 않으면, 직접 넣어 둔 아이콘을 임시 그림이 덮어쓸 수 있습니다.
        /// </summary>
        public static void EnsureLobbyIcon()
        {
            if (Directory.Exists(Arcade.EditorTools.ShellArt.GamesFolder)
                && Directory.GetFiles(Arcade.EditorTools.ShellArt.GamesFolder, "Game_02_*.png").Length > 0)
                return;

            string path = Path.Combine(Arcade.EditorTools.ShellArt.WorkingDir(), LobbyIconName);
            if (File.Exists(path)) return;

            const int size = 512;
            WritePng(path, TargetPixels(size), size, size);
            Debug.Log("[Archery] 로비 아이콘 임시 그림을 만들었습니다 -> " + path +
                      "\n  진짜 그림이 나오면 이 파일을 덮어쓰고 Tools > Arcade > Build All Scenes 를 누르세요.");
        }

        // ------------------------------------------------------------------ 그리기

        /// <summary>동심원 5개. 가운데가 5점이고 바깥으로 갈수록 1점씩 줄어듭니다.</summary>
        static Color[] TargetPixels(int size)
        {
            var pixels = new Color[size * size];
            float center = size * 0.5f;
            float radius = center - 2f;
            int rings = RingColors.Length;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x + 0.5f - center) * (x + 0.5f - center) +
                                            (y + 0.5f - center) * (y + 0.5f - center));

                    // 바깥 경계 1px 안티에일리어싱
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    if (alpha <= 0f) { pixels[y * size + x] = Color.clear; continue; }

                    float r = dist / radius;                     // 0(가운데) ~ 1(가장자리)
                    int ring = Mathf.Clamp(Mathf.FloorToInt(r * rings), 0, rings - 1);
                    Color color = RingColors[ring];

                    // 링 사이를 흰 선으로 갈라 어디까지가 몇 점인지 눈에 보이게 합니다.
                    float ringPos = r * rings - ring;
                    float lineWidth = 1.6f / (radius / rings);
                    if (ring > 0 && ringPos < lineWidth) color = Color.Lerp(RingLine, color, ringPos / lineWidth);

                    color.a = alpha;
                    pixels[y * size + x] = color;
                }
            }

            return pixels;
        }

        /// <summary>위를 향한 화살. 아래에서부터 깃 / 대 / 촉.</summary>
        static Color[] ArrowPixels(int w, int h)
        {
            var pixels = new Color[w * h];
            var shaft = new Color(0.83f, 0.85f, 0.89f);
            var head = new Color(0.38f, 0.43f, 0.53f);
            var fletch = new Color(0.49f, 0.71f, 0.91f);

            float cx = w * 0.5f;
            float headBottom = h * 0.80f;
            float fletchTop = h * 0.26f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - cx);
                    Color c = Color.clear;

                    if (y >= headBottom)
                    {
                        // 촉 : 위로 갈수록 좁아지는 삼각형
                        float t = (y - headBottom) / (h - headBottom);
                        float halfW = Mathf.Lerp(w * 0.38f, 0.6f, t);
                        if (dx <= halfW) c = head;
                    }
                    else if (y <= fletchTop && y >= 6)
                    {
                        // 깃 : 아래로 갈수록 넓어짐
                        float halfW = 6.5f + (fletchTop - y) * 0.34f;
                        if (dx <= halfW) c = dx <= 6.5f ? shaft : fletch;
                    }

                    if (c.a == 0f && y >= 4 && y < headBottom + 2f && dx <= 6.5f) c = shaft;

                    pixels[y * w + x] = c;
                }
            }

            return pixels;
        }

        /// <summary>
        /// 활. 가운데 손잡이에서 양쪽으로 솟았다가 끝에서 다시 내려오는 날개 모양입니다.
        /// (기획서 손스케치와 같은 모양)
        /// </summary>
        static Color[] BowPixels(int w, int h)
        {
            var pixels = new Color[w * h];
            var wood = new Color(0.55f, 0.35f, 0.17f);
            var grip = new Color(0.24f, 0.16f, 0.09f);
            var stringColor = new Color(0.42f, 0.45f, 0.52f);

            float cx = w * 0.5f;
            float baseY = h * 0.26f;         // 시위(활줄) 높이 = 양쪽 끝 높이
            float amplitude = h * 0.52f;
            float thickness = 9f;

            // 곡선을 촘촘히 샘플링해 두고, 각 픽셀에서 가장 가까운 샘플까지의 거리로 두께를 만듭니다.
            // 가운데(손잡이)를 조금 들어 올려서 M 자가 아니라 활처럼 보이게 합니다.
            const int samples = 400;
            var curveX = new float[samples];
            var curveY = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float u = i / (samples - 1f) * 2f - 1f;                  // -1 ~ 1
                curveX[i] = cx + u * (w * 0.5f - thickness - 2f);
                curveY[i] = baseY + amplitude * (0.34f + 0.66f * Mathf.Sin(Mathf.PI * Mathf.Abs(u)));
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float best = float.MaxValue;

                    for (int i = 0; i < samples; i++)
                    {
                        float dx = px - curveX[i], dy = py - curveY[i];
                        float d = dx * dx + dy * dy;
                        if (d < best) best = d;
                    }

                    float dist = Mathf.Sqrt(best);
                    float alpha = Mathf.Clamp01(thickness - dist + 0.5f);
                    Color c = wood;
                    c.a = alpha;

                    // 시위 : 양쪽 활 끝을 잇는 가로선. 끝 높이에 맞춰야 활처럼 보입니다.
                    float stringY = baseY + amplitude * 0.34f;
                    float stringDist = Mathf.Abs(py - stringY);
                    if (alpha <= 0f && stringDist <= 2.6f && Mathf.Abs(px - cx) <= w * 0.5f - thickness - 2f)
                    {
                        c = stringColor;
                        c.a = Mathf.Clamp01(2.8f - stringDist);
                    }

                    // 손잡이 : 가운데 짧은 막대
                    if (Mathf.Abs(px - cx) <= 11f && py >= baseY + amplitude * 0.34f - 26f
                                                  && py <= baseY + amplitude * 0.34f + 26f)
                    {
                        c = grip;
                        c.a = 1f;
                    }

                    pixels[y * w + x] = c;
                }
            }

            return pixels;
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
                    pixels[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - dist));
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

        // ------------------------------------------------------------------ 파일

        static void WritePng(string path, Color[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void ImportSprite(string path, float pixelsPerUnit, Vector2 pivot, Vector4 border)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // Tiled / Sliced 드로우 모드에 필요합니다
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
