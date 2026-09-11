using System.Text;
using UnityEditor;
using UnityEngine;

namespace Arcade.EditorTools
{
    /// <summary>
    /// 랭킹 · 광고 뼈대가 제대로 도는지 **플레이 모드에 들어가지 않고** 확인합니다.
    /// 두 게임의 플레이테스트와 같은 자리(배치 모드)에서 돌릴 수 있습니다.
    ///
    /// 여기서 확인하는 것은 화면이 아니라 **규칙**입니다.
    ///  - 한 판이 끝나면 판 수가 하나 늘어나는가
    ///  - <b>2판마다</b> 광고를 띄울 때가 되는가
    ///  - 광고를 띄운 직후에는 (최소 간격 때문에) 다시 뜨지 않는가
    ///  - 별명이 다듬어지고 길이가 잘리는가
    ///  - 점수를 올리면 내 최고 점수와 순위가 잡히는가
    ///
    /// 검사가 끝나면 <b>건드린 PlayerPrefs 를 전부 원래대로 되돌립니다.</b>
    /// (실제로 게임을 하신 판 수·기록이 검사 때문에 바뀌면 안 되니까요)
    /// </summary>
    public static class ArcadeSelfTest
    {
        /// <summary>숫자로 저장되는 키. 되돌릴 때 타입을 맞춰야 해서 따로 둡니다.</summary>
        static readonly string[] IntKeys =
        {
            "Arcade.PlayCount", "Arcade.PlaysSinceAd", "Arcade.Rank.selftest.Best",
        };

        static readonly string[] StringKeys =
        {
            "Arcade.LastAdUtcTicks", "Arcade.PlayerId", "Arcade.ServerId", "Arcade.Nickname",
            "Arcade.NicknameRegistered",
        };

