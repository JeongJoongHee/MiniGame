using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Enchant.EditorTools
{
    /// <summary>
    /// 플레이 모드에 들어가지 않고 검 강화를 그대로 돌려 보는 자동 점검.
    /// EnchantGame.Step(dt, action) 을 직접 부르므로 실제 게임과 완전히 같은 코드를 검증합니다.
    ///
    ///  1) 규칙 점검 — 일반 강화 실패 = 파괴 / 안전 강화 실패 = 그대로 / 분해는 표의 수치부터 / 강화 중 연타 무시 ...
    ///  2) 표 읽기 점검 — 기획서의 오타 열 이름 · % 표기 · 빈 칸 이어받기 · 확률 0 = 최고 단계
    ///  3) 확률 점검 — 표의 확률마다 수천 번 눌러서 **실제 성공률이 표와 맞는지**
    ///  4) 봇 — 밸런스를 볼 수 있게 "몇 번 눌러야 +20 에 가는지" 를 잽니다 (통과/실패 없음, 참고용)
    ///
    /// 확률은 씨앗을 고정해서 돌리므로 매번 같은 결과가 나옵니다.
    /// **폰의 기록(PlayerPrefs)은 건드리지 않습니다** — 저장 점검 한 건만 잠깐 쓰고 원래대로 되돌립니다.
    /// </summary>
    public static class EnchantPlaytest
    {
        const float Dt = 1f / 60f;
        /// <summary>강화 한 번을 한 걸음에 끝내려고 쓰는 큰 시간 간격 (연출 시간을 건너뜁니다).</summary>
        const float Jump = 5f;

        [MenuItem("Tools/Enchant/Run Headless Playtest", priority = 20)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 봇이 돌린 수만 번의 강화가 광고 카운터 · 랭킹에 섞이면 안 됩니다.
            Arcade.GameSession.Recording = false;

            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var game = EnchantSceneBuilder.Populate();
                if (game == null) return;

                game.SaveProgress = false;
                game.Boot();
                var config = game.Config;

                var log = new StringBuilder();
                int failed = 0;
                failed += CheckRules(game, config, log);
                failed += CheckTable(game, config, log);
                failed += CheckOdds(game, config, log);
                failed += CheckReportAndSave(game, config, log);

                if (failed == 0) Debug.Log("[Enchant 플레이테스트] 규칙 점검 전부 통과\n" + log);
                else Debug.LogError("[Enchant 플레이테스트] " + failed + "건 실패\n" + log);

                Debug.Log("[Enchant 플레이테스트] 봇 (밸런스 참고용)\n" + RunBots(game, config));
            }
            finally
            {
                Arcade.GameSession.Recording = true;
            }
        }

        // ------------------------------------------------------------------ 1) 규칙

        static int CheckRules(EnchantGame game, EnchantConfig config, StringBuilder log)
        {
            int failed = 0;
            game.Seed(1234);

            // 표에서 시험할 수치를 고릅니다 (사용자가 표를 고쳐도 점검이 따라가도록 숫자를 박지 않습니다)
            int risky = RiskiestLevel(config);        // 확률이 가장 낮은 (0 은 뺀) 수치
            int meltLevel = config.FirstMeltLevel;
            log.Append($"  (시험에 쓴 수치 : 가장 위험한 +{risky} = {EnchantTable.Percent(config.SuccessChance(risky))}, " +
                       $"분해는 +{meltLevel} 부터)\n");

            // 강화 중 연타는 한 번으로
            game.LoadProgress(0, 0, 0);
            int attemptsBefore = game.Attempts;
            game.Step(Dt, EnchantAction.Normal);
            bool working = game.State == EnchantState.Working;
            int frames = 0;
            while (game.State == EnchantState.Working && frames < 600)
            {
                game.Step(Dt, EnchantAction.Normal);   // 결과가 날 때까지 매 프레임 연타
                frames++;
            }
            float waited = frames * Dt;
            failed += Check(log, $"강화 버튼을 누르면 결과까지 {waited:0.00}초 기다리고, 그동안 연타해도 한 번만 강화된다  -> +{game.Level}",
                            working && waited >= config.workSeconds - Dt && game.Attempts == attemptsBefore + 1 &&
                            (config.SuccessChance(0) < 10000 || game.Level == 1));

            // 일반 강화 실패 = 파괴
            BreakAt(game, risky, 3);
            failed += Check(log, $"일반 강화가 실패하면 검이 파괴되어 +0 이 된다 (주문서는 그대로)  -> +{game.BrokenLevel} 파괴, 주문서 {game.SafeScrolls}",
                            game.LastOutcome == EnchantOutcome.Destroyed && game.State == EnchantState.Broken &&
                            game.Level == 0 && game.BrokenLevel == risky && game.SafeScrolls == 3);

            // 부서진 뒤 [새 검 받기] 는 잠깐 잠겨 있다
            game.Step(Dt, EnchantAction.NewSword);
            bool locked = game.State == EnchantState.Broken;
            game.Step(config.newSwordLockSeconds, EnchantAction.NewSword);
            failed += Check(log, $"부서진 직후 {config.newSwordLockSeconds}초 동안은 [새 검 받기] 가 안 먹는다 (연타 · 광고 오클릭 방지)",
                            locked && game.State == EnchantState.Idle && game.Level == 0);

            // 부서진 동안에는 강화 · 분해 버튼이 안 먹는다
            BreakAt(game, risky, 3);
            game.Step(Dt, EnchantAction.Safe);
            game.Step(Dt, EnchantAction.Melt);
            failed += Check(log, "검이 부서진 화면에서는 다른 버튼이 안 먹는다",
                            game.State == EnchantState.Broken && game.SafeScrolls == 3);

            // 안전 강화 실패 = 그대로, 주문서 1장 씀
            int tries = 0;
            do
            {
                game.LoadProgress(risky, 5, risky);
                Enchant(game, EnchantAction.Safe);
                tries++;
            } while (game.LastOutcome == EnchantOutcome.Success && tries < 200);
            failed += Check(log, $"안전 강화가 실패해도 검은 그대로다  -> +{game.Level}, 주문서 5 -> {game.SafeScrolls}",
                            game.LastOutcome == EnchantOutcome.SafeFail && game.State == EnchantState.Idle &&
                            game.Level == risky && game.SafeScrolls == 4);

            // 안전 강화 성공도 주문서 1장
            game.LoadProgress(0, 2, 0);
            Enchant(game, EnchantAction.Safe);
            failed += Check(log, "안전 강화는 성공해도 주문서를 1장 쓴다",
                            config.SuccessChance(0) < 10000 || (game.Level == 1 && game.SafeScrolls == 1));

            game.LoadProgress(risky, 0, risky);
            Enchant(game, EnchantAction.Safe);
            failed += Check(log, "주문서가 없으면 안전 강화를 할 수 없다",
                            game.State == EnchantState.Idle && game.Level == risky &&
                            game.LastOutcome == EnchantOutcome.None);

            // 분해
            if (meltLevel > 0)
            {
                game.LoadProgress(meltLevel - 1, 0, meltLevel - 1);
                game.Step(Dt, EnchantAction.Melt);
                failed += Check(log, $"+{meltLevel - 1} 에서는 분해할 수 없다",
                                game.Level == meltLevel - 1 && game.SafeScrolls == 0);

                game.LoadProgress(meltLevel, 0, meltLevel);
                game.Step(Dt, EnchantAction.Melt);
                int expected = config.MeltChance(meltLevel) >= 10000 ? config.MeltScrolls(meltLevel) : game.SafeScrolls;
                failed += Check(log, $"+{meltLevel} 에서 분해하면 +0 이 되고 주문서를 받는다  -> 주문서 {game.SafeScrolls}",
                                game.Level == 0 && game.SafeScrolls == expected && game.State == EnchantState.Idle &&
                                (game.LastOutcome == EnchantOutcome.Melted || game.LastOutcome == EnchantOutcome.MeltEmpty));
            }
            else
            {
                log.Append("  참고  표에 분해 줄(Melt 1 이상)이 없어 분해 점검을 건너뜁니다\n");
            }

            // 최고 기록
            game.LoadProgress(4, 0, 9);
            Enchant(game, EnchantAction.Normal);
            failed += Check(log, "최고 기록은 더 높이 올렸을 때만 바뀐다",
                            game.BestLevel == Mathf.Max(9, game.Level));

            // 표보다 높은 수치
            int beyond = config.LastTableLevel + 10;
            failed += Check(log, $"표보다 높은 +{beyond} 은(는) 마지막 줄 값을 쓴다  -> {EnchantTable.Percent(config.SuccessChance(beyond))}",
                            config.SuccessChance(beyond) == config.SuccessChance(config.LastTableLevel));

            return failed;
        }

        // ------------------------------------------------------------------ 2) 표 읽기

        static int CheckTable(EnchantGame game, EnchantConfig config, StringBuilder log)
        {
            int failed = 0;

            int dataLines = 0;
            if (config.levelCsv != null)
                foreach (var line in config.levelCsv.text.Split('\n'))
                    if (line.Trim().Length > 0 && char.IsDigit(line.Trim()[0])) dataLines++;
            failed += Check(log, $"@Enchant_Sword.csv 를 전부 읽었다  -> {config.Levels.Length}줄 (파일의 숫자 줄 {dataLines})",
                            config.levelCsv != null && config.Levels.Length == dataLines && dataLines > 0);

            // 기획서의 오타 열 이름 / % 표기 / 빈 칸 이어받기 / 순서 뒤섞임 / BOM / CRLF
            string messy = "﻿Enchat_Level,Enchant_Prob,Melt,Image,Note\r\n0,10000,0,A,memo\r\n2,90%,,B,\r\n1,,1,,\r\n";
            var rows = EnchantTable.Parse(messy, out string report);
            failed += Check(log, "표 읽기 : 오타 열 이름(Enchat_Level) · 90% 표기 · 빈 칸 이어받기 · 뒤섞인 순서",
                            rows != null && rows.Length == 3 && rows[1].level == 1 && rows[2].successPer10000 == 9000 &&
                            rows[1].meltScrolls == 1 && rows[1].image == "B" && report.Length > 0);

            failed += Check(log, "표 읽기 : 강화 수치 열이 없으면 읽지 않는다 (게임은 인스펙터의 예비 목록을 씀)",
                            EnchantTable.Parse("Prob,Image\n10000,A\n", out _) == null);

            // 확률 0 = 최고 단계. 게임에 잠깐 다른 표를 끼워서 버튼이 안 먹는지 봅니다.
            var capped = ScriptableObject.CreateInstance<EnchantConfig>();
            capped.levels = EnchantTable.Parse("Level,Prob,Melt,Image\n0,10000,0,A\n3,0,1,A\n", out _);
            var so = new SerializedObject(game);
            var prop = so.FindProperty("config");
            var original = prop.objectReferenceValue;
            prop.objectReferenceValue = capped;
            so.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                game.LoadProgress(3, 2, 3);
                Enchant(game, EnchantAction.Normal);
                Enchant(game, EnchantAction.Safe);
                failed += Check(log, "확률 0 인 줄이 최고 단계다 (강화 버튼 둘 다 안 먹고, 분해는 된다)",
                                !capped.CanEnchant(3) && capped.CanEnchant(2) && game.Level == 3 && game.SafeScrolls == 2);
            }
            finally
            {
                prop.objectReferenceValue = original;
                so.ApplyModifiedPropertiesWithoutUndo();
                Object.DestroyImmediate(capped);
            }

            return failed;
        }

        // ------------------------------------------------------------------ 3) 확률

        /// <summary>표에 나오는 확률마다 수천 번 눌러서 실제 성공률이 표와 맞는지 봅니다.</summary>
        static int CheckOdds(EnchantGame game, EnchantConfig config, StringBuilder log)
        {
            int failed = 0;
            const int n = 4000;
            game.Seed(20260911);

            var seen = new HashSet<int>();
            foreach (var row in config.Levels)
            {
                int p = row.successPer10000;
                if (p <= 0 || !seen.Add(p)) continue;

                int wins = 0;
                for (int i = 0; i < n; i++)
                {
                    game.LoadProgress(row.level, 0, row.level);
                    Enchant(game, EnchantAction.Normal);
                    if (game.LastOutcome == EnchantOutcome.Success) wins++;
                }

                float expected = p / 10000f;
                float observed = wins / (float)n;
                float tolerance = Mathf.Max(0.02f, 4f * Mathf.Sqrt(expected * (1f - expected) / n));
                failed += Check(log, $"확률 : +{row.level} 표 {EnchantTable.Percent(p)} -> 실제 {observed * 100f:0.0}% ({n}번)",
                                Mathf.Abs(observed - expected) <= tolerance);
            }

            return failed;
        }

        // ------------------------------------------------------------------ 4) 판 종료 신호 · 저장

        static int CheckReportAndSave(EnchantGame game, EnchantConfig config, StringBuilder log)
        {
            int failed = 0;

            // 검이 부서지면 "한 판 끝" 신호가 그 검의 수치로 나간다 (랭킹 · 광고가 이것을 듣습니다).
            // 신호를 켜면 판 수를 세므로, 건드린 판 수 기록은 끝나고 되돌립니다.
            string[] keys = { "Arcade.PlayCount", "Arcade.PlaysSinceAd" };
            var saved = new int?[keys.Length];
            for (int i = 0; i < keys.Length; i++) saved[i] = PlayerPrefs.HasKey(keys[i]) ? PlayerPrefs.GetInt(keys[i]) : (int?)null;

            var results = new List<Arcade.RunResult>();
            void Listen(Arcade.RunResult r) => results.Add(r);
            Arcade.GameSession.RunFinished += Listen;
            Arcade.GameSession.Recording = true;
            try
            {
                int risky = RiskiestLevel(config);
                BreakAt(game, risky, 0);

                int meltLevel = config.FirstMeltLevel;
                if (meltLevel >= 0)
                {
                    game.LoadProgress(meltLevel, 0, meltLevel);
                    game.Step(Dt, EnchantAction.Melt);
                }

                bool ok = results.Count == (meltLevel >= 0 ? 2 : 1) &&
                          results[0].gameId == EnchantGame.GameId && results[0].score == risky &&
                          (meltLevel < 0 || results[1].score == meltLevel);
                failed += Check(log, $"검이 끝나면(파괴 · 분해) 그 검의 수치로 랭킹 신호가 나간다  -> " +
                                     string.Join(", ", results.ConvertAll(r => r.gameId + " " + r.score + " (" + r.reason + ")")),
                                ok);
            }
            finally
            {
                Arcade.GameSession.RunFinished -= Listen;
                Arcade.GameSession.Recording = false;
                for (int i = 0; i < keys.Length; i++)
                {
                    if (saved[i].HasValue) PlayerPrefs.SetInt(keys[i], saved[i].Value);
                    else PlayerPrefs.DeleteKey(keys[i]);
                }
                PlayerPrefs.Save();
            }

            // 진행 상황 저장 (강화 수치 · 주문서 · 최고 기록). 실제 기록은 끝나고 되돌립니다.
            string[] progress = { "Enchant.Level", "Enchant.SafeScrolls", "Enchant.BestLevel" };
            var before = new int?[progress.Length];
            for (int i = 0; i < progress.Length; i++) before[i] = PlayerPrefs.HasKey(progress[i]) ? PlayerPrefs.GetInt(progress[i]) : (int?)null;
            try
            {
                game.SaveProgress = true;
                game.LoadProgress(12, 3, 15);
                failed += Check(log, "강화 수치 · 주문서 · 최고 기록이 폰에 저장된다 (로비에 나갔다 와도 이어짐)",
                                PlayerPrefs.GetInt(progress[0], -1) == 12 && PlayerPrefs.GetInt(progress[1], -1) == 3 &&
                                PlayerPrefs.GetInt(progress[2], -1) == 15);
            }
            finally
            {
                game.SaveProgress = false;
                for (int i = 0; i < progress.Length; i++)
                {
                    if (before[i].HasValue) PlayerPrefs.SetInt(progress[i], before[i].Value);
                    else PlayerPrefs.DeleteKey(progress[i]);
                }
                PlayerPrefs.Save();
            }

            return failed;
        }

        // ------------------------------------------------------------------ 봇

        static string RunBots(EnchantGame game, EnchantConfig config)
        {
            var sb = new StringBuilder();
            int meltLevel = config.FirstMeltLevel;

            // (1) 일반 강화만 누르는 봇 : 검 한 자루가 어디서 부서지는지
            game.Seed(7);
            game.LoadProgress(0, 0, 0);
            const int swords = 5000;
            var broken = new List<int>();
            long presses = 0;
            while (broken.Count < swords && presses < 5000000)
            {
                if (!config.CanEnchant(game.Level))   // 표의 최고 단계에 닿음 — 그 자리를 기록하고 새로
                {
                    broken.Add(game.Level);
                    game.LoadProgress(0, 0, game.BestLevel);
                    continue;
                }
                if (game.State == EnchantState.Broken)
                {
                    broken.Add(game.BrokenLevel);
                    game.Step(Jump, EnchantAction.NewSword);
                    continue;
                }
                Enchant(game, EnchantAction.Normal);
                presses++;
            }
            if (broken.Count == 0) return "  (검이 한 번도 부서지지 않았습니다 — 표의 확률이 전부 100% 인지 확인하세요)\n";
            broken.Sort();
            int swordsDone = broken.Count;
            sb.Append($"  [일반 강화만] 검 {swordsDone}자루 · {presses:N0}번 누름 (한 자루에 평균 {presses / (float)swordsDone:0.#}번)\n")
              .Append($"    부서진 수치 : 가운데값 +{broken[swordsDone / 2]} / 상위 10% +{broken[swordsDone * 9 / 10]} / 최고 +{broken[swordsDone - 1]}\n")
              .Append("    그 수치까지 간 검 : ");
            foreach (int mark in new[] { 10, 15, 20, 25 })
            {
                int reached = broken.FindAll(l => l >= mark).Count;
                sb.Append($"+{mark} {reached * 100f / swordsDone:0.#}%   ");
            }
            sb.Append('\n');

            // (2) 분해로 주문서를 모으는 봇 : +분해 수치에서 주문서가 모자라면 분해, 넉넉하면 안전 강화로 밀어붙이기
            if (meltLevel > 0)
            {
                foreach (int reserve in new[] { 5, 10, 20 })
                {
                    game.Seed(100 + reserve);
                    game.LoadProgress(0, config.startSafeScrolls, 0);
                    var firstAt = new Dictionary<int, long>();
                    long taps = 0;
                    const long budget = 30000;

                    while (taps < budget)
                    {
                        if (game.State == EnchantState.Broken) { game.Step(Jump, EnchantAction.NewSword); taps++; continue; }

                        EnchantAction action;
                        if (game.Level < meltLevel) action = EnchantAction.Normal;
                        else if (game.Level == meltLevel && game.SafeScrolls < reserve) action = EnchantAction.Melt;
                        else action = game.SafeScrolls > 0 && config.CanEnchant(game.Level) ? EnchantAction.Safe : EnchantAction.Normal;

                        if (action == EnchantAction.Melt) game.Step(Dt, EnchantAction.Melt);
                        else Enchant(game, action);
                        taps++;

                        foreach (int mark in new[] { 15, 20, 25, 30, 35, 40 })
                            if (game.Level >= mark && !firstAt.ContainsKey(mark)) firstAt[mark] = taps;
                    }

                    sb.Append($"  [분해 파밍 · 주문서 {reserve}장 모이면 밀기] {budget:N0}번 누름 -> 최고 +{game.BestLevel}\n    처음 도달 : ");
                    foreach (int mark in new[] { 15, 20, 25, 30, 35, 40 })
                        sb.Append(firstAt.TryGetValue(mark, out long at) ? $"+{mark} {at:N0}번   " : $"+{mark} -   ");
                    sb.Append('\n');
                }
            }

            sb.Append($"  (강화 한 번 = 버튼 한 번 + 연출 {config.workSeconds}초. 1만 번 누름 ≈ 강화만 해도 {10000 * config.workSeconds / 60f:0}분 이상)\n");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ 도우미

        /// <summary>강화 버튼 한 번 누르고 결과까지. 연출 시간을 한 걸음에 건너뜁니다.</summary>
        static void Enchant(EnchantGame game, EnchantAction kind)
        {
            game.Step(Dt, kind);
            if (game.State == EnchantState.Working) game.Step(Jump, EnchantAction.None);
        }

        /// <summary>그 수치에서 일반 강화를 검이 부서질 때까지 다시 시도합니다 (확률이 100% 면 500번 뒤 포기).</summary>
        static void BreakAt(EnchantGame game, int level, int scrolls)
        {
            for (int i = 0; i < 500; i++)
            {
                game.LoadProgress(level, scrolls, level);
                Enchant(game, EnchantAction.Normal);
                if (game.LastOutcome == EnchantOutcome.Destroyed) return;
            }
        }

        /// <summary>확률이 가장 낮은 수치 (0 은 뺍니다). 실패를 보려고 씁니다.</summary>
        static int RiskiestLevel(EnchantConfig config)
        {
            int best = 0, lowest = int.MaxValue;
            foreach (var row in config.Levels)
                if (row.successPer10000 > 0 && row.successPer10000 < lowest) { lowest = row.successPer10000; best = row.level; }
            return best;
        }

        static int Check(StringBuilder log, string what, bool ok)
        {
            log.Append(ok ? "  통과  " : "  실패  ").Append(what).Append('\n');
            return ok ? 0 : 1;
        }
    }
}
