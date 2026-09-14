using UnityEngine;

namespace Archery
{
    public enum GameState { Ready, Playing, GameOver }

    /// <summary>
    /// 활쏘기의 규칙 전부. 상태 / 점수 / 화살 수 / 과녁 크기를 여기서 관리하고,
    /// 매 프레임 하위 시스템(과녁 -> 화살 -> 활 -> HUD)을 정해진 순서로 굴립니다.
    ///
    /// 규칙 (기획서 @활쏘기 기획서.png):
    ///   - 활은 화면 아래 가운데에 고정. 터치하면 화살이 수직으로 날아간다.
    ///   - 과녁은 좌우로 왕복한다. 가운데 5점, 가장자리 1점.
    ///   - 화살은 5발. 쏠 때마다 1 줄고, **맞히면 최대치로 다시 찬다.**
    ///   - 맞힐 때마다 과녁이 1% 씩 작아진다.
    ///   - 화살이 다 떨어지면 게임 오버. (= 연속으로 최대치만큼 빗나가면 끝)
    ///
    /// 난이도 수치는 전부 archery.csv 의 구간표에서 옵니다. 기준은 **누적 명중 수**입니다.
    ///
    /// Step(dt, tap) 이 입력과 델타타임을 인자로 받기 때문에, 플레이 모드에 들어가지 않고
    /// 배치 모드에서 이 게임을 그대로 재생할 수 있습니다. (ArcheryPlaytest / ArcheryPreview)
    /// </summary>
    public class ArcheryGame : MonoBehaviour
    {
        public static ArcheryGame Instance { get; private set; }

        [SerializeField] ArcheryConfig config;
        [SerializeField] TargetController target;
        [SerializeField] ArrowPool arrows;
        [SerializeField] BowController bow;
        [SerializeField] ArcheryHud hud;

        const string BestScoreKey = "Archery.BestScore";

        /// <summary>랭킹표를 가르는 이름. `GameCatalog.asset` 의 id 와 같아야 합니다.</summary>
        public const string GameId = "archery";
        const float RetryLockSeconds = 0.6f;

        float _stateClock;    // 현재 상태에 머문 시간 (에디터 테스트에서도 동작하도록 Time.time 대신 누적)
        float _fireClock;     // 마지막 발사로부터 지난 시간
        bool _booted;

        public ArcheryConfig Config => config;
        public GameState State { get; private set; } = GameState.Ready;
        public int Score { get; private set; }
        public int BestScore { get; private set; }
        /// <summary>누적 명중 수. 난이도 구간을 가르는 기준입니다.</summary>
        public int Hits { get; private set; }
        public int Shots { get; private set; }
        public int Ammo { get; private set; }
        public int AmmoMax { get; private set; }
        public float SizePercent => target != null ? target.SizePercent : 100f;
        public string LastResultReason { get; private set; } = "";

        /// <summary>방금 맞힌 점수와 그 자리. HUD 가 "+5 PT" 를 띄우는 데 씁니다.</summary>
        public int LastHitPoints { get; private set; }
        public float LastHitX { get; private set; }
        public float HitPopupTimer { get; private set; }

        /// <summary>명중률(%). 결과 화면에 보여 줍니다.</summary>
        public float Accuracy => Shots > 0 ? Hits * 100f / Shots : 0f;

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

            // 오버레이의 "LOBBY" 버튼을 누른 것까지 발사로 세지 않도록 UI 위의 터치는 걸러냅니다.
            bool tap = TapInput.Pressed && !TapInput.OverUI;
            if (tap) TapInput.Consume();

            // 게임 오버에서 "다시하기" 를 누른 순간이 광고 자리입니다 (2판마다 · Arcade.AdBreak 설명 참고).
            // 광고가 뜨면 곧바로 판을 시작하지 않고, 닫힌 뒤 "터치하면 시작" 화면으로 돌려놓습니다.
            // Step 바깥이라 배치 플레이테스트에는 광고가 끼어들지 않습니다.
            if (tap && State == GameState.GameOver && _stateClock >= RetryLockSeconds &&
                Arcade.AdBreak.TryShow(ResetRun)) return;

            Step(Time.deltaTime, tap);
        }

