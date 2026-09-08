using UnityEngine;

namespace JumpJump
{
    public enum GameState { Ready, Playing, GameOver }

    /// <summary>
    /// 상태 관리 + 점수 / 높이 / 타이머 규칙. 매 프레임 하위 시스템을 정해진 순서로 굴려서
    /// 발판 이동 -> 착지 판정 -> 카메라 -> HUD 순서가 항상 보장되게 합니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] GameConfig config;
        [SerializeField] PlayerController player;
        [SerializeField] PlatformManager platforms;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] BackdropTiler backdrop;
        [SerializeField] HudController hud;

        const string BestScoreKey = "JumpJump.BestScore";
        const float RetryLockSeconds = 0.6f;

        float _stateClock;   // 현재 상태에 머문 시간 (에디터 테스트에서도 동작하도록 Time.time 대신 누적)
        float _startY;
        bool _booted;

        public GameConfig Config => config;
        public GameState State { get; private set; } = GameState.Ready;
        public int Score { get; private set; }
        public int BestScore { get; private set; }
        public int Combo { get; private set; }
        public int TopRow { get; private set; }
        public float TimeLeft { get; private set; }
        /// <summary>현재 구간의 타이머 최대값. 타이머 바의 비율 계산에 씁니다.</summary>
        public float TimeMax { get; private set; }
        public float HeightMeters { get; private set; }
        /// <summary>지금 이 순간의 높이(m). HeightMeters 는 최고 기록이라 내려오면 줄지 않습니다.</summary>
        float CurrentMeters => player != null ? (player.transform.position.y - _startY) * config.metersPerUnit : 0f;
        public string LastResultReason { get; private set; } = "";

        void Awake()
        {
            Application.targetFrameRate = 60;
            Boot();
        }

        void Start()
        {
            ResetRun();
        }

        /// <summary>Awake 대신 직접 호출할 수 있는 초기화. 에디터 플레이테스트에서 사용합니다.</summary>
        public void Boot()
        {
            if (_booted) return;
            _booted = true;
            Instance = this;
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        }

        void Update()
        {
            // "로비로 나가시겠습니까?" 팝업이 떠 있는 동안에는 게임을 멈춥니다.
            // 고민하는 사이에 죽으면 안 되니까요. 여기서 막기 때문에 아래 Step 은
            // 팝업을 전혀 몰라도 되고, 배치 모드 플레이테스트도 그대로 돕니다.
            if (Arcade.PopupPanel.Blocking) return;

            // 오버레이의 "로비로" 버튼을 누른 것까지 점프로 세지 않도록 UI 위의 터치는 걸러냅니다.
            bool tap = TapInput.Pressed && !TapInput.OverUI;
            if (tap) TapInput.Consume();
            Step(Time.deltaTime, tap);
        }

        /// <summary>
        /// 한 프레임 진행. 입력과 델타타임을 인자로 받기 때문에 자동 플레이테스트로 재생할 수 있습니다.
        /// </summary>
        public void Step(float dt, bool tap)
        {
            _stateClock += dt;

            switch (State)
            {
                case GameState.Ready:
                    platforms.Tick(dt, cameraRig.transform.position.y, cameraRig.HalfHeight);
                    player.Tick(dt, false, false);
                    if (backdrop != null) backdrop.Tick(CurrentMeters);
                    if (tap) StartRun();
                    break;

                case GameState.Playing:
                    TimeLeft = Mathf.Max(0f, TimeLeft - config.drainPerSecond * dt);

                    platforms.Tick(dt, cameraRig.transform.position.y, cameraRig.HalfHeight);
                    player.Tick(dt, tap, true);

                    // player.Tick 안에서 낙사로 EndRun 이 호출됐을 수 있습니다.
                    if (State != GameState.Playing) break;

                    cameraRig.Tick(dt);

                    float meters = CurrentMeters;
                    if (meters > HeightMeters) HeightMeters = meters;
                    if (backdrop != null) backdrop.Tick(meters);

                    if (TimeLeft <= 0f) EndRun(Arcade.StringTable.Get("jump.over.timeup", "TIME OVER"));
                    break;

                case GameState.GameOver:
                    if (_stateClock >= RetryLockSeconds && tap)
                    {
                        ResetRun();
                        StartRun();
                    }
                    break;
            }

            hud.Refresh(this);
        }

        public void ResetRun()
        {
            Boot();

            Score = 0;
            Combo = 0;
            TopRow = 0;
            TimeMax = config.TimerForRow(0);
            TimeLeft = TimeMax;
            HeightMeters = 0f;
            LastResultReason = "";

            platforms.ResetRun();
            player.ResetRun();
            cameraRig.ResetRun();

            _startY = player.transform.position.y;
            if (backdrop != null) backdrop.Tick(0f);
            SetState(GameState.Ready);
        }

        void StartRun()
        {
            TimeMax = config.TimerForRow(TopRow);
            TimeLeft = TimeMax;
            SetState(GameState.Playing);
        }

        public void EndRun(string reason)
        {
            if (State == GameState.GameOver) return;

            LastResultReason = reason;

            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt(BestScoreKey, BestScore);
                PlayerPrefs.Save();
            }

            SetState(GameState.GameOver);
        }

        void SetState(GameState next)
        {
            State = next;
            _stateClock = 0f;
        }

        /// <summary>PlayerController 가 착지할 때 호출합니다.</summary>
        public void OnLanded(int rowIndex)
        {
            if (State != GameState.Playing) return;

            if (rowIndex > TopRow)
            {
                int gained = rowIndex - TopRow;
                Combo += gained;

                float multiplier = config.ScoreMultiplier(Combo);
                Score += Mathf.RoundToInt(config.scorePerRow * gained * multiplier);

                // 새 발판을 밟으면 타이머를 그 구간의 값으로 완전히 초기화합니다. (수정사항_01)
                TimeMax = config.TimerForRow(rowIndex);
                TimeLeft = TimeMax;

                TopRow = rowIndex;
            }
            else
            {
                // 같은 칸이나 아래 칸에 착지 = 전진 실패, 콤보 초기화
                Combo = 0;
            }
        }
    }
}
