using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Archery.EditorTools
{
    /// <summary>
    /// 플레이 모드에 들어가지 않고 게임 루프를 그대로 돌려 보는 자동 플레이테스트.
    /// ArcheryGame.Step(dt, tap) 을 직접 호출하기 때문에 실제 게임과 완전히 같은 코드를 검증합니다.
    ///
    /// 두 가지 봇을 돌립니다.
    ///   1) 조준 봇 : 착탄 시점의 과녁 위치를 예측해서 쏩니다.
    ///      -> "잘 쏘면 계속 이어지는가 / 과녁이 얼마나 작아지는가 / 난이도 구간이 올라가는가"
    ///   2) 일부러 빗맞히는 봇 : 과녁이 멀리 있을 때만 쏩니다.
    ///      -> "빗나가면 화살이 떨어져 판이 제대로 끝나는가"
    /// </summary>
    public static class ArcheryPlaytest
    {
        const float Dt = 1f / 60f;
        const int AimSeconds = 120;
        const int MissSeconds = 30;

        [MenuItem("Tools/Archery/Run Headless Playtest", priority = 20)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            const string bestKey = "Archery.BestScore";
            int savedBest = PlayerPrefs.GetInt(bestKey, 0);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = ArcherySceneBuilder.Populate();
            if (game == null) return;

            var target = Object.FindFirstObjectByType<TargetController>();
            var config = game.Config;

            LogStageTable(config);

            var aim = Simulate(game, target, config, AimSeconds, aiming: true);
            Debug.Log($"[Archery 플레이테스트] 조준 봇 {AimSeconds}초\n" + aim);

            var spray = Simulate(game, target, config, MissSeconds, aiming: false);
            Debug.Log($"[Archery 플레이테스트] 일부러 빗맞히는 봇 {MissSeconds}초\n" + spray);

            PlayerPrefs.SetInt(bestKey, savedBest);
            PlayerPrefs.Save();

            if (aim.hits < 20)
                Debug.LogError("[Archery 플레이테스트] 조준 봇이 20발도 못 맞혔습니다. " +
                               "화살 속도(Arrow_Speed)나 과녁 속도(Target_Speed)를 확인하세요.");
            if (spray.runs == 0)
                Debug.LogError("[Archery 플레이테스트] 빗맞히는 봇이 한 판도 끝내지 못했습니다. " +
                               "화살이 떨어져도 게임 오버가 되지 않는다는 뜻입니다.");
        }

        struct Result
        {
            public int runs, hits, shots, bestScore, bestHits;
            public float smallestSize;
            public string log;
            public override string ToString() => log;
        }

        static Result Simulate(ArcheryGame game, TargetController target, ArcheryConfig config,
                               int seconds, bool aiming)
        {
            game.ResetRun();

            var result = new Result { smallestSize = 100f };
            var reasons = new StringBuilder();
            int steps = seconds * 60;

            for (int i = 0; i < steps; i++)
            {
                bool tap;
                if (game.State != GameState.Playing)
                {
                    tap = true;   // 시작 / 재시작 (게임 오버 뒤 0.6초 잠금은 ArcheryGame 이 처리)
                }
                else if (aiming)
                {
                    tap = WantsShot(game, target, config);
                }
                else
                {
                    // 일부러 빗맞히는 봇: 과녁이 활에서 멀리 있을 때만 쏩니다.
                    // 운에 기대지 않고 "화살이 떨어지면 판이 끝나는가"를 확실히 확인하기 위한 것입니다.
                    // (착탄 지점이 과녁 반지름 밖이면 반드시 빗나갑니다. 벽 근처에서만 성립하므로
                    //  여유를 5% 만 둡니다 — 더 키우면 아예 쏠 기회가 없습니다)
                    tap = game.Ammo > 0 && Predict(game, target, config) > target.Radius * 1.05f;
                }

                // 한 판이 끝나면 게임이 숫자를 0 으로 돌리므로, 이번 프레임에 늘어난 만큼만 더합니다.
                int shotsBefore = game.Shots, hitsBefore = game.Hits;
                var before = game.State;
                game.Step(Dt, tap);

                if (before == GameState.Playing)
                {
                    result.shots += game.Shots - shotsBefore;
                    result.hits += game.Hits - hitsBefore;
                    if (game.Score > result.bestScore) result.bestScore = game.Score;
                    if (game.Hits > result.bestHits) result.bestHits = game.Hits;
                    if (game.SizePercent < result.smallestSize) result.smallestSize = game.SizePercent;
                }

                if (before == GameState.Playing && game.State == GameState.GameOver)
                {
                    result.runs++;
                    if (result.runs <= 6)
                        reasons.Append("  #").Append(result.runs)
                               .Append(" ").Append(game.LastResultReason)
                               .Append("  점수 ").Append(game.Score)
                               .Append("  명중 ").Append(game.Hits).Append("/").Append(game.Shots)
                               .Append("  과녁 ").Append(game.SizePercent.ToString("0.#")).Append("%\n");
                }
            }

            float accuracy = result.shots > 0 ? result.hits * 100f / result.shots : 0f;
            result.log =
                $"  끝난 판 수    : {result.runs}\n" +
                $"  쏜 화살       : {result.shots}\n" +
                $"  맞힌 화살     : {result.hits}  (명중률 {accuracy:0.#}%)\n" +
                $"  한 판 최고    : {result.bestScore}점 / 연속 명중 {result.bestHits}발\n" +
                $"  가장 작아진 과녁 : {result.smallestSize:0.#}%\n" +
                $"  도달한 구간   : 명중 {result.bestHits}회 -> 과녁속도 {config.TargetSpeed(result.bestHits):0.##}, 화살 {config.AmmoMax(result.bestHits)}발\n" +
                (reasons.Length > 0 ? "  끝난 이유\n" + reasons : "");
            return result;
        }

        /// <summary>조준 봇: 착탄 지점이 과녁 한가운데일 때만 쏩니다.</summary>
        static bool WantsShot(ArcheryGame game, TargetController target, ArcheryConfig config)
        {
            if (game.Ammo <= 0) return false;

            // 가운데(5점)를 노립니다. 링 하나 폭의 절반 안쪽이면 쏩니다.
            return Predict(game, target, config) <= target.Radius / Mathf.Max(1, config.rings) * 0.5f;
        }

        /// <summary>
        /// 지금 쏘면 화살이 과녁 중심에서 얼마나 빗나갈지(월드 거리). 활은 x=0 에 고정입니다.
        /// 과녁은 벽에서 반사되므로 PingPong 으로 왕복까지 계산합니다.
        /// </summary>
        static float Predict(ArcheryGame game, TargetController target, ArcheryConfig config)
        {
            float speed = config.ArrowSpeed(game.Hits);
            float travel = config.targetY - (config.arrowStartY + config.arrowLength);
            if (travel <= 0f || speed <= 0f) return float.MaxValue;

            float t = travel / speed;
            float limit = Mathf.Max(0.01f, config.playHalfWidth - target.Radius);
            float v = config.TargetSpeed(game.Hits) * target.Direction;

            float predicted = Mathf.PingPong(target.X + v * t + limit, 2f * limit) - limit;
            return Mathf.Abs(predicted);
        }

        static void LogStageTable(ArcheryConfig config)
        {
            string source = config.stageCsv != null ? config.stageCsv.name + ".csv" : "ArcheryConfig 인스펙터 목록";
            Debug.Log($"[Archery 난이도 구간표] 출처: {source} ({config.Bands.Length}개 구간)\n"
                      + StageTable.Describe(config.Bands));
        }
    }
}
