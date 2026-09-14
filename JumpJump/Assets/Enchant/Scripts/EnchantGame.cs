using UnityEngine;

namespace Enchant
{
    public enum EnchantState
    {
        /// <summary>버튼을 기다리는 중.</summary>
        Idle,
        /// <summary>강화 버튼을 누르고 결과를 기다리는 중 (검이 떨립니다). 이때 누른 버튼은 무시합니다.</summary>
        Working,
        /// <summary>검이 부서졌습니다. [새 검 받기] 를 기다립니다.</summary>
        Broken,
    }

    /// <summary>한 프레임에 들어오는 입력. 점프점프 · 활쏘기의 tap 자리에 해당합니다.</summary>
    public enum EnchantAction { None, Normal, Safe, Melt, NewSword }

    /// <summary>방금 일어난 일. 화면이 "성공!" 같은 글자를 띄우는 데 씁니다.</summary>
    public enum EnchantOutcome { None, Success, SafeFail, Destroyed, Melted, MeltEmpty }

    /// <summary>
    /// 검 강화의 규칙 전부. (기획서 @기획서_게임_03.pdf / 표 @Enchant_Sword.csv)
    ///
    ///   - [일반 강화] 표의 확률로 성공하면 +1. **실패하면 검이 파괴되어 +0 새 검으로.**
    ///   - [안전 강화] 안전 강화 주문서 1장을 쓰고 같은 확률로 시도. **실패해도 검은 그대로.**
    ///   - [분해]      표의 Melt 가 1 이상인 수치(+10 부터)에서만. 검을 +0 으로 돌리고 주문서를 받습니다.
    ///   - 강화 수치 · 주문서 · 최고 기록은 **폰에 저장됩니다.** 로비에 나갔다 와도, 앱을 껐다 켜도 이어집니다.
    ///
    /// 랭킹 점수는 **그 검이 도달한 강화 수치**입니다. 검 한 자루의 일생(파괴 또는 분해)이 한 판입니다.
    ///
    /// Step(dt, action) 이 입력과 델타타임을 인자로 받기 때문에, 플레이 모드에 들어가지 않고
    /// 배치 모드에서 이 게임을 그대로 재생할 수 있습니다. (EnchantPlaytest / EnchantPreview)
    /// 확률은 System.Random 을 쓰므로 Seed() 로 같은 판을 다시 돌릴 수 있습니다.
    /// </summary>
    public class EnchantGame : MonoBehaviour
    {
        public static EnchantGame Instance { get; private set; }

        /// <summary>랭킹표를 가르는 이름. `GameCatalog.asset` 의 id 와 같아야 합니다.</summary>
        public const string GameId = "enchant";

        const string LevelKey = "Enchant.Level";
        const string ScrollsKey = "Enchant.SafeScrolls";
        const string BestKey = "Enchant.BestLevel";

        [SerializeField] EnchantConfig config;
        [SerializeField] EnchantHud hud;

        [Tooltip("\"분해하시겠습니까?\" 창. 비어 있으면 묻지 않고 바로 분해합니다")]
        [SerializeField] Arcade.PopupPanel meltPopup;

        System.Random _rng = new System.Random();
        EnchantAction _pressed;
        EnchantAction _working;
        float _stateClock;
        bool _booted;

        public EnchantConfig Config => config;
        public EnchantState State { get; private set; } = EnchantState.Idle;

        /// <summary>지금 검의 강화 수치.</summary>
        public int Level { get; private set; }
        /// <summary>안전 강화 주문서 수.</summary>
        public int SafeScrolls { get; private set; }
        /// <summary>지금까지 가장 높이 올린 강화 수치 (모든 검을 통틀어).</summary>
        public int BestLevel { get; private set; }