        /// <summary>
        /// 한 프레임 진행. 입력과 델타타임을 인자로 받기 때문에 자동 플레이테스트로 재생할 수 있습니다.
        /// </summary>
        public void Step(float dt, bool tap)
        {
            _stateClock += dt;
            _fireClock += dt;
            if (HitPopupTimer > 0f) HitPopupTimer = Mathf.Max(0f, HitPopupTimer - dt);

            switch (State)
            {
                case GameState.Ready:
                    target.Tick(dt, config.TargetSpeed(0));
                    bow.Tick(dt, true);
                    if (tap) StartRun();
                    break;

                case GameState.Playing:
                    if (tap) TryFire();

                    target.Tick(dt, config.TargetSpeed(Hits));
                    arrows.Tick(dt, config.ArrowSpeed(Hits), target.X, target.Radius, OnArrowResolved);
                    bow.Tick(dt, Ammo > 0);

                    // 화살이 다 떨어지고, 날아가는 중인 화살도 없으면 판이 끝납니다.
                    // (마지막 한 발이 아직 과녁에 닿지 않았다면 결과를 기다립니다)
                    if (Ammo <= 0 && arrows.PendingCount <= 0)
                        EndRun(Arcade.StringTable.Get("archery.over.noammo", "OUT OF ARROWS"));
                    break;

                case GameState.GameOver:
                    target.Tick(dt, config.TargetSpeed(Hits));
                    bow.Tick(dt, false);
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
            Hits = 0;
            Shots = 0;
            AmmoMax = config.AmmoMax(0);
            Ammo = AmmoMax;
            LastResultReason = "";
            LastHitPoints = 0;
            HitPopupTimer = 0f;
            _fireClock = 999f;

            target.ResetRun();
            arrows.ResetRun();
            bow.ResetRun();

            SetState(GameState.Ready);
        }

        void StartRun()
        {
            SetState(GameState.Playing);
        }

        void TryFire()
        {
            if (Ammo <= 0) return;
            if (_fireClock < config.fireCooldown) return;
            if (!arrows.Fire(bow.MuzzleX)) return;   // 풀이 꽉 찼으면 화살을 소모하지 않습니다

            Ammo--;
            Shots++;
            _fireClock = 0f;
            bow.Fire();
            Arcade.Sfx.Play(Arcade.Sfx.ArcheryShoot);
        }

        /// <summary>화살 한 발이 과녁 높이를 지났을 때 ArrowPool 이 부릅니다.</summary>
        void OnArrowResolved(ArrowResult result)
        {
            if (result.points <= 0) return;   // 빗나감 — 화살은 이미 줄어 있습니다

            Arcade.Sfx.Play(Arcade.Sfx.ArcheryHit);
            if (result.points >= config.rings) Arcade.Sfx.Play(Arcade.Sfx.ArcheryBullseye);   // 한가운데

            Score += result.points;
            Hits++;

            // 맞히면 과녁이 작아지고, 화살이 그 구간의 최대치로 다시 찹니다.
            target.OnHit(Hits);
            AmmoMax = config.AmmoMax(Hits);
            Ammo = AmmoMax;

            LastHitPoints = result.points;
            LastHitX = result.x;
            HitPopupTimer = config.hitPopupSeconds;
        }

        public void EndRun(string reason)
        {
            if (State == GameState.GameOver) return;

            LastResultReason = reason;

            // 최고 점수를 넘었으면 GameOver 대신 NewRecord. 처음 한 판(최고 0)은 제외.
            Arcade.Sfx.Play(BestScore > 0 && Score > BestScore ? Arcade.Sfx.NewRecord : Arcade.Sfx.GameOver);

            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt(BestScoreKey, BestScore);
                PlayerPrefs.Save();
            }

            // 점프점프와 똑같은 한 줄입니다. 여기서는 "끝났다"만 알리고,
            // 랭킹 등록·광고는 이 신호를 듣는 쪽에서 합니다 (Arcade.GameSession 설명 참고).
            Arcade.GameSession.ReportRunFinished(
                new Arcade.RunResult(GameId, Score, reason, "명중 " + Hits));

            SetState(GameState.GameOver);
        }

        void SetState(GameState next)
        {
            State = next;
            _stateClock = 0f;
        }
    }
}
