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

        /// <summary>
        /// 글꼴이 들어가는 곳. **Resources 폴더인 것이 중요합니다** —
        /// 실행 중에 `Resources.Load&lt;Font&gt;("GameFont")` 로 찾아 쓰기 때문입니다.
        /// (`ShellUI.GameFont`. 씬에 참조를 저장하는 방식이 아니라서 씬을 다시 굽지 않아도 바뀝니다)
        /// </summary>
        public const string FontFolder = "Assets/Shell/Resources";

        /// <summary>
        /// 소리가 들어가는 곳 (2026-09-13). 실행 중에 <see cref="UiClickSound"/> 가 `Resources.Load` 로 찾습니다.
        /// 작업 폴더(또는 `@리소스` 폴더)의 같은 이름 파일이 더 새로우면 가져옵니다.
        /// </summary>
        public const string SoundFolder = "Assets/Shell/Resources/Sounds";
        public const string ClickSoundPath = SoundFolder + "/Click.wav";

        static readonly (string[] sources, string dest)[] SoundMap =
        {
            (new[] { "Click.wav", "@Click.wav" }, ClickSoundPath),
        };

        /// <summary>
        /// 글꼴 원본을 놓는 폴더. 작업 폴더 아래 `Font` 입니다.
        /// **여기에 `.ttf` 나 `.otf` 를 넣기만 하면 됩니다** — 파일 이름은 아무거나 좋습니다.
        /// </summary>
        public const string FontSourceFolder = "Font";

        public const string TitlePath = Folder + "/Title.jpg";
        public const string TitleTextPath = Folder + "/Title_Text.png";
        public const string LobbyPath = Folder + "/Lobby.png";
        public const string StartButtonPath = Folder + "/Start_Button.png";
        public const string PlayButtonPath = Folder + "/Play_Button.png";

        /// <summary>이름표 그림이 아직 없는 게임에 깔리는 빈 이름표. 없으면 코드가 그려서 만듭니다.</summary>
        public const string BlankPlatePath = Folder + "/NamePlate_Blank.png";

        // --- 팝업(설정 / 종료 / 로비로 나가기) 에 쓰는 그림들 -------------------
        // 프로젝트 안에서는 "무엇에 쓰는 그림인지" 알 수 있는 이름으로 바꿔 둡니다.
        // (작업 폴더의 Popup_01 / Popup_02 만 봐서는 어느 쪽이 어느 쪽인지 알 수 없습니다)

        /// <summary>로비 오른쪽 위 톱니바퀴. 설정 팝업을 엽니다.</summary>
        public const string SettingButtonPath = Folder + "/Setting_Button.png";
        /// <summary>미니게임 오른쪽 위 뒤로가기 화살표.</summary>
        public const string BackButtonPath = Folder + "/Back_Button.png";
        /// <summary>글자가 없는 빈 판. 설정 팝업의 바탕입니다 (위쪽 빈자리는 나중에 사운드 조절용).</summary>
        public const string PanelPath = Folder + "/Popup_Panel.png";
        /// <summary>"로비로 나가시겠습니까?" 가 그려진 판.</summary>
        public const string AskExitPath = Folder + "/Popup_Ask_Exit.png";
        /// <summary>"종료하시겠습니까?" 가 그려진 판.</summary>
        public const string AskQuitPath = Folder + "/Popup_Ask_Quit.png";
        /// <summary>로비 게임 칸마다 붙는 랭킹 아이콘 (2026-09-11 전에는 왼쪽 위 하나). 없으면 코드가 임시 그림을 그려 둡니다.</summary>
        public const string RankButtonPath = Folder + "/Rank_Button.png";
        /// <summary>
        /// 게임 칸의 랭킹 아이콘 뒤에 깔리는 동그란 받침. 아이콘이 배경에 그려진 **칸 번호(1, 2 ...)를 가립니다.**
        /// 없으면 코드가 그려 둡니다. 바꾸려면 작업 폴더에 <c>Rank_Badge.png</c> 를 넣으세요.
        /// </summary>
        public const string RankBadgePath = Folder + "/Rank_Badge.png";
        public const string OkButtonPath = Folder + "/Ok_Button.png";
        public const string CancelButtonPath = Folder + "/Cancel_Button.png";
        public const string EndButtonPath = Folder + "/End_Button.png";
        public const string CloseButtonPath = Folder + "/Close_Button.png";

        /// <summary>작업 폴더의 파일 이름 -> 프로젝트 안 경로. 배경은 자르지 않고 그대로 복사합니다.</summary>
        static readonly (string[] sources, string dest, bool trim)[] Map =
        {
            (new[] { "@Title.jpg", "@Title.png" },                    TitlePath,        false),
            (new[] { "@Title_Text.png" },                             TitleTextPath,    true),
            (new[] { "@Lobby.png", "@Lobby.jpg" },                    LobbyPath,        false),
            (new[] { "@Start_Button.png" },                           StartButtonPath,  true),
            (new[] { "@Play_Button.png" },                            PlayButtonPath,   true),

            (new[] { "Setting.png" },                                 SettingButtonPath, true),
            (new[] { "Undo.png" },                                    BackButtonPath,    true),
            (new[] { "Empty_Pannel.png", "Empty_Panel.png" },         PanelPath,         true),
            (new[] { "Popup_01.png" },                                AskExitPath,       true),
            (new[] { "Popup_02.png" },                                AskQuitPath,       true),
            (new[] { "Ok_Button.png" },                               OkButtonPath,      true),
            (new[] { "Cancel_Button.png" },                           CancelButtonPath,  true),
            (new[] { "End_Button.png" },                              EndButtonPath,     true),
            (new[] { "Close_Button.png" },                            CloseButtonPath,   true),
            (new[] { "Rank.png", "Ranking.png", "Trophy.png" },        RankButtonPath,    true),
            (new[] { "Rank_Badge.png" },                              RankBadgePath,     true),
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
            if (EnsureRankIcon()) changed = true;
            if (EnsureRankBadge()) changed = true;

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
                foreach (var dir in SourceFolders())
                foreach (var source in Directory.GetFiles(dir, pattern))
                {
                    string dest = folder + "/" + Path.GetFileName(source).Substring(1);
                    if (CopyIfNewer(source, dest, true, force, report)) changed = true;
                }
            }

            if (TrimStrayArt(report)) changed = true;
            if (SyncFont(force, report)) changed = true;
            if (SyncSounds(force, report)) changed = true;

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
        /// 작업 폴더의 `Font/` 에 있는 글꼴을 `Assets/Shell/Resources/GameFont.*` 로 가져옵니다.
        ///
        /// **글꼴을 바꾸는 방법은 그 폴더의 파일을 갈아 끼우는 것뿐입니다.** 코드에는 글꼴 이름이 없고,
        /// `ShellUI.GameFont` 가 실행 중에 가져온 파일을 찾아 씁니다.
        /// 폴더가 비어 있으면 Unity 기본 글꼴을 씁니다 — 그래서 글꼴이 없어도 앱은 돌아갑니다.
        ///
        /// 확장자가 다른 글꼴로 갈아 끼우면(.ttf -> .otf) 예전 파일은 지웁니다.
        /// 안 지우면 둘 다 남아서 어느 쪽이 쓰일지 알 수 없게 됩니다.
        /// </summary>
        static bool SyncFont(bool force, StringBuilder report)
        {
            string source = FindFontSource();
            if (source == null) return false;

            Directory.CreateDirectory(FontFolder);

            string extension = Path.GetExtension(source).ToLowerInvariant();
            string dest = FontFolder + "/GameFont" + extension;

            if (!force && File.Exists(dest) && File.GetLastWriteTimeUtc(dest) >= File.GetLastWriteTimeUtc(source))
                return false;

            File.Copy(source, dest, true);
            report.Append("  ").Append(Path.GetFileName(source)).Append(" -> ").Append(dest).Append('\n');

            // 확장자가 다른 옛 글꼴 파일이 남아 있으면 지웁니다.
            foreach (var stale in new[] { FontFolder + "/GameFont.ttf", FontFolder + "/GameFont.otf" })
            {
                if (stale == dest || !File.Exists(stale)) continue;
                AssetDatabase.DeleteAsset(stale);
                report.Append("  옛 글꼴 파일을 지웠습니다: ").Append(stale).Append('\n');
            }

            return true;
        }

        /// <summary>
        /// 작업 폴더의 `Font/` 안에서 쓸 글꼴 파일 하나를 고릅니다.
        ///
        /// **`.ttf` 를 `.otf` 보다 먼저 봅니다** — 둘 다 있으면 ttf 를 씁니다. Unity 가 더 잘 다룹니다.
        /// (같은 글꼴을 여러 형식으로 받는 일이 흔합니다. `.bdf` / `.woff2` / 압축 파일은 무시합니다)
        /// 같은 확장자가 여러 개면 이름 순서로 첫 번째입니다.
        /// </summary>
        static string FindFontSource()
        {
            string folder = Path.Combine(WorkingDir(), FontSourceFolder);
            if (!Directory.Exists(folder)) return null;

            foreach (var pattern in new[] { "*.ttf", "*.otf" })
            {
                var files = Directory.GetFiles(folder, pattern);
                if (files.Length == 0) continue;

                System.Array.Sort(files, string.CompareOrdinal);
                return files[0];
            }

            return null;
        }

        /// <summary>
        /// 글꼴 임포트 설정. 픽셀아트 화면이라 **글자에 뿌연 테두리가 생기지 않게** 잡습니다.
        /// 글꼴 파일이 없으면 아무것도 하지 않습니다.
        /// </summary>
        static void ConfigureFont()
        {
            foreach (var path in new[] { FontFolder + "/GameFont.ttf", FontFolder + "/GameFont.otf" })
            {
                var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
                if (importer == null) continue;
                if (importer.userData == FontMarker) continue;

                // HintedRaster : 글자 모양을 픽셀 격자에 맞춰 또렷하게 굽습니다.
                //                (기본값 Smooth 는 부드럽게 뭉개져서 픽셀 글꼴과 안 어울립니다)
                importer.fontRenderingMode = FontRenderingMode.HintedRaster;
                importer.includeFontData = true;   // 폰에 글꼴을 같이 담습니다. 끄면 한글이 안 나옵니다
                importer.userData = FontMarker;
                importer.SaveAndReimport();

                Debug.Log("[Arcade] 글꼴을 가져왔습니다 -> " + path +
                          "\n  앱 전체(타이틀 / 로비 / 두 미니게임의 HUD)가 이 글꼴을 씁니다.");
            }
        }

        const string FontMarker = "arcade-shell-font-v1";

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

        /// <summary>
        /// 로비 왼쪽 위 **랭킹 아이콘**의 임시 그림을 만들어 둡니다 (1·2·3등 시상대 모양).
        /// 톱니바퀴처럼 진짜 그림을 받으면 그것으로 바뀝니다 —
        /// 작업 폴더(또는 `@리소스 추가_N차`)에 <c>Rank.png</c> 를 넣으면 됩니다.
        /// 파일이 이미 있으면 아무것도 하지 않습니다.
        /// </summary>
        static bool EnsureRankIcon()
        {
            if (File.Exists(RankButtonPath)) return false;

            const int size = 512;
            var edge = new Color(0.231f, 0.106f, 0.078f);     // 팝업 테두리와 같은 진한 갈색
            var fill = new Color(0.898f, 0.729f, 0.478f);
            var top  = new Color(0.976f, 0.867f, 0.647f);

            var pixels = new Color[size * size];              // 기본값 = 투명

            // 시상대 세 칸 : (왼쪽 x, 오른쪽 x, 높이). 가운데가 1등이라 가장 높습니다.
            var bars = new[] { (60, 196, 300), (196, 316, 396), (316, 452, 250) };

            foreach (var (x0, x1, height) in bars)
            {
                Fill(pixels, size, x0, x1, 96, height, edge);                 // 테두리
                Fill(pixels, size, x0 + 14, x1 - 14, 110, height - 14, fill); // 안쪽
                Fill(pixels, size, x0 + 14, x1 - 14, height - 40, height - 14, top); // 윗면 하이라이트
            }

            Fill(pixels, size, 44, 468, 60, 96, edge);        // 바닥

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(RankButtonPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Debug.Log("[Arcade] 랭킹 아이콘 임시 그림을 만들었습니다 -> " + RankButtonPath +
                      "\n  진짜 그림을 쓰시려면 작업 폴더에 Rank.png 를 넣고 Build All Scenes 를 누르세요.");
            return true;
        }

        /// <summary>
        /// 랭킹 아이콘 뒤에 까는 **동그란 받침**을 만들어 둡니다 (빈 이름표와 같은 크림색 + 갈색 테두리).
        /// 게임 칸의 번호 자리에 아이콘을 얹는데, 아이콘만 얹으면 뒤에 그려진 "2" 같은 숫자가
        /// 삐져나와 보여서 받침으로 덮습니다. 파일이 이미 있으면 아무것도 하지 않습니다.
        /// </summary>
        static bool EnsureRankBadge()
        {
            if (File.Exists(RankBadgePath)) return false;

            const int size = 256;
            var fill = new Color(1f, 0.953f, 0.855f);
            var edge = new Color(0.545f, 0.353f, 0.169f);
            float half = size * 0.5f, radius = half - 4f;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;   // 0 보다 작으면 원 안쪽

                    Color c = dist > -14f ? edge : fill;                   // 바깥쪽 14px 은 테두리 색
                    c.a = Mathf.Clamp01(0.5f - dist);
                    pixels[y * size + x] = c;
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(RankBadgePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Debug.Log("[Arcade] 랭킹 아이콘 받침 그림을 만들었습니다 -> " + RankBadgePath);
            return true;
        }

        static void Fill(Color[] pixels, int size, int x0, int x1, int y0, int y1, Color color)
        {
            x0 = Mathf.Clamp(x0, 0, size); x1 = Mathf.Clamp(x1, 0, size);
            y0 = Mathf.Clamp(y0, 0, size); y1 = Mathf.Clamp(y1, 0, size);

            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    pixels[y * size + x] = color;
        }

        /// <summary>
        /// 그림 원본을 찾아 볼 폴더들. 작업 폴더 바로 아래가 먼저고,
        /// 그 다음이 `@리소스` 로 시작하는 하위 폴더들입니다 (이름 순).
        ///
        /// 사용자가 리소스를 `@리소스 추가_1차` 같은 폴더에 묶음으로 넣어 주기 때문입니다.
        /// **뒤에 오는 폴더가 이깁니다** — `_2차` 폴더에 같은 이름의 그림을 넣으면
        /// 코드를 고치지 않아도 그쪽이 쓰입니다.
        /// (검 강화의 검 그림도 같은 폴더들에서 찾습니다 — EnchantArt)
        /// </summary>
        public static List<string> SourceFolders()
        {
            var folders = new List<string> { WorkingDir() };

            var extras = new List<string>(Directory.GetDirectories(WorkingDir(), "@리소스*"));
            extras.Sort(string.CompareOrdinal);
            folders.AddRange(extras);

            return folders;
        }

        static string FindSource(string[] candidates)
        {
            string found = null;
            foreach (var dir in SourceFolders())
            {
                foreach (var name in candidates)
                {
                    string path = Path.Combine(dir, name);
                    if (!File.Exists(path)) continue;
                    found = path;   // 뒤쪽 폴더가 이깁니다
                    break;          // 한 폴더 안에서는 앞에 적은 이름이 이깁니다
                }
            }
            return found;
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

            // 팝업 : 판과 버튼에는 한글이 그려져 있어서 512 로 줄이면 글자가 뭉갭니다.
            Configure(SettingButtonPath, 512);
            Configure(BackButtonPath, 512);
            Configure(RankButtonPath, 512);
            Configure(RankBadgePath, 256);

            // 빈 판은 랭킹 창처럼 **세로로 긴 팝업**에도 쓰기 때문에 9-슬라이스로 잡습니다.
            // 테두리 29px 바로 바깥쪽(34px)을 모서리로 두면, 판을 어떤 크기로 늘려도
            // 테두리 두께가 그대로 유지되고 가운데만 늘어납니다.
            Configure(PanelPath, 1024, new Vector4(PanelBorder, PanelBorder, PanelBorder, PanelBorder));
            Configure(AskExitPath, 1024);
            Configure(AskQuitPath, 1024);
            Configure(OkButtonPath, 1024);
            Configure(CancelButtonPath, 1024);
            Configure(EndButtonPath, 1024);
            Configure(CloseButtonPath, 1024);

            foreach (var path in GameIconPaths())
                Configure(path, 512);
            foreach (var path in NamePlatePaths())
                Configure(path, 1024);           // 이름표에는 글자가 들어 있어서 아이콘보다 크게 잡습니다

            ConfigureFont();
            ConfigureSounds();
        }

        /// <summary>
        /// 로비 아이콘을 동그랗게 자른 사본이 들어가는 곳 (2026-09-13). `Art/Games` **바깥**에 둡니다 —
        /// 그 안에 두면 아이콘 목록(GameIconPaths)에 섞여 게임 순서가 어긋납니다.
        /// </summary>
        public const string RoundIconsFolder = Folder + "/GamesRound";

        /// <summary>
        /// 아이콘을 **가운데 정사각형으로 맞춘 뒤 동그랗게 잘라** RoundIconsFolder 에 PNG 로 씁니다. 원본은 건드리지 않습니다.
        /// 원 밖은 투명, 가장자리 1px 은 반투명으로 부드럽게. 원본이 사본보다 새로우면 다시 자릅니다.
        /// (MiniGameEntry.roundIcon 이 켜진 게임만. 네모난 그림을 로비 칸의 동그라미 안에 넣을 때)
        /// </summary>
        public static Sprite RoundIcon(Sprite icon)
        {
            string source = AssetDatabase.GetAssetPath(icon);
            if (string.IsNullOrEmpty(source) || !File.Exists(source)) return null;
            string dest = RoundIconsFolder + "/" + Path.GetFileNameWithoutExtension(source) + ".png";

            if (!File.Exists(dest) || File.GetLastWriteTimeUtc(dest) < File.GetLastWriteTimeUtc(source))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!texture.LoadImage(File.ReadAllBytes(source))) return null;
                    int w = texture.width, h = texture.height, size = Mathf.Min(w, h);
                    int ox = (w - size) / 2, oy = (h - size) / 2;
                    var src = texture.GetPixels32();
                    var dst = new Color32[size * size];
                    float radius = size * 0.5f;

                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            var p = src[(oy + y) * w + ox + x];
                            float dx = x + 0.5f - radius, dy = y + 0.5f - radius;
                            float cover = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));   // 원 밖 0, 가장자리 1px 부드럽게
                            p.a = (byte)Mathf.RoundToInt(p.a * cover);
                            dst[y * size + x] = p;
                        }

                    var round = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    round.SetPixels32(dst);
                    round.Apply();
                    Directory.CreateDirectory(RoundIconsFolder);
                    File.WriteAllBytes(dest, round.EncodeToPNG());
                    Object.DestroyImmediate(round);
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }

                AssetDatabase.ImportAsset(dest);
                Debug.Log("[Arcade] 로비 아이콘을 동그랗게 잘랐습니다  " + source + " -> " + dest);
            }

            Configure(dest, 512);
            return AssetDatabase.LoadAssetAtPath<Sprite>(dest);
        }

        /// <summary>자체 점검용 — 그림의 왼쪽 아래 모서리가 투명하고 한가운데는 불투명한지 (동그랗게 잘렸는지).</summary>
        public static bool CornerIsTransparent(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) return false;
                return texture.GetPixel(0, 0).a < 0.05f
                    && texture.GetPixel(texture.width / 2, texture.height / 2).a > 0.9f;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>작업 폴더 · `@리소스` 폴더의 소리 파일이 더 새로우면 Resources/Sounds 로 가져옵니다.</summary>
        static bool SyncSounds(bool force, StringBuilder report)
        {
            bool changed = false;
            foreach (var (sources, dest) in SoundMap)
            {
                string source = FindSource(sources);
                if (source == null) continue;
                if (!force && File.Exists(dest) && File.GetLastWriteTimeUtc(dest) >= File.GetLastWriteTimeUtc(source)) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(source, dest, true);
                report.Append("  ").Append(Path.GetFileName(source)).Append(" -> ").Append(dest).Append('\n');
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// 버튼 소리는 아주 짧아서 **압축하지 않고(PCM) 불러올 때 풀어 둡니다** — 누르는 순간 늦지 않게.
        /// 한 채널(모노)로 합칩니다 (화면 UI 소리라 좌우가 필요 없습니다).
        /// </summary>
        static void ConfigureSounds()
        {
            const string marker = "arcade-sound-v1";
            foreach (var (_, path) in SoundMap)
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null || importer.userData == marker) continue;

                importer.forceToMono = true;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.userData = marker;
                importer.SaveAndReimport();
            }
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

        const string Marker = "arcade-shell-art-v2";

        /// <summary>빈 판(Popup_Panel)의 테두리 두께(px). 그림의 실제 테두리가 29px 입니다.</summary>
        public const float PanelBorder = 34f;

        static void Configure(string path, int maxSize, Vector4 border = default)
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
            settings.spriteBorder = border;                                  // 0 이면 늘였을 때 통째로 늘어납니다
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