        public EnchantOutcome LastOutcome { get; private set; }
        /// <summary>마지막 결과가 나온 뒤 지난 시간. 화면 연출이 씁니다.</summary>
        public float OutcomeAge { get; private set; } = 999f;
        /// <summary>마지막 분해로 받은 주문서 수.</summary>
        public int LastScrollsGained { get; private set; }
        /// <summary>부서진 검의 강화 수치.</summary>
        public int BrokenLevel { get; private set; }

        public float StateClock => _stateClock;
        /// <summary>켜진 뒤 흐른 시간. 검 뒤의 빛이 숨 쉬는 연출이 씁니다 (Step 으로만 흐르므로 미리보기에서도 같습니다).</summary>
        public float Clock { get; private set; }
        /// <summary>강화 중이면 0 -> 1. 아니면 0.</summary>
        public float WorkProgress => State == EnchantState.Working ? Mathf.Clamp01(_stateClock / Mathf.Max(0.01f, config.workSeconds)) : 0f;
        /// <summary>지금 누르는 강화가 일반인지 안전인지 (Working 일 때만 뜻이 있습니다).</summary>
        public EnchantAction WorkingKind => _working;
        /// <summary>[새 검 받기] 를 누를 수 있는지.</summary>
        public bool CanTakeNewSword => State == EnchantState.Broken && _stateClock >= config.newSwordLockSeconds;

        // 이번 실행에서 센 숫자. 자동 플레이테스트가 봅니다 (저장하지 않습니다).
        public int Attempts { get; private set; }
        public int Successes { get; private set; }
        public int Destroys { get; private set; }
        public int Melts { get; private set; }

        /// <summary>
        /// 끄면 폰에 아무것도 저장하지 않고 읽지도 않습니다. **자동 플레이테스트 · 미리보기가 끕니다** —
        /// 봇이 돌린 수만 번의 강화가 실제 기록을 덮어쓰면 안 되기 때문입니다.
        /// </summary>
        public bool SaveProgress { get; set; } = true;

        // ------------------------------------------------------------ 켜기

        void Awake()
        {
            Application.targetFrameRate = 60;
            Boot();
        }

        public void Boot()
        {
            if (_booted) return;
            _booted = true;
            Instance = this;

            if (SaveProgress)
            {
                Level = Mathf.Max(0, PlayerPrefs.GetInt(LevelKey, 0));
                SafeScrolls = Mathf.Max(0, PlayerPrefs.GetInt(ScrollsKey, config.startSafeScrolls));
                BestLevel = Mathf.Max(Level, PlayerPrefs.GetInt(BestKey, 0));
            }
            else
            {
                Level = 0;
                SafeScrolls = config.startSafeScrolls;
                BestLevel = 0;
            }

            State = EnchantState.Idle;
            LastOutcome = EnchantOutcome.None;
            OutcomeAge = 999f;
        }

        /// <summary>같은 판을 다시 돌리려고 확률의 씨앗을 고정합니다 (플레이테스트용).</summary>
        public void Seed(int seed) => _rng = new System.Random(seed);

        /// <summary>강화 수치 · 주문서 · 최고 기록을 직접 넣습니다 (플레이테스트 · 미리보기용).</summary>
        public void LoadProgress(int level, int scrolls, int best)
        {
            Boot();
            Level = Mathf.Max(0, level);
            SafeScrolls = Mathf.Max(0, scrolls);
            BestLevel = Mathf.Max(Level, best);
            State = EnchantState.Idle;
            LastOutcome = EnchantOutcome.None;
            OutcomeAge = 999f;
            _stateClock = 0f;
            _pressed = EnchantAction.None;
            Save();
        }

        // ------------------------------------------------------------ 버튼 (씬에 미리 연결됨)

        public void PressNormal() => _pressed = EnchantAction.Normal;
        public void PressSafe() => _pressed = EnchantAction.Safe;
        public void PressNewSword() => _pressed = EnchantAction.NewSword;

