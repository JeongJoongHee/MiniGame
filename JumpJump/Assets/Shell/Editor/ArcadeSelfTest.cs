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

                // ---- 랭킹 -------------------------------------------------------------
                var ranking = new LocalRankingService(useSamples: false);
                bool submitted = false;
                ranking.Submit("selftest", PlayerIdentity.Nickname, 500, ok => submitted = ok);
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
            }

            if (failed == 0)
                Debug.Log("[Arcade 자체 점검] 전부 통과\n" + log);
            else
                Debug.LogError("[Arcade 자체 점검] " + failed + "건 실패\n" + log);
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
