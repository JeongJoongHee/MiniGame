using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JumpJump.EditorTools
{
    /// <summary>
    /// 플레이 모드에 들어가지 않고 게임 루프를 그대로 돌려 보는 자동 플레이테스트.
    /// GameManager.Step(dt, tap) 을 직접 호출하기 때문에 실제 게임과 완전히 같은 코드를 검증합니다.
    /// 난이도를 조정한 뒤 "실제로 오를 수 있는 숫자인지" 확인할 때 쓰세요.
    /// </summary>
    public static class JumpJumpPlaytest
    {
        const float Dt = 1f / 60f;
        const int Seconds = 120;

        [MenuItem("Tools/JumpJump/Run Headless Playtest", priority = 20)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            const string bestKey = "JumpJump.BestScore";
            int savedBest = PlayerPrefs.GetInt(bestKey, 0);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = JumpJumpSceneBuilder.Populate();
            if (game == null) return;

            var player = Object.FindFirstObjectByType<PlayerController>();
            var platforms = Object.FindFirstObjectByType<PlatformManager>();
            var config = game.Config;

            game.ResetRun();

            LogDifficultyTable(config);

            var bot = new Bot(config, player, platforms);
            int steps = Seconds * 60;
            int runs = 0;
            int bestRow = 0;
            int bestScore = 0;
            var reasons = new StringBuilder();

            for (int i = 0; i < steps; i++)
            {
                bool tap;
                switch (game.State)
                {
                    case GameState.Ready:
                        tap = true;
                        break;
                    case GameState.Playing:
                        tap = bot.WantsJump(Dt);
                        break;
                    default:
                        tap = true; // 게임 오버 -> 재시작 (0.6초 잠금은 GameManager 가 처리)
                        break;
                }

                var before = game.State;
                game.Step(Dt, tap);

                if (before == GameState.Playing)
                {
                    if (game.TopRow > bestRow) bestRow = game.TopRow;
                    if (game.Score > bestScore) bestScore = game.Score;
                }

                if (before == GameState.Playing && game.State == GameState.GameOver)
                {
                    runs++;
                    reasons.Append("  #").Append(runs)
                           .Append(" reason=").Append(game.LastResultReason)
                           .Append(" row=").Append(game.TopRow)
                           .Append(" score=").Append(game.Score)
                           .Append(" height=").Append(Mathf.FloorToInt(game.HeightMeters)).Append("m\n");
                    bot.Reset();
                }
            }

            PlayerPrefs.SetInt(bestKey, savedBest);
            PlayerPrefs.Save();

            Debug.Log($"[JumpJump 플레이테스트] {Seconds}초 시뮬레이션 결과\n" +
                      $"  완료한 판     : {runs}\n" +
                      $"  최고 도달 칸  : {bestRow}\n" +
                      $"  최고 점수     : {bestScore}\n" +
                      $"  마지막 상태   : {game.State}\n" +
                      reasons);

            if (bestRow < 5)
                Debug.LogError("[JumpJump 플레이테스트] 봇이 5칸도 오르지 못했습니다. 점프 높이(jumpApex)나 발판 간격(rowSpacing)을 확인하세요.");
        }

        /// <summary>구간표가 실제로 어떻게 적용되는지 확인용으로 찍습니다.</summary>
        static void LogDifficultyTable(GameConfig config)
        {
            string source = config.difficultyCsv != null
                ? config.difficultyCsv.name + ".csv"
                : "GameConfig 인스펙터 목록";

            Debug.Log($"[JumpJump 난이도 구간표] 출처: {source} ({config.Bands.Length}개 구간)\n"
                      + DifficultyTable.Describe(config.Bands, config.rowSpacing * config.metersPerUnit));
        }

        /// <summary>
        /// 아주 단순한 봇: 다음 칸 발판의 x 속도를 관측해서 착지 시점의 위치를 예측하고,
        /// 그때 발판이 자기 위에 오도록 점프합니다.
        /// </summary>
        class Bot
        {
            readonly GameConfig _config;
            readonly PlayerController _player;
            readonly PlatformManager _platforms;

            int _trackedRow = -1;
            float _lastX;
            bool _hasLastX;

            public Bot(GameConfig config, PlayerController player, PlatformManager platforms)
            {
                _config = config;
                _player = player;
                _platforms = platforms;
            }

            public void Reset()
            {
                _trackedRow = -1;
                _hasLastX = false;
            }

            public bool WantsJump(float dt)
            {
                if (!_player.IsGrounded) { _hasLastX = false; return false; }

                int nextIndex = _player.CurrentRow + 1;
                var next = _platforms.GetRow(nextIndex);
                if (next == null) return false;

                if (_trackedRow != nextIndex) { _trackedRow = nextIndex; _hasLastX = false; }

                float x = next.X;
                if (!_hasLastX) { _lastX = x; _hasLastX = true; return false; }

                float vx = (x - _lastX) / dt;
                _lastX = x;

                float landTime = DescentTimeToNextRow();
                if (landTime <= 0f) return false;

                float limit = Mathf.Max(0f, _config.playHalfWidth - next.HalfWidth);
                float predicted = PredictWithBounce(x, vx, landTime, limit);

                return Mathf.Abs(predicted - _player.transform.position.x) <= next.HalfWidth * 0.6f;
            }

            /// <summary>점프한 뒤 발이 한 칸 위 발판 높이까지 내려오는 데 걸리는 시간.</summary>
            float DescentTimeToNextRow()
            {
                float v0 = _config.JumpVelocity;
                float g = _config.gravity;
                float h = _config.rowSpacing;
                float disc = v0 * v0 - 2f * g * h;
                if (disc < 0f) return -1f;           // 애초에 한 칸 위까지 못 올라감
                return (v0 + Mathf.Sqrt(disc)) / g;  // 하강하면서 통과하는 시점
            }

            /// <summary>좌우 벽에 반사되는 왕복 운동을 고려한 t초 뒤 위치.</summary>
            static float PredictWithBounce(float x, float vx, float t, float limit)
            {
                if (limit <= 0f) return 0f;

                float span = 2f * limit;
                float travelled = x + vx * t + limit;   // 왼쪽 벽을 0으로 맞춘 좌표
                return Mathf.PingPong(travelled, span) - limit;
            }
        }
    }
}
