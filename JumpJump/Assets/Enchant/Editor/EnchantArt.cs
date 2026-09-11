using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Enchant.EditorTools
{
    /// <summary>
    /// 검 강화의 그림을 준비합니다. **진짜 그림이 오기 전까지 쓸 임시 그림(픽셀아트)을 코드로 그리고**,
    /// 진짜 그림이 오면 그것을 가져옵니다. 코드는 고칠 필요가 없습니다.
    ///
    /// | 그림 | 어디 | 바꾸는 법 |
    /// | --- | --- | --- |
    /// | 검 (표의 Image 이름) | `Art/Swords/Sword_01.png` ... | 작업 폴더나 `@리소스 추가_N차` 에 **`Sword_01.png`** (또는 `@Sword_01.png`) 를 넣고 Build All Scenes |
    /// | 배경 | `Art/Background.png` | 작업 폴더에 **`@Enchant_BG.png`** 를 넣거나, 이 파일을 덮어쓰기 |
    /// | 로비 아이콘 | 작업 폴더 `@Game_03_검강화.png` | 이 파일을 덮어쓰기 |
    ///
    /// 진짜 검 그림이 없는 이름만 `Art/Placeholder/` 의 임시 그림을 씁니다. 임시 그림은 **따로 된 폴더**에 있어서,
    /// 진짜 그림을 넣으면 날짜와 상관없이 그쪽이 이깁니다 (복사하면 파일 날짜가 옛날로 남는 일이 흔합니다).
    ///
    /// 진짜 검 그림은 가져오면서 **투명 여백을 잘라 냅니다.** 화면에서는 검 칸의 높이에 꽉 차게 그려지므로
    /// 그림 크기(픽셀 수)는 상관없습니다.
    /// </summary>
    public static class EnchantArt
    {
        public const string ArtFolder = "Assets/Enchant/Art";
        public const string SwordsFolder = ArtFolder + "/Swords";
        public const string PlaceholderFolder = ArtFolder + "/Placeholder";
        public const string BackgroundPath = ArtFolder + "/Background.png";
        public const string BadgePath = ArtFolder + "/Badge.png";
        public const string ButtonPath = ArtFolder + "/Button.png";
        public const string GlowPath = ArtFolder + "/Glow.png";

        /// <summary>로비 아이콘의 원본. 작업 폴더(D:\00.JumpJump)에 놓입니다. 게임 순서 3번째.</summary>
        public const string LobbyIconName = "@Game_03_검강화.png";
        const string LobbyIconPattern = "Game_03_*.png";

        static readonly string[] BackgroundSources = { "@Enchant_BG.png", "Enchant_BG.png", "@Enchant_Background.png" };

        const int SwordW = 32, SwordH = 96;
        const int BgW = 135, BgH = 240;          // 1080x1920 의 1/8. 한 칸이 화면에서 8픽셀짜리 점이 됩니다
        const int ButtonSize = 16;
        /// <summary>버튼 그림의 9-슬라이스 테두리(px). 이 안쪽만 늘어납니다.</summary>
        public const int ButtonBorder = 5;
        const int BadgeSize = 40;
        const int GlowSize = 64;

        const string Marker = "enchant-art-v1";

        [MenuItem("Tools/Enchant/Regenerate Placeholder Art", priority = 30)]
        public static void RegenerateMenu()
        {
            var config = AssetDatabase.LoadAssetAtPath<EnchantConfig>(EnchantTableCsv.ConfigPath);
            Prepare(config != null ? ImageNames(config) : new List<string>(), force: true);
            Debug.Log("[Enchant] 임시 그림을 다시 만들었습니다. 진짜 그림(Art/Swords)은 건드리지 않았습니다.");
        }

        /// <summary>표에 나오는 그림 이름들 (중복 없이, 나오는 순서대로).</summary>
        public static List<string> ImageNames(EnchantConfig config)
        {
            var names = new List<string>();
            foreach (var row in config.Levels)
                if (!string.IsNullOrEmpty(row.image) && !names.Contains(row.image)) names.Add(row.image);
            return names;
        }

        /// <summary>
        /// 씬을 구울 때마다 부릅니다. 없는 임시 그림만 만들고, 작업 폴더의 진짜 그림을 가져옵니다.
        /// 몇 번을 돌려도 안전합니다 (이미 있는 것은 건드리지 않습니다).
        /// </summary>
        public static void Prepare(List<string> swordNames, bool force = false)
        {
            Directory.CreateDirectory(SwordsFolder);
            Directory.CreateDirectory(PlaceholderFolder);

            var report = new StringBuilder();

            if (force || !File.Exists(BackgroundPath)) WritePng(BackgroundPath, BackgroundPixels(), BgW, BgH);
            if (force || !File.Exists(BadgePath)) WritePng(BadgePath, BadgePixels(), BadgeSize, BadgeSize);
            if (force || !File.Exists(ButtonPath)) WritePng(ButtonPath, ButtonPixels(), ButtonSize, ButtonSize);
            if (force || !File.Exists(GlowPath)) WritePng(GlowPath, GlowPixels(), GlowSize, GlowSize);

            for (int i = 0; i < swordNames.Count; i++)
            {
                string path = PlaceholderFolder + "/" + swordNames[i] + ".png";
                if (!force && File.Exists(path)) continue;
                WritePng(path, SwordPixels(TierOf(swordNames[i], i)), SwordW, SwordH);
                report.Append("  임시 검 그림 ").Append(swordNames[i]).Append('\n');
            }

            ImportBackground(report);
            ImportRealSwords(report);
            EnsureLobbyIcon(report);

            AssetDatabase.Refresh();
            ConfigureAll();

            if (report.Length > 0) Debug.Log("[Enchant] 그림 준비\n" + report);
        }

        /// <summary>
        /// 이 이름의 검 그림. 진짜 그림(Art/Swords)이 있으면 그것, 없으면 임시 그림, 둘 다 없으면 null.
        /// </summary>
        public static Sprite SwordSprite(string name)
        {
            var real = AssetDatabase.LoadAssetAtPath<Sprite>(SwordsFolder + "/" + name + ".png");
            if (real != null) return real;
            return AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderFolder + "/" + name + ".png");
        }

        /// <summary>진짜 그림이 들어온 검 이름들.</summary>
        public static List<string> RealSwordNames()
        {
            var names = new List<string>();
            if (!Directory.Exists(SwordsFolder)) return names;
            foreach (var file in Directory.GetFiles(SwordsFolder, "*.png"))
                names.Add(Path.GetFileNameWithoutExtension(file));
            names.Sort(string.CompareOrdinal);
            return names;
        }

        // ------------------------------------------------------------------ 진짜 그림 가져오기

        /// <summary>
        /// 작업 폴더 · `@리소스` 폴더들의 `Sword_*.png` / `@Sword_*.png` 를 Art/Swords 로 가져옵니다 (여백을 잘라서).
        /// Art/Swords 에 **직접 넣은** 그림도 여백을 잘라 둡니다.
        /// </summary>
        static void ImportRealSwords(StringBuilder report)
        {
            foreach (var dir in Arcade.EditorTools.ShellArt.SourceFolders())
            {
                foreach (var pattern in new[] { "Sword_*.png", "@Sword_*.png" })
                {
                    foreach (var source in Directory.GetFiles(dir, pattern))
                    {
                        string name = Path.GetFileName(source).TrimStart('@');
                        string dest = SwordsFolder + "/" + name;
                        if (File.Exists(dest) && File.GetLastWriteTimeUtc(dest) >= File.GetLastWriteTimeUtc(source)) continue;

                        if (TrimToContent(source, dest, out var size, out var trimmed))
                            report.Append($"  {Path.GetFileName(source)} -> {dest}  여백 잘라냄 {size.x}x{size.y} -> {trimmed.x}x{trimmed.y}\n");
                        else
                        {
                            File.Copy(source, dest, true);
                            report.Append($"  {Path.GetFileName(source)} -> {dest}\n");
                        }
                    }
                }
            }

            foreach (var file in Directory.GetFiles(SwordsFolder, "*.png"))
            {
                string path = file.Replace('\\', '/');
                if (TrimToContent(path, path, out var size, out var trimmed) && size != trimmed)
                    report.Append($"  {Path.GetFileName(path)}  직접 넣은 그림의 여백을 잘라냈습니다 {size.x}x{size.y} -> {trimmed.x}x{trimmed.y}\n");
            }
        }

        static void ImportBackground(StringBuilder report)
        {
            string source = null;
            foreach (var dir in Arcade.EditorTools.ShellArt.SourceFolders())
                foreach (var name in BackgroundSources)
                {
                    string path = Path.Combine(dir, name);
                    if (File.Exists(path)) source = path;   // 뒤쪽 폴더가 이깁니다 (ShellArt 와 같은 규칙)
                }

            if (source == null) return;
            if (File.Exists(BackgroundPath) && File.GetLastWriteTimeUtc(BackgroundPath) >= File.GetLastWriteTimeUtc(source)) return;

            File.Copy(source, BackgroundPath, true);
            report.Append($"  {Path.GetFileName(source)} -> {BackgroundPath}\n");
        }

        /// <summary>
        /// 로비 칸의 동그란 아이콘을 작업 폴더에 만들어 둡니다 (활쏘기와 같은 방식).
        /// **진짜 그림이 이미 있으면 아무것도 하지 않습니다** — 프로젝트의 Art/Games 와 작업 폴더 · @리소스 폴더를 다 봅니다.
        /// </summary>
        static void EnsureLobbyIcon(StringBuilder report)
        {
            string games = Arcade.EditorTools.ShellArt.GamesFolder;
            if (Directory.Exists(games) && Directory.GetFiles(games, LobbyIconPattern).Length > 0) return;
            foreach (var dir in Arcade.EditorTools.ShellArt.SourceFolders())
                if (Directory.GetFiles(dir, "@" + LobbyIconPattern).Length > 0) return;

            string path = Path.Combine(Arcade.EditorTools.ShellArt.WorkingDir(), LobbyIconName);
            const int size = 128, scale = 4;
            WritePng(path, Upscale(IconPixels(size), size, size, scale), size * scale, size * scale);
            report.Append("  로비 아이콘 임시 그림 -> ").Append(path)
                  .Append("\n    진짜 그림이 나오면 이 파일을 덮어쓰고 Build All Scenes 를 누르세요.\n");
        }

        // ------------------------------------------------------------------ 임포트 설정

        static void ConfigureAll()
        {
            Configure(BackgroundPath, FilterMode.Point, 2048, Vector4.zero);
            Configure(BadgePath, FilterMode.Point, 256, Vector4.zero);
            Configure(ButtonPath, FilterMode.Point, 64,
                      new Vector4(ButtonBorder, ButtonBorder, ButtonBorder, ButtonBorder));
            Configure(GlowPath, FilterMode.Bilinear, 256, Vector4.zero);

            foreach (var folder in new[] { SwordsFolder, PlaceholderFolder })
                foreach (var file in Directory.GetFiles(folder, "*.png"))
                    Configure(file.Replace('\\', '/'), FilterMode.Point, 1024, Vector4.zero);
        }

        static void Configure(string path, FilterMode filter, int maxSize, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            string marker = $"{Marker}|{filter}|{maxSize}|{border}";
            if (importer.userData == marker) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = filter;                // 픽셀아트는 Point 라야 점이 뭉개지지 않습니다
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = maxSize;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteBorder = border;
            importer.SetTextureSettings(settings);

            importer.userData = marker;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ 검 (픽셀아트)

        struct Palette
        {
            public Color light, mid, fuller, edge, guard, guardDark, grip, gripDark, pommel, gem;
        }

        /// <summary>이름 끝의 숫자가 등급입니다 (Sword_03 -> 3). 숫자가 없으면 표에 나온 순서.</summary>
        static int TierOf(string name, int index)
        {
            int end = name.Length;
            int start = end;
            while (start > 0 && char.IsDigit(name[start - 1])) start--;
            if (start < end && int.TryParse(name.Substring(start), out int tier) && tier > 0) return tier;
            return index + 1;
        }

        static Palette PaletteFor(int tier)
        {
            switch (tier)
            {
                case 1:   // 녹슨 쇠
                    return new Palette
                    {
                        light = C(0.70f, 0.60f, 0.50f), mid = C(0.56f, 0.46f, 0.38f), fuller = C(0.42f, 0.33f, 0.27f), edge = C(0.78f, 0.70f, 0.60f),
                        guard = C(0.46f, 0.31f, 0.19f), guardDark = C(0.33f, 0.21f, 0.12f),
                        grip = C(0.38f, 0.26f, 0.16f), gripDark = C(0.26f, 0.17f, 0.10f), pommel = C(0.46f, 0.31f, 0.19f), gem = C(0.46f, 0.31f, 0.19f),
                    };
                case 2:   // 쇠
                    return new Palette
                    {
                        light = C(0.80f, 0.82f, 0.86f), mid = C(0.62f, 0.64f, 0.69f), fuller = C(0.47f, 0.49f, 0.54f), edge = C(0.92f, 0.93f, 0.96f),
                        guard = C(0.45f, 0.47f, 0.52f), guardDark = C(0.31f, 0.33f, 0.37f),
                        grip = C(0.45f, 0.30f, 0.18f), gripDark = C(0.31f, 0.20f, 0.12f), pommel = C(0.45f, 0.47f, 0.52f), gem = C(0.45f, 0.47f, 0.52f),
                    };
                case 3:   // 강철 + 청동
                    return new Palette
                    {
                        light = C(0.90f, 0.93f, 0.96f), mid = C(0.72f, 0.77f, 0.83f), fuller = C(0.55f, 0.60f, 0.68f), edge = C(1f, 1f, 1f),
                        guard = C(0.80f, 0.56f, 0.26f), guardDark = C(0.58f, 0.38f, 0.16f),
                        grip = C(0.60f, 0.18f, 0.15f), gripDark = C(0.40f, 0.10f, 0.09f), pommel = C(0.80f, 0.56f, 0.26f), gem = C(0.80f, 0.56f, 0.26f),
                    };
                case 4:   // 미스릴 (푸른빛)
                    return new Palette
                    {
                        light = C(0.78f, 0.93f, 1f), mid = C(0.50f, 0.76f, 0.96f), fuller = C(0.92f, 0.99f, 1f), edge = C(1f, 1f, 1f),
                        guard = C(0.84f, 0.87f, 0.92f), guardDark = C(0.56f, 0.60f, 0.68f),
                        grip = C(0.18f, 0.24f, 0.46f), gripDark = C(0.11f, 0.15f, 0.30f), pommel = C(0.84f, 0.87f, 0.92f), gem = C(0.25f, 0.62f, 1f),
                    };
                case 5:   // 마력 (보랏빛)
                    return new Palette
                    {
                        light = C(0.88f, 0.76f, 1f), mid = C(0.66f, 0.46f, 0.94f), fuller = C(1f, 0.92f, 1f), edge = C(1f, 0.96f, 1f),
                        guard = C(0.98f, 0.78f, 0.28f), guardDark = C(0.70f, 0.50f, 0.14f),
                        grip = C(0.30f, 0.14f, 0.40f), gripDark = C(0.19f, 0.08f, 0.27f), pommel = C(0.98f, 0.78f, 0.28f), gem = C(0.78f, 0.25f, 1f),
                    };
                default:  // 전설 (금빛 + 붉은 보석). 6 보다 높은 등급은 색을 돌려 가며 씁니다
                {
                    var p = new Palette
                    {
                        light = C(1f, 0.97f, 0.78f), mid = C(1f, 0.82f, 0.36f), fuller = C(1f, 1f, 0.94f), edge = C(1f, 1f, 1f),
                        guard = C(1f, 0.80f, 0.24f), guardDark = C(0.74f, 0.46f, 0.10f),
                        grip = C(0.62f, 0.10f, 0.12f), gripDark = C(0.42f, 0.05f, 0.07f), pommel = C(1f, 0.80f, 0.24f), gem = C(1f, 0.20f, 0.22f),
                    };
                    if (tier <= 6) return p;
                    float shift = ((tier - 6) * 0.14f) % 1f;
                    p.light = Hue(p.light, shift); p.mid = Hue(p.mid, shift); p.fuller = Hue(p.fuller, shift);
                    p.gem = Hue(p.gem, shift);
                    return p;
                }
            }
        }

        /// <summary>
        /// 위를 향한 검 한 자루 (32x96 픽셀). 등급이 오를수록 날이 길고 넓어지고, 코등이가 커지고,
        /// 4등급부터 보석 · 휘어진 코등이, 5등급부터 날에 룬, 6등급부터 날개 장식이 붙습니다.
        /// </summary>
        static Color[] SwordPixels(int tier)
        {
            int w = SwordW, h = SwordH;
            var pal = PaletteFor(tier);
            var px = new Color[w * h];
            var rng = new System.Random(tier * 7919);

            const int pommelY = 2, gripY = 6, gripLen = 12;
            int guardY = gripY + gripLen;                  // 18
            int bladeY = guardY + 4;                       // 22
            int bladeTop = Mathf.Min(h - 2, bladeY + 50 + tier * 4);
            float guardHalf = Mathf.Min(14f, 7.5f + tier);
            float bladeHalf = 2.5f + (tier >= 3 ? 1f : 0f) + (tier >= 5 ? 1f : 0f);
            float tipLen = bladeHalf * 2f + 2f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - 15.5f);
                    bool left = x <= 15;
                    Color c = Color.clear;

                    // 손잡이 끝 장식
                    if (y >= pommelY && y < gripY)
                    {
                        float r = (y == pommelY || y == gripY - 1) ? 1.5f : 2.5f;
                        if (dx < r) c = left ? pal.pommel : Mul(pal.pommel, 0.8f);
                        if (tier >= 4 && dx < 0.6f && y == pommelY + 2) c = pal.gem;
                    }
                    // 손잡이 : 감긴 끈 줄무늬
                    else if (y >= gripY && y < guardY)
                    {
                        if (dx < 1.5f) c = (y - gripY) % 3 == 0 ? pal.gripDark : (left ? pal.grip : Mul(pal.grip, 0.85f));
                    }
                    // 코등이
                    else if (y >= guardY && y < bladeY)
                    {
                        if (dx < guardHalf) c = (y == guardY || dx > guardHalf - 1.5f) ? pal.guardDark : pal.guard;
                        if (tier >= 4 && dx < 1.5f && (y == guardY + 1 || y == guardY + 2)) c = pal.gem;
                    }

                    // 휘어진 코등이 끝 (4등급~) / 날개 장식 (6등급~)
                    if (tier >= 4 && y >= bladeY && y < bladeY + 2 && dx >= guardHalf - 2.5f && dx < guardHalf)
                        c = pal.guardDark;
                    if (tier >= 6 && y >= bladeY + 2 && y < bladeY + 6 && dx >= guardHalf - 4.5f && dx < guardHalf - 1f - (y - bladeY - 2))
                        c = pal.guard;

                    // 날
                    if (y >= bladeY && y < bladeTop)
                    {
                        float half = bladeHalf;
                        float fromTop = bladeTop - y;
                        if (fromTop < tipLen) half = bladeHalf * (fromTop / tipLen);

                        if (dx < half)
                        {
                            if (dx < 1f && fromTop > tipLen * 0.6f) c = pal.fuller;               // 가운데 홈
                            else if (dx >= half - 1f) c = left ? pal.edge : Mul(pal.mid, 0.85f);  // 날 끝
                            else c = left ? pal.light : pal.mid;

                            if (tier == 1 && rng.NextDouble() < 0.07) c = C(0.48f, 0.30f, 0.18f);  // 녹
                            if (tier >= 5 && dx < 1f && (y - bladeY) % 7 == 3) c = Color.white;    // 룬
                        }
                    }

                    px[y * w + x] = c;
                }
            }

            // 반짝임 한 점 (2등급~)
            if (tier >= 2)
            {
                int gy = bladeTop - (int)tipLen - 3;
                px[gy * w + 14] = Color.white;
                px[(gy - 1) * w + 14] = Color.white;
            }

            return Outline(px, w, h, C(0.10f, 0.07f, 0.08f));
        }

        /// <summary>그림이 있는 칸 둘레 한 칸에 짙은 테두리를 두릅니다 (픽셀아트의 외곽선).</summary>
        static Color[] Outline(Color[] px, int w, int h, Color ink)
        {
            var result = (Color[])px.Clone();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a > 0f) continue;
                    bool near = (x > 0 && px[y * w + x - 1].a > 0f) || (x < w - 1 && px[y * w + x + 1].a > 0f) ||
                                (y > 0 && px[(y - 1) * w + x].a > 0f) || (y < h - 1 && px[(y + 1) * w + x].a > 0f);
                    if (near) result[y * w + x] = ink;
                }
            }
            return result;
        }

        // ------------------------------------------------------------------ 배경 · UI 조각

        static readonly float[] Bayer =
        {
            0f / 16, 8f / 16, 2f / 16, 10f / 16,
            12f / 16, 4f / 16, 14f / 16, 6f / 16,
            3f / 16, 11f / 16, 1f / 16, 9f / 16,
            15f / 16, 7f / 16, 13f / 16, 5f / 16,
        };

        /// <summary>
        /// 대장간 벽 (135x240). 벽돌 벽 + 바닥 + 양쪽 횃불 + 가운데 검 뒤의 따뜻한 불빛.
        /// 불빛은 점무늬(디더링)로 단계를 나눠 픽셀아트처럼 보이게 합니다.
        /// </summary>
        static Color[] BackgroundPixels()
        {
            int w = BgW, h = BgH;
            var px = new Color[w * h];
            var brick = C(0.24f, 0.18f, 0.18f);
            var mortar = C(0.11f, 0.08f, 0.09f);
            var floor = C(0.15f, 0.11f, 0.10f);
            var warm = C(1f, 0.56f, 0.24f);
            const int floorTop = 46;

            var torches = new[] { new Vector2Int(16, 158), new Vector2Int(w - 17, 158) };

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    if (y < floorTop)
                    {
                        // 바닥 : 가로 판석 줄, 맨 윗줄은 밝은 턱
                        bool seam = (y % 11 == 0) || ((x + (y / 11) * 13) % 27 == 0);
                        c = seam ? Mul(floor, 0.7f) : Mul(floor, 0.95f + Hash(x / 27, y / 11) * 0.1f);
                        if (y >= floorTop - 2) c = C(0.30f, 0.23f, 0.21f);
                    }
                    else
                    {
                        // 벽돌 : 16x8 칸을 한 줄씩 엇갈려 쌓습니다
                        int row = (y - floorTop) / 8;
                        int shift = (row % 2) * 8;
                        int col = (x + shift) / 16;
                        bool isMortar = (y - floorTop) % 8 == 0 || (x + shift) % 16 == 0;
                        c = isMortar ? mortar : Mul(brick, 0.86f + Hash(col, row) * 0.26f);
                        if (!isMortar && (y - floorTop) % 8 == 7) c = Mul(c, 1.12f);   // 벽돌 윗면
                    }

                    // 가운데 불빛 (검 뒤). 거리에 따라 5단계로 나눠 점무늬로 섞습니다.
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(w * 0.5f, h * 0.56f)) / (h * 0.52f);
                    float light = Mathf.Clamp01(1f - d);
                    light = light * light;
                    foreach (var t in torches)
                    {
                        float td = Vector2.Distance(new Vector2(x, y), t) / 34f;
                        light += Mathf.Clamp01(1f - td) * 0.55f;
                    }
                    float bayer = Bayer[(y % 4) * 4 + (x % 4)];
                    float step = Mathf.Floor(Mathf.Clamp01(light) * 5f + bayer) / 5f;
                    c = Color.Lerp(c, Color.Lerp(c, warm, 0.55f) * 1.6f, step * 0.5f);

                    // 가장자리 어둡게
                    float vx = Mathf.Abs(x / (float)w - 0.5f) * 2f, vy = Mathf.Abs(y / (float)h - 0.5f) * 2f;
                    c = Mul(c, 1f - 0.35f * Mathf.Clamp01(Mathf.Max(vx, vy) - 0.55f) / 0.45f);

                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }

            foreach (var t in torches) DrawTorch(px, w, t.x, t.y);
            return px;
        }

        static void DrawTorch(Color[] px, int w, int cx, int cy)
        {
            var metal = C(0.20f, 0.17f, 0.17f);
            var red = C(0.90f, 0.25f, 0.12f);
            var orange = C(1f, 0.58f, 0.16f);
            var yellow = C(1f, 0.92f, 0.52f);

            for (int y = cy - 9; y < cy; y++)               // 받침대
                for (int x = cx - 1; x <= cx + 1; x++) Set(px, w, x, y, metal);
            for (int x = cx - 3; x <= cx + 3; x++) Set(px, w, x, cy - 1, metal);

            // 불꽃 : 아래가 넓고 위로 갈수록 좁아집니다 (바깥 빨강 / 주황 / 가운데 노랑)
            int[] outer = { 3, 3, 2, 2, 1, 1, 0 };
            for (int i = 0; i < outer.Length; i++)
            {
                int y = cy + i;
                for (int x = cx - outer[i]; x <= cx + outer[i]; x++)
                {
                    int dx = Mathf.Abs(x - cx);
                    Color c = dx == outer[i] && outer[i] > 0 ? red : (dx <= outer[i] - 2 || i >= 4 ? yellow : orange);
                    if (i == outer.Length - 1) c = orange;
                    Set(px, w, x, y, c);
                }
            }
        }

        /// <summary>강화 수치를 적는 동그라미 (40x40). 금테 + 짙은 속.</summary>
        static Color[] BadgePixels()
        {
            int s = BadgeSize;
            var px = new Color[s * s];
            float c = s * 0.5f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    Color col = Color.clear;
                    if (d < c - 0.5f) col = C(0.33f, 0.20f, 0.10f);                         // 바깥 테두리
                    if (d < c - 1.5f) col = y > c ? C(1f, 0.84f, 0.40f) : C(0.86f, 0.62f, 0.24f);   // 금테
                    if (d < c - 4.5f) col = C(0.33f, 0.20f, 0.10f);
                    if (d < c - 5.5f) col = y > c + 6 ? C(0.26f, 0.17f, 0.15f) : C(0.19f, 0.12f, 0.11f);   // 속
                    px[y * s + x] = col;
                }
            }
            return px;
        }

        /// <summary>
        /// 버튼 판 (16x16, 9-슬라이스). 흰색이라 씬에서 색을 입힙니다.
        /// 아래 3줄이 그림자, 위 2줄이 윗면 빛이라 색을 입히면 볼록한 픽셀 버튼이 됩니다. 모서리는 한 칸씩 깎았습니다.
        /// </summary>
        static Color[] ButtonPixels()
        {
            int s = ButtonSize;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool corner = (x == 0 || x == s - 1) && (y == 0 || y == s - 1);
                    if (corner) { px[y * s + x] = Color.clear; continue; }

                    float v;
                    if (y <= 2) v = 0.58f;
                    else if (y >= s - 2) v = 1f;
                    else v = 0.86f;
                    if (x == 0 || x == s - 1 || y == 0 || y == s - 1) v = 0.34f;   // 테두리
                    px[y * s + x] = new Color(v, v, v, 1f);
                }
            }
            return px;
        }

        /// <summary>검 뒤의 부드러운 빛 (가운데가 진하고 바깥으로 사라짐). 이것만 픽셀아트가 아닙니다.</summary>
        static Color[] GlowPixels()
        {
            int s = GlowSize;
            var px = new Color[s * s];
            float c = s * 0.5f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    px[y * s + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            return px;
        }

        /// <summary>로비 아이콘 (128x128 을 4배로 키워 저장). 짙은 동그라미 안에 전설 검 한 자루.</summary>
        static Color[] IconPixels(int s)
        {
            var px = new Color[s * s];
            float c = s * 0.5f;
            var warm = C(1f, 0.56f, 0.24f);

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    if (d >= c - 1f) { px[y * s + x] = Color.clear; continue; }

                    float light = Mathf.Clamp01(1f - d / (c * 0.9f));
                    float bayer = Bayer[(y % 4) * 4 + (x % 4)];
                    float step = Mathf.Floor(light * 4f + bayer) / 4f;
                    Color col = Color.Lerp(C(0.17f, 0.11f, 0.10f), warm, step * 0.55f);
                    col.a = 1f;
                    px[y * s + x] = col;
                }
            }

            // 가운데에 6등급 검을 그대로 얹습니다 (32x96)
            var sword = SwordPixels(6);
            int ox = (s - SwordW) / 2, oy = (s - SwordH) / 2;
            for (int y = 0; y < SwordH; y++)
                for (int x = 0; x < SwordW; x++)
                {
                    var p = sword[y * SwordW + x];
                    if (p.a > 0f) px[(y + oy) * s + (x + ox)] = p;
                }

            return px;
        }

        // ------------------------------------------------------------------ 도우미

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
        static Color Mul(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        static Color Hue(Color c, float shift)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var result = Color.HSVToRGB((h + shift) % 1f, s, v);
            result.a = c.a;
            return result;
        }

        static float Hash(int a, int b)
        {
            unchecked
            {
                int n = a * 374761393 + b * 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0xffff) / 65535f;
            }
        }

        static void Set(Color[] px, int w, int x, int y, Color c)
        {
            if (x < 0 || x >= w || y < 0 || y * w + x >= px.Length) return;
            px[y * w + x] = c;
        }

        static Color[] Upscale(Color[] px, int w, int h, int k)
        {
            var result = new Color[w * k * h * k];
            for (int y = 0; y < h * k; y++)
                for (int x = 0; x < w * k; x++)
                    result[y * w * k + x] = px[(y / k) * w + (x / k)];
            return result;
        }

        static void WritePng(string path, Color[] pixels, int w, int h)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>알파가 있는 실제 그림 영역만 남기고 dest 에 PNG 로 씁니다. 자를 것이 없고 제자리면 건드리지 않습니다.</summary>
        static bool TrimToContent(string source, string dest, out Vector2Int size, out Vector2Int trimmed)
        {
            size = trimmed = Vector2Int.zero;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(source))) return false;

                int w = texture.width, h = texture.height;
                size = new Vector2Int(w, h);
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

                if (maxX < 0) return false;
                int cw = maxX - minX + 1, ch = maxY - minY + 1;
                trimmed = new Vector2Int(cw, ch);

                bool inPlace = string.Equals(Path.GetFullPath(source), Path.GetFullPath(dest), System.StringComparison.OrdinalIgnoreCase);
                if (cw == w && ch == h && inPlace) return true;

                var cropped = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                cropped.SetPixels(texture.GetPixels(minX, minY, cw, ch));
                cropped.Apply();
                File.WriteAllBytes(dest, cropped.EncodeToPNG());
                Object.DestroyImmediate(cropped);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