        /// <summary>
        /// [분해]. 설정이 켜져 있으면 "분해하시겠습니까?" 를 먼저 묻습니다 —
        /// 높이 올린 검이 한 번에 사라지는 버튼이라서요. 묻는 창은 Step 바깥이라 플레이테스트와 무관합니다.
        /// </summary>
        public void PressMelt()
        {
            if (State != EnchantState.Idle || config.MeltScrolls(Level) <= 0) return;

            if (config.confirmMelt && meltPopup != null)
            {
                hud.ShowMeltQuestion(this);
                meltPopup.Open();
                return;
            }

            _pressed = EnchantAction.Melt;
        }

        /// <summary>"분해하시겠습니까?" 의 [확인].</summary>
        public void OnMeltConfirmed()
        {
            if (meltPopup != null) meltPopup.Close();
            _pressed = EnchantAction.Melt;
        }

        /// <summary>"분해하시겠습니까?" 의 [취소].</summary>
        public void OnMeltCancelled()
        {
            if (meltPopup != null) meltPopup.Close();
        }

        void Update()
        {
            // 팝업(로비로 나가기 · 분해 확인 · 별명)이나 광고가 떠 있는 동안은 멈춥니다.
            // 누른 버튼은 버리지 않고 남겨 둡니다 — [확인] 을 누른 그 프레임도 막히기 때문입니다.
            if (Arcade.PopupPanel.Blocking) return;

            var action = _pressed;
            _pressed = EnchantAction.None;

            // 검이 부서진 뒤 [새 검 받기] 를 누른 순간이 광고 자리입니다 (2판마다 · Arcade.AdBreak 설명 참고).
            // 광고가 뜨면 닫힌 뒤에 새 검을 받습니다. Step 바깥이라 배치 플레이테스트에는 끼어들지 않습니다.
            if (action == EnchantAction.NewSword && CanTakeNewSword &&
                Arcade.AdBreak.TryShow(TakeNewSword)) return;

            Step(Time.deltaTime, action);
        }

        // ------------------------------------------------------------ 규칙

        /// <summary>
        /// 한 프레임 진행. 입력과 델타타임을 인자로 받기 때문에 자동 플레이테스트로 재생할 수 있습니다.
        /// </summary>
        public void Step(float dt, EnchantAction action)
        {
            _stateClock += dt;
            OutcomeAge += dt;
            Clock += dt;

            switch (State)
            {
                case EnchantState.Idle:
                    if (action == EnchantAction.Normal) TryBegin(EnchantAction.Normal);
                    else if (action == EnchantAction.Safe) TryBegin(EnchantAction.Safe);
                    else if (action == EnchantAction.Melt) Melt();
                    break;

                case EnchantState.Working:
                    // 강화 중에 누른 버튼은 무시합니다 (연타해도 한 번만).
                    if (_stateClock >= config.workSeconds) Resolve();
                    break;

                case EnchantState.Broken:
                    if (action == EnchantAction.NewSword && CanTakeNewSword) TakeNewSword();
                    break;
            }

            if (hud != null) hud.Refresh(this);
        }

        void TryBegin(EnchantAction kind)
        {
            if (!config.CanEnchant(Level)) return;
            if (kind == EnchantAction.Safe && SafeScrolls <= 0) return;

            _working = kind;
            SetState(EnchantState.Working);

            // 망치질 "깡 깡 깡" — 결과가 나오기까지의 시간에 고르게.
            int hits = Mathf.Max(1, config.hammerHits);
            for (int i = 0; i < hits; i++)
                Arcade.Sfx.Play(Arcade.Sfx.EnchantHammer, delay: config.workSeconds * i / hits);
        }