        [MenuItem("Tools/Arcade/Run Ranking + Ads Self Test", priority = 23)]
        public static void Run()
        {
            var savedInts = new int[IntKeys.Length];
            var hadInt = new bool[IntKeys.Length];
            for (int i = 0; i < IntKeys.Length; i++)
            {
                hadInt[i] = PlayerPrefs.HasKey(IntKeys[i]);
                if (hadInt[i]) savedInts[i] = PlayerPrefs.GetInt(IntKeys[i], 0);
            }

            var savedStrings = new string[StringKeys.Length];
            var hadString = new bool[StringKeys.Length];
            for (int i = 0; i < StringKeys.Length; i++)
            {
                hadString[i] = PlayerPrefs.HasKey(StringKeys[i]);
                if (hadString[i]) savedStrings[i] = PlayerPrefs.GetString(StringKeys[i], "");
            }

            var log = new StringBuilder();
            int failed = 0;

            try
            {
                foreach (var key in IntKeys) PlayerPrefs.DeleteKey(key);
                foreach (var key in StringKeys) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();

                var config = ShellConfigAsset.LoadOrCreate();
                GameSession.Recording = true;

                log.Append("설정: ").Append(config.playsPerAd).Append("판마다 / 최소 간격 ")
                   .Append(config.minSecondsBetweenAds).Append("초\n");

                // ---- 판 수와 광고 시점 -------------------------------------------------
                int before = PlayCounter.Total;
                Report("jumpjump", 1000);
                failed += Check(log, "한 판을 끝내면 판 수가 1 늘어난다",
                                PlayCounter.Total == before + 1);
                failed += Check(log, "1판째에는 아직 광고를 띄우지 않는다",
                                config.playsPerAd <= 1 || !AdGate.ShouldShow);

                for (int i = 1; i < config.playsPerAd; i++) Report("jumpjump", 1000 + i);
                failed += Check(log, config.playsPerAd + "판째에 광고를 띄울 때가 된다", AdGate.ShouldShow);

                AdGate.MarkShown();
                failed += Check(log, "광고를 띄운 직후에는 다시 띄우지 않는다", !AdGate.ShouldShow);

                for (int i = 0; i < config.playsPerAd; i++) Report("archery", 200 + i);
                failed += Check(log, "판 수를 다시 채워도 최소 간격 안에서는 뜨지 않는다",
                                config.minSecondsBetweenAds <= 0f || !AdGate.ShouldShow);

                // ---- 별명 -------------------------------------------------------------
                PlayerIdentity.Nickname = "   김  하늘   ";
                failed += Check(log, "별명의 앞뒤·연속 공백이 정리된다  -> \"" + PlayerIdentity.Nickname + "\"",
                                PlayerIdentity.Nickname == "김 하늘");

                PlayerIdentity.Nickname = new string('가', config.nicknameMaxLength + 5);
                failed += Check(log, "별명이 최대 " + config.nicknameMaxLength + "글자로 잘린다",
                                PlayerIdentity.Nickname.Length == config.nicknameMaxLength);

                failed += Check(log, "PID 가 만들어져 있다  -> " + Shorten(PlayerIdentity.Id),
                                !string.IsNullOrEmpty(PlayerIdentity.Id));

                // ---- 별명 : 서버 이름표 · 금지어 (2026-09-11) ----------------------------
                failed += Check(log, "영문 대소문자가 달라도 같은 별명으로 본다 (Tom = tOM)",
                                PlayerIdentity.KeyOf("Tom") == PlayerIdentity.KeyOf("tOM"));
                failed += Check(log, "별명에서 빗금(/)을 뺀다  -> \"" + PlayerIdentity.Sanitize("a/b\\c") + "\"",
                                PlayerIdentity.Sanitize("a/b\\c") == "abc");
                failed += Check(log, "점(.) 하나짜리 별명은 쓸 수 없다",
                                NicknameRules.Check(".") == NicknameProblem.Invalid);

                NicknameRules.Reload();
                failed += Check(log, "금지어 표를 읽었다  -> " + NicknameRules.WordCount + "개",
                                NicknameRules.WordCount > 1000);
                foreach (var bad in new[] { "시발", "시 발", "SiBaL", "개새끼야" })
                    failed += Check(log, "금지어가 든 별명을 막는다  -> \"" + bad + "\"",
                                    NicknameRules.Check(bad) == NicknameProblem.BadWord);
                foreach (var good in new[] { "홍길동", "김하늘", "점프왕" })
                    failed += Check(log, "멀쩡한 별명은 통과한다  -> \"" + good + "\"",
                                    NicknameRules.Check(good) == NicknameProblem.None);

                PlayerIdentity.UseServerId("uid-A");
                PlayerIdentity.Nickname = "하늘";
                PlayerIdentity.MarkNicknameRegistered("uid-A", "하늘");
                bool registered = PlayerIdentity.IsNicknameRegistered;
                PlayerIdentity.Nickname = "바다";
                bool afterRename = PlayerIdentity.IsNicknameRegistered;
                failed += Check(log, "서버 등록 표시는 그 별명에만 붙는다 (별명이 바뀌면 다시 등록)",
                                registered && !afterRename);

                // ---- 서버 답 읽기 (Firestore 모양의 JSON) ---------------------------------
                var parsed = Online.MiniJson.Parse(
                    "[{\"document\":{\"name\":\"projects/p/databases/(default)/documents/ranks/jumpjump/scores/uid-A\"," +
                    "\"fields\":{\"nick\":{\"stringValue\":\"하늘 \\\"별\\\"\"},\"score\":{\"integerValue\":\"47900\"}}}}]");
                var doc = Online.MiniJson.Dig((parsed as System.Collections.Generic.List<object>)?[0], "document");
                failed += Check(log, "서버 답에서 별명·점수·PID 를 꺼낸다",
                                Online.Firestore.GetString(doc, "nick") == "하늘 \"별\"" &&
                                Online.Firestore.GetInt(doc, "score") == 47900 &&
                                Online.Firestore.LastSegment(doc) == "uid-A");
                string round = Online.MiniJson.Serialize(new System.Collections.Generic.Dictionary<string, object>
                    { { "nick", "줄\n바꿈 \"따옴표\"" }, { "score", 12L } });
                failed += Check(log, "보낼 JSON 을 쓰고 다시 읽으면 그대로다",
                                Online.MiniJson.DigString(Online.MiniJson.Parse(round), "nick") == "줄\n바꿈 \"따옴표\"");

                // ---- 랭킹 서버 주소 ------------------------------------------------------
                ShellFirebaseConfig.Sync();
                failed += Check(log, "google-services.json 에서 서버 주소를 가져왔다  -> " + config.firebaseProjectId,
                                config.HasFirebase);

                // ---- 글자표 ------------------------------------------------------------
                StringTable.Reload();
                foreach (var key in new[] { "settings.nick", "settings.nick.change", "nick.bad", "nick.title.change" })
                    failed += Check(log, "strings.csv 에 새 줄이 있다  -> " + key + " = \"" + StringTable.Get(key, "") + "\"",
                                    StringTable.Get(key, "").Length > 0);

                // ---- 로비 씬 : 랭킹 탭 · 게임 칸 랭킹 아이콘 (수정사항_02) --------------------
                failed += CheckLobbyScene(log);

                // ---- 랭킹 -------------------------------------------------------------
                PlayerIdentity.Nickname = "김 하늘";
                var ranking = new LocalRankingService(useSamples: false);
                bool submitted = false;
                ranking.Submit("selftest", PlayerIdentity.Nickname, 500, r => submitted = r == SubmitResult.Ok);
                failed += Check(log, "점수를 올릴 수 있다", submitted);

                ranking.Submit("selftest", PlayerIdentity.Nickname, 300);
                failed += Check(log, "더 낮은 점수는 내 기록을 덮어쓰지 않는다",
                                LocalRankingService.BestOf("selftest") == 500);

                RankingPage page = null;
                ranking.Fetch("selftest", config.rankingTopCount, p => page = p);
                failed += Check(log, "랭킹을 가져오면 내 줄이 1등으로 잡힌다",
                                page != null && page.ok && page.myRank == 1 && page.myScore == 500);

                // ---- 배치 플레이테스트 보호 --------------------------------------------
                GameSession.Recording = false;
                int locked = PlayCounter.Total;
                Report("jumpjump", 999);
                failed += Check(log, "Recording 을 끄면 판 수가 늘지 않는다 (플레이테스트 보호)",
                                PlayCounter.Total == locked);
            }
            finally
            {
                GameSession.Recording = true;

                for (int i = 0; i < IntKeys.Length; i++)
                {
                    if (hadInt[i]) PlayerPrefs.SetInt(IntKeys[i], savedInts[i]);
                    else PlayerPrefs.DeleteKey(IntKeys[i]);
                }
                for (int i = 0; i < StringKeys.Length; i++)
                {
                    if (hadString[i]) PlayerPrefs.SetString(StringKeys[i], savedStrings[i]);
                    else PlayerPrefs.DeleteKey(StringKeys[i]);
                }
                PlayerPrefs.Save();
                PlayerIdentity.Reload();
            }

            if (failed == 0)
                Debug.Log("[Arcade 자체 점검] 전부 통과\n" + log);
            else
                Debug.LogError("[Arcade 자체 점검] " + failed + "건 실패\n" + log);
        }

