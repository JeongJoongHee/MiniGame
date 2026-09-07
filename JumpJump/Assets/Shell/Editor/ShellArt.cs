using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Arcade.EditorTools
{
    /// <summary>
    /// 작업 폴더(D:\00.JumpJump)에 놓아 둔 `@` 로 시작하는 그림들을 프로젝트 안으로 가져오고,
    /// 픽셀아트가 뭉개지지 않도록 임포트 설정을 잡습니다. config.csv 와 같은 방식입니다.
    ///
    /// **버튼·이름표·아이콘은 가져오면서 투명한 여백을 잘라 냅니다.**
    /// 원본 PNG 는 그림 주위에 빈 공간이 넓게 남아 있어서, 그대로 쓰면 "버튼을 화면의 이만큼"
    /// 이라고 지정해도 실제 버튼은 그보다 훨씬 작게 보입니다. 여백을 잘라 두면
    /// 사각형 크기 = 보이는 그림 크기가 되어 배치 계산이 그대로 맞아떨어집니다.
    /// (원본은 작업 폴더에 그대로 있고, 잘라낸 사본만 프로젝트에 들어갑니다)
    /// </summary>
    public static class ShellArt
    {
        public const string Folder = "Assets/Shell/Art";
        public const string GamesFolder = "Assets/Shell/Art/Games";
        public const string NamesFolder = "Assets/Shell/Art/Names";

        public const string TitlePath = Folder + "/Title.jpg";
        public const string TitleTextPath = Folder + "/Title_Text.png";
        public const string LobbyPath = Folder + "/Lobby.png";
        public const string StartButtonPath = Folder + "/Start_Button.png";
        public const string PlayButtonPath = Folder + "/Play_Button.png";

        /// <summary>이름표 그림이 아직 없는 게임에 깔리는 빈 이름표. 없으면 코드가 그려서 만듭니다.</summary>
        public const string BlankPlatePath = Folder + "/NamePlate_Blank.png";

        /// <summary>작업 폴더의 파일 이름 -> 프로젝트 안 경로. 배경은 자르지 않고 그대로 복사합니다.</summary>
        static readonly (string[] sources, string dest, bool trim)[] Map =
        {
            (new[] { "@Title.jpg", "@Title.png" },                    TitlePath,        false),
            (new[] { "@Title_Text.png" },                             TitleTextPath,    true),
            (new[] { "@Lobby.png", "@Lobby.jpg" },                    LobbyPath,        false),
            (new[] { "@Start_Button.png" },                           StartButtonPath,  true),
            (new[] { "@Play_Button.png" },                            PlayButtonPath,   true),
        };

        [MenuItem("Tools/Arcade/Import Shell Art", priority = 20)]
        public static void ImportMenu()
        {
            Sync(force: true);
            Debug.Log("[Arcade] 타이틀 / 로비 리소스를 다시 가져왔습니다.");
        }

        /// <summary>작업 폴더가 더 새로우면 가져옵니다. 씬을 구울 때마다 자동으로 불립니다.</summary>
        public static void Sync(bool force)
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(GamesFolder);
            Directory.CreateDirectory(NamesFolder);

            var report = new StringBuilder();
            bool changed = false;

            if (EnsureBlankNamePlate()) changed = true;

            foreach (var (sources, dest, trim) in Map)
            {
                string source = FindSource(sources);
                if (source == null) continue;
                if (CopyIfNewer(source, dest, trim, force, report)) changed = true;
            }

            // @Game_NN_*.png 는 미니게임 아이콘, @Name_NN_*.png 는 그 게임의 이름표입니다.
            // 파일을 늘리면 자동으로 따라오므로 게임을 추가할 때 코드를 고칠 일이 없습니다.
            foreach (var (pattern, folder) in new[] { ("@Game_*.png", GamesFolder), ("@Name_*.png", NamesFolder) })
            {
                foreach (var source in Directory.GetFiles(WorkingDir(), pattern))
                {
                    string dest = folder + "/" + Path.GetFileName(source).Substring(1);
                    if (CopyIfNewer(source, dest, true, force, report)) changed = true;
                }
            }

            if (TrimStrayArt(report)) changed = true;

            if (changed)
            {
                AssetDatabase.Refresh();
                ApplyImportSettings();
                Debug.Log("[Arcade] 리소스 가져오기\n" + report);
            }
            else
            {
                ApplyImportSettings();
            }
        }

        /// <summary>
        /// 작업 폴더를 거치지 않고 `Art/Games` / `Art/Names` 에 **직접 넣은** 그림의 투명 여백을 잘라 냅니다.
        ///
        /// 작업 폴더의 `@` 그림은 가져오면서 여백을 잘라 내지만, 프로젝트 폴더에 바로 떨어뜨린 그림은
        /// 그 과정을 거치지 않습니다. 그러면 원본에 남은 빈 여백만큼 그림이 작게 보여서,
        /// **게임마다 아이콘·이름표 크기가 들쭉날쭉해집니다.** (예: 2400x1308 파일의 실제 그림은 1797x838)
        /// 한 번 자르고 나면 여백이 없어 다시 자르지 않으므로, 몇 번을 돌려도 안전합니다.
        /// </summary>
        static bool TrimStrayArt(StringBuilder report)
        {
            bool changed = false;

            foreach (var folder in new[] { GamesFolder, NamesFolder })
            {
                foreach (var path in SortedTextures(folder))
                {
                    if (!TrimToContent(path, path, out var size, out var trimmed)) continue;
                    if (trimmed == size) continue;   // 자를 여백이 없었음

                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    report.Append("  ").Append(Path.GetFileName(path))
                          .Append($"  프로젝트에 직접 넣은 그림의 여백을 잘라냈습니다 {size.x}x{size.y} -> {trimmed.x}x{trimmed.y}\n");
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// 이름표 그림이 없는 게임(= 아직 @Name_NN_*.png 을 안 그린 게임)에 깔릴
        /// 빈 이름표를 만들어 둡니다. 그 위에는 코드가 게임 이름을 글자로 얹습니다.
        /// 파일이 이미 있으면 아무것도 하지 않습니다.
        /// </summary>
        static bool EnsureBlankNamePlate()
        {
            if (File.Exists(BlankPlatePath)) return false;

            const int w = 512, h = 238, radius = 34;
            var fill = new Color(1f, 0.953f, 0.855f);
            var edge = new Color(0.545f, 0.353f, 0.169f);

            var pixels = new Color[w * h];
            float halfW = w * 0.5f, halfH = h * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 모서리가 둥근 사각형까지의 거리. 0 보다 작으면 안쪽입니다.
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - halfW) - (halfW - radius - 4f), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - halfH) - (halfH - radius - 4f), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                    Color c = dist > -7f ? edge : fill;         // 바깥쪽 7px 은 테두리 색
                    c.a = Mathf.Clamp01(0.5f - dist);
                    pixels[y * w + x] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(BlankPlatePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Debug.Log("[Arcade] 빈 이름표 그림을 만들었습니다 -> " + BlankPlatePath +
                      "\n  이름표 그림이 없는 게임은 이 위에 게임 이름이 글자로 찍힙니다.");
            return true;
        }

        static string FindSource(string[] candidates)
        {
            foreach (var name in candidates)
            {
                string path = Path.Combine(WorkingDir(), name);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        static bool CopyIfNewer(string source, string dest, bool trim, bool force, StringBuilder report)
        {
            if (!force && File.Exists(dest) && File.GetLastWriteTimeUtc(dest) >= File.GetLastWriteTimeUtc(source))
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(dest));

            if (!trim)
            {
                File.Copy(source, dest, true);
                report.Append("  ").Append(Path.GetFileName(source)).Append(" -> ").Append(dest).Append('\n');
                return true;
            }

            if (!TrimToContent(source, dest, out var size, out var trimmed))
            {
                File.Copy(source, dest, true);
                report.Append("  ").Append(Path.GetFileName(source)).Append(" -> ").Append(dest).Append("  (여백 자르기 실패, 원본 그대로)\n");
                return true;
            }

            report.Append("  ").Append(Path.GetFileName(source))
                  .Append($" -> {dest}  여백 잘라냄 {size.x}x{size.y} -> {trimmed.x}x{trimmed.y}\n");
            return true;
        }

        /// <summary>알파가 있는 실제 그림 영역만 남기고 PNG 로 다시 씁니다.</summary>
        static bool TrimToContent(string source, string dest, out Vector2Int size, out Vector2Int trimmedSize)
        {
            size = trimmedSize = Vector2Int.zero;

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

                if (maxX < 0) return false;   // 전부 투명

                int cw = maxX - minX + 1, ch = maxY - minY + 1;
                trimmedSize = new Vector2Int(cw, ch);

                // 제자리에서 자르는 경우, 자를 여백이 없으면 파일을 건드리지 않습니다.
                // (매번 다시 쓰면 수정 시각이 바뀌어 쓸데없는 재임포트가 일어납니다)
                if (cw == w && ch == h && string.Equals(source, dest, System.StringComparison.OrdinalIgnoreCase))
                    return true;

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

        /// <summary>가져온 그림들의 임포트 설정(Point 필터 / 압축 없음 / 크기 상한)을 잡습니다.</summary>
        public static void ApplyImportSettings()
        {
            // 배경은 화면 전체를 덮으므로 크게, 버튼·아이콘은 작게 보이므로 512 면 충분합니다.
            Configure(TitlePath, 2048);
            Configure(LobbyPath, 2048);
            Configure(TitleTextPath, 2048);      // 가로로 긴 그림이라 512 로 줄이면 글자가 뭉갭니다
            Configure(StartButtonPath, 512);
            Configure(BlankPlatePath, 1024);
            Configure(PlayButtonPath, 512);

            foreach (var path in GameIconPaths())
                Configure(path, 512);
            foreach (var path in NamePlatePaths())
                Configure(path, 1024);           // 이름표에는 글자가 들어 있어서 아이콘보다 크게 잡습니다
        }

        /// <summary>Art/Games 의 아이콘들. Game_01, Game_02 ... 이름 순서입니다.</summary>
        public static List<string> GameIconPaths() => SortedTextures(GamesFolder);

        /// <summary>Art/Names 의 이름표들. Name_01, Name_02 ... 이름 순서입니다.</summary>
        public static List<string> NamePlatePaths() => SortedTextures(NamesFolder);

        static List<string> SortedTextures(string folder)
        {
            var paths = new List<string>();
            if (!Directory.Exists(folder)) return paths;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            paths.Sort(string.CompareOrdinal);   // _01, _02 ... 순서 고정
            return paths;
        }

        const string Marker = "arcade-shell-art-v1";

        static void Configure(string path, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.userData == Marker && importer.maxTextureSize == maxSize) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;                          // 픽셀아트가 뭉개지지 않도록
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = maxSize;
            importer.isReadable = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            importer.userData = Marker;
            importer.SaveAndReimport();
        }

        public static Sprite Load(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogError("[Arcade] 그림을 찾지 못했습니다: " + path +
                               "\n  작업 폴더에 원본이 있는지 확인한 뒤 Tools > Arcade > Import Shell Art 를 눌러 보세요.");
            return sprite;
        }

        /// <summary>Unity 프로젝트의 부모 폴더(D:\00.JumpJump).</summary>
        public static string WorkingDir()
        {
            var projectDir = Directory.GetParent(Application.dataPath);
            var workingDir = projectDir != null ? projectDir.Parent : null;
            return workingDir != null ? workingDir.FullName : Directory.GetCurrentDirectory();
        }
    }
}