        /// <summary>강화 결과. 주문서는 여기서(결과가 날 때) 씁니다 — 도중에 앱을 꺼도 아무 일도 없었던 것이 됩니다.</summary>
        void Resolve()
        {
            bool safe = _working == EnchantAction.Safe;
            if (safe) SafeScrolls = Mathf.Max(0, SafeScrolls - 1);

            Attempts++;
            bool success = _rng.Next(10000) < config.SuccessChance(Level);

            if (success)
            {
                Level++;
                Successes++;
                if (Level > BestLevel)
                {
                    BestLevel = Level;
                    // 새 최고 기록은 폰의 랭킹 기록에도 바로 적어 둡니다 (통신 없음).
                    // 서버에는 검이 끝날 때(파괴 · 분해) 또는 다음에 연결될 때 올라갑니다.
                    if (SaveProgress && Arcade.GameSession.Recording)
                        Arcade.LocalRankingService.RecordBest(GameId, BestLevel);
                }

                Show(EnchantOutcome.Success);
                SetState(EnchantState.Idle);
            }
            else if (safe)
            {
                Show(EnchantOutcome.SafeFail);
                SetState(EnchantState.Idle);
            }
            else
            {
                BrokenLevel = Level;
                Destroys++;
                Level = 0;                         // 결과가 난 순간 저장합니다. 앱을 꺼도 되돌릴 수 없습니다.
                Show(EnchantOutcome.Destroyed);
                SetState(EnchantState.Broken);
                FinishSword(BrokenLevel, Arcade.StringTable.Get("enchant.over.broken", "DESTROYED"));
            }

            Save();
        }

        void Melt()
        {
            int reward = config.MeltScrolls(Level);
            if (reward <= 0) return;

            int meltedLevel = Level;
            bool got = _rng.Next(10000) < config.MeltChance(Level);
            LastScrollsGained = got ? reward : 0;
            SafeScrolls += LastScrollsGained;
            Melts++;
            Level = 0;

            Show(got ? EnchantOutcome.Melted : EnchantOutcome.MeltEmpty);
            SetState(EnchantState.Idle);
            FinishSword(meltedLevel, Arcade.StringTable.Get("enchant.over.melt", "MELTED"));
            Save();
        }

        /// <summary>새 +0 검으로 다시 시작합니다. [새 검 받기] (광고가 떴으면 닫힌 뒤).</summary>
        public void TakeNewSword()
        {
            if (State != EnchantState.Broken) return;
            Level = 0;
            LastOutcome = EnchantOutcome.None;
            OutcomeAge = 999f;
            SetState(EnchantState.Idle);
            if (hud != null) hud.Refresh(this);
        }

        /// <summary>
        /// 검 한 자루가 끝났습니다 (파괴 · 분해). 점프점프 · 활쏘기의 EndRun 과 같은 한 줄입니다.
        /// 여기서는 "끝났다"만 알리고, 랭킹 등록 · 광고 차례 세기는 이 신호를 듣는 쪽에서 합니다.
        /// </summary>
        void FinishSword(int level, string reason)
        {
            Arcade.GameSession.ReportRunFinished(new Arcade.RunResult(GameId, level, reason, "+" + level));
        }

        void Show(EnchantOutcome outcome)
        {
            LastOutcome = outcome;
            OutcomeAge = 0f;

            switch (outcome)
            {
                case EnchantOutcome.Success: Arcade.Sfx.Play(Arcade.Sfx.EnchantSuccess); break;
                case EnchantOutcome.SafeFail: Arcade.Sfx.Play(Arcade.Sfx.EnchantSafeFail); break;
                case EnchantOutcome.Destroyed: Arcade.Sfx.Play(Arcade.Sfx.EnchantDestroy); break;
                case EnchantOutcome.Melted:
                case EnchantOutcome.MeltEmpty: Arcade.Sfx.Play(Arcade.Sfx.EnchantMelt); break;
            }
        }

        void SetState(EnchantState next)
        {
            State = next;
            _stateClock = 0f;
        }

        void Save()
        {
            if (!SaveProgress) return;
            PlayerPrefs.SetInt(LevelKey, Level);
            PlayerPrefs.SetInt(ScrollsKey, SafeScrolls);
            PlayerPrefs.SetInt(BestKey, BestLevel);
            PlayerPrefs.Save();
        }
    }
}