        /// <summary>
        /// 구워진 로비 씬을 열어 봅니다. **이번에 고친 버그를 다시 못 들어오게 막는 검사입니다.**
        ///  - 랭킹 창의 탭이 0, 1, 2 ... 로 구워졌는가 (예전에는 전부 0 이라 탭을 눌러도 안 바뀌었습니다)
        ///  - 탭을 누르면 / 게임 칸 아이콘으로 열면 그 게임이 골라지는가
        ///  - 로비 왼쪽 위 랭킹 아이콘이 없어지고, 게임 칸마다 아이콘이 붙었는가
        /// </summary>
        static int CheckLobbyScene(StringBuilder log)
        {
            string path = ShellSceneBuilder.LobbyScenePath;
            var already = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !already.isLoaded;
            var scene = opened
                ? UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive)
                : already;

            int failed = 0;
            try
            {
                var tabs = new System.Collections.Generic.List<RankingTab>();
                RankingPopup view = null;
                LobbyScreen lobby = null;
                bool oldIcon = false;

                foreach (var root in scene.GetRootGameObjects())
                {
                    tabs.AddRange(root.GetComponentsInChildren<RankingTab>(true));
                    if (view == null) view = root.GetComponentInChildren<RankingPopup>(true);
                    if (lobby == null) lobby = root.GetComponentInChildren<LobbyScreen>(true);
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name == "RankButton" && t.parent != null && t.parent.name == "Backdrop") oldIcon = true;
                }

                var indices = new System.Collections.Generic.List<int>();
                foreach (var tab in tabs) indices.Add(new SerializedObject(tab).FindProperty("index").intValue);
                indices.Sort();

                bool distinct = indices.Count >= 2;
                for (int i = 0; i < indices.Count; i++) distinct &= indices[i] == i;
                failed += Check(log, "랭킹 창 탭 번호가 0, 1, ... 로 구워졌다  -> [" + string.Join(", ", indices) + "]", distinct);

                if (view != null)
                {
                    view.SelectTab(1);
                    string second = view.SelectedGameId;
                    view.ShowFor("jumpjump");
                    string first = view.SelectedGameId;
                    view.ShowFor("archery");
                    failed += Check(log, "탭을 누르면 게임이 바뀐다 / 게임 칸에서 열면 그 게임이 골라진다  -> " +
                                         second + ", " + first + ", " + view.SelectedGameId,
                                    second == "archery" && first == "jumpjump" && view.SelectedGameId == "archery");
                    view.GetComponentInParent<PopupPanel>(true)?.Close();
                }
                else
                {
                    failed += Check(log, "로비에 랭킹 창이 있다", false);
                }

                bool wired = false;
                if (lobby != null)
                {
                    var so = new SerializedObject(lobby);
                    wired = so.FindProperty("rankIcon").objectReferenceValue != null
                            && so.FindProperty("ranking").objectReferenceValue != null;
                }
                failed += Check(log, "로비 왼쪽 위 랭킹 아이콘은 없어지고, 게임 칸마다 붙을 준비가 되었다",
                                !oldIcon && wired);
            }
            finally
            {
                if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }

            return failed;
        }

        static void Report(string gameId, int score)
        {
            GameSession.ReportRunFinished(new RunResult(gameId, score, "SELF TEST"));
        }

        static int Check(StringBuilder log, string what, bool ok)
        {
            log.Append(ok ? "  통과  " : "  실패  ").Append(what).Append('\n');
            return ok ? 0 : 1;
        }

        static string Shorten(string id)
        {
            return string.IsNullOrEmpty(id) || id.Length <= 8 ? id : id.Substring(0, 8) + "...";
        }
    }
}
