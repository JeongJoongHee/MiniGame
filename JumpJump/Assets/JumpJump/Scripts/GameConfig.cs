using UnityEngine;

namespace JumpJump
{
    /// <summary>
    /// 높이(m) 한 구간의 난이도. 수정사항_01.xlsx 의 표 한 줄에 해당합니다.
    /// </summary>
    [System.Serializable]
    public class DifficultyBand
    {
        [Tooltip("이 높이(m)까지 이 구간이 적용됩니다. 목록의 마지막 항목이 그 위쪽 전부를 담당합니다.")]
        public float upToMeters = 100f;
        [Tooltip("발판 최소 블록 수")]
        public int minBlocks = 4;
        [Tooltip("발판 최대 블록 수. 칸마다 최소~최대 사이에서 무작위로 뽑습니다.")]
        public int maxBlocks = 6;
        [Tooltip("이 구간의 타이머(초). 새 칸을 밟으면 이 값으로 초기화됩니다.")]
        public float timer = 5f;
        [Tooltip("발판 좌우 이동 속도")]
        public float moveSpeed = 1f;
    }

    /// <summary>
    /// 높이(m) 한 구간의 배경. 낮은 순서대로 늘어놓습니다.
    /// 구간 경계에서는 다음 배경이 서서히 겹쳐 나타납니다.
    /// </summary>
    [System.Serializable]
    public class BackdropBand
    {
        [Tooltip("이 높이(m)부터 이 배경이 나옵니다. 목록의 첫 항목은 보통 0 입니다.")]
        public float fromMeters = 0f;
        [Tooltip("세로로 무한 반복되는 배경 그림. 위끝과 아래끝이 이어지는 그림이어야 합니다.")]
        public Sprite sprite;
    }

    /// <summary>
    /// 게임의 모든 튜닝 값. Assets/JumpJump/GameConfig.asset 을 인스펙터에서 수정하면
    /// 코드를 건드리지 않고 손맛을 조절할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "JumpJump/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("레이아웃 (월드 단위, 1유닛 = 1m)")]
        [Tooltip("발판 한 칸(row) 사이의 세로 간격")]
        public float rowSpacing = 2.2f;
        [Tooltip("발판이 움직일 수 있는 좌우 한계 (중앙 기준 절반 폭)")]
        public float playHalfWidth = 4.0f;
        [Tooltip("발판 블록 한 칸의 가로 폭. Base.png 한 장이 이 폭이 되도록 PPU 가 자동 계산됩니다.")]
        public float blockWidth = 1.0f;
        [Tooltip("Base.png 의 가로세로 비율에서 자동 계산됩니다. (Tools > JumpJump > Setup Art Assets)")]
        public float blockHeight = 0.68f;

        [Header("플레이어")]
        [Tooltip("캐릭터 크기. y(키)에 맞춰 5종의 PPU 가 자동 계산되므로 덩치가 통일됩니다.")]
        public Vector2 playerSize = new Vector2(0.72f, 0.95f);
        public float gravity = 55f;
        [Tooltip("점프 최고점 높이. rowSpacing 보다 커야 한 칸 위로 올라갈 수 있습니다.")]
        public float jumpApex = 3.0f;
        [Tooltip("착지 직전에 누른 터치를 기억해 주는 시간")]
        public float jumpBuffer = 0.12f;
        [Tooltip("발판 끝을 살짝 벗어나도 착지시켜 주는 여유 폭")]
        public float landPad = 0.12f;

        [Header("난이도 구간표")]
        [Tooltip("0번 칸(바닥)의 블록 수. 구간표와 무관하게 항상 이 값입니다.")]
        public int groundBlocks = 7;
        [Tooltip("난이도 구간표 CSV. 지정하면 게임이 실행될 때 이 파일을 읽습니다. " +
                 "엑셀로 편집하고 저장하면 다음 Play 부터 바로 반영됩니다.")]
        public TextAsset difficultyCsv;
        [Tooltip("CSV 를 읽은 결과가 여기에 구워집니다(눈으로 확인용). " +
                 "CSV 가 없거나 읽기에 실패하면 이 목록이 대신 쓰입니다.")]
        public DifficultyBand[] difficultyBands = new DifficultyBand[0];

        [Header("타이머")]
        [Tooltip("초당 감소량")]
        public float drainPerSecond = 1f;

        [Header("점수")]
        public int scorePerRow = 100;
        [Tooltip("콤보 1당 배수 증가량 (0.1 = 콤보10에서 2배)")]
        public float comboScoreStep = 0.1f;
        public int scoreComboCap = 20;
        public float metersPerUnit = 1f;

        [Header("카메라")]
        public float orthoSize = 8f;
        [Tooltip("플레이어가 화면 중앙보다 얼마나 아래에 보일지")]
        public float cameraPlayerOffset = 4f;
        public float cameraSmooth = 8f;
        [Tooltip("화면 아래쪽 이 거리만큼 더 내려가면 게임 오버")]
        public float deathMargin = 1f;

        [Header("배경")]
        [Tooltip("배경이 월드 대비 얼마나 빠르게 흐를지. 0 = 카메라에 고정, 1 = 발판과 같은 속도")]
        [Range(0f, 1f)] public float backdropParallax = 0.5f;
        [Tooltip("BG 한 장의 가로 폭(월드 단위). 세로 높이는 이미지 비율로 자동 결정됩니다.")]
        public float backdropTileWidth = 10.4f;
        [Tooltip("높이별 배경. 낮은 순서대로 늘어놓습니다. " +
                 "그림은 Art/Background 폴더의 파일 이름 순서로 Build Game Scene 이 채워 줍니다.")]
        public BackdropBand[] backdropBands = new BackdropBand[0];
        [Tooltip("구간 경계 앞쪽 몇 m 에 걸쳐 다음 배경으로 서서히 넘어갈지. " +
                 "예: 30 이면 170m 부터 겹치기 시작해 200m 에서 완전히 바뀝니다.")]
        public float backdropBlendMeters = 30f;

        [Header("캐릭터 애니메이션 (1프레임 스프라이트를 코드로 움직입니다)")]
        [Tooltip("속도에 따른 늘어남/찌그러짐 강도")]
        public float squashStrength = 0.012f;
        public float squashMax = 0.35f;
        [Tooltip("발판 위에서 대기할 때 위아래로 살짝 움직이는 폭")]
        public float idleBobAmount = 0.035f;
        public float idleBobSpeed = 4.5f;
        [Tooltip("착지 순간의 찌그러짐")]
        public float landSquash = 0.32f;
        [Tooltip("점프 순간의 늘어남")]
        public float jumpStretch = 0.26f;
        [Tooltip("스쿼시가 원래대로 돌아오는 속도")]
        public float squashRecovery = 11f;

        [Header("스폰")]
        [Tooltip("화면 위쪽으로 미리 만들어 둘 칸 수")]
        public int rowsAhead = 8;

        DifficultyBand[] _bands;

        /// <summary>
        /// 실제로 쓰이는 구간표. difficultyCsv 가 있으면 그 내용이고,
        /// 없거나 읽기에 실패하면 인스펙터의 difficultyBands 입니다.
        /// 결과는 한 번만 파싱해 두고 재사용합니다.
        /// </summary>
        public DifficultyBand[] Bands
        {
            get
            {
                if (_bands != null) return _bands;

                if (difficultyCsv != null)
                {
                    var parsed = DifficultyTable.Parse(difficultyCsv.text, out string report);

                    if (parsed != null)
                    {
                        if (!string.IsNullOrEmpty(report))
                            Debug.LogWarning($"[JumpJump] {difficultyCsv.name} 을(를) 읽었지만 손본 부분이 있습니다.\n{report}");

                        _bands = parsed;
                        return _bands;
                    }

                    Debug.LogError($"[JumpJump] {difficultyCsv.name} 을(를) 읽지 못해 GameConfig 의 목록을 대신 씁니다.\n  {report}");
                }

                _bands = difficultyBands;
                return _bands;
            }
        }

        /// <summary>CSV 를 다시 읽게 합니다. (에디터에서 파일을 고친 뒤)</summary>
        public void InvalidateBands() => _bands = null;

        void OnEnable() => _bands = null;
        void OnValidate() => _bands = null;

        /// <summary>jumpApex 와 gravity 로부터 계산된 점프 초기 속도.</summary>
        public float JumpVelocity => Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, jumpApex));

        /// <summary>N번째 칸의 높이(m). HUD 의 Height 표시와 같은 값입니다.</summary>
        public float MetersForRow(int row) => row * rowSpacing * metersPerUnit;

        /// <summary>주어진 높이(m)가 속한 난이도 구간. 표를 위에서부터 훑고, 넘어서면 마지막 구간입니다.</summary>
        public DifficultyBand BandForMeters(float meters)
        {
            var bands = Bands;
            if (bands == null || bands.Length == 0) return null;

            for (int i = 0; i < bands.Length; i++)
                if (meters <= bands[i].upToMeters) return bands[i];

            return bands[bands.Length - 1];
        }

        public DifficultyBand BandForRow(int row) => BandForMeters(MetersForRow(row));

        /// <summary>구간의 최소~최대 사이에서 이 칸의 발판 블록 수를 뽑습니다.</summary>
        public int BlocksForRow(int row)
        {
            if (row <= 0) return groundBlocks;

            var band = BandForRow(row);
            if (band == null) return groundBlocks;

            int lo = Mathf.Max(1, Mathf.Min(band.minBlocks, band.maxBlocks));
            int hi = Mathf.Max(lo, band.maxBlocks);
            return Random.Range(lo, hi + 1);
        }

        public float SpeedForRow(int row)
        {
            if (row <= 0) return 0f;
            var band = BandForRow(row);
            return band != null ? band.moveSpeed : 0f;
        }

        /// <summary>이 칸을 밟았을 때 타이머가 초기화될 값(초).</summary>
        public float TimerForRow(int row)
        {
            var band = BandForRow(row);
            return band != null ? Mathf.Max(0.1f, band.timer) : 5f;
        }

        public float ScoreMultiplier(int combo)
        {
            return 1f + Mathf.Min(combo, scoreComboCap) * comboScoreStep;
        }

        /// <summary>주어진 높이(m)에서 바탕이 되는 배경 구간의 번호. 배경이 없으면 -1.</summary>
        public int BackdropIndexForMeters(float meters)
        {
            if (backdropBands == null || backdropBands.Length == 0) return -1;

            int index = 0;
            for (int i = 0; i < backdropBands.Length; i++)
                if (meters >= backdropBands[i].fromMeters) index = i;

            return index;
        }

        /// <summary>
        /// 다음 배경이 얼마나 겹쳐 보일지 (0 = 아직 안 보임, 1 = 완전히 넘어감).
        /// 경계보다 backdropBlendMeters 만큼 앞에서 시작해 경계에서 1이 됩니다.
        /// </summary>
        public float BackdropBlend(float meters, int index)
        {
            if (backdropBands == null || index < 0 || index + 1 >= backdropBands.Length) return 0f;

            float boundary = backdropBands[index + 1].fromMeters;
            float span = Mathf.Max(0.01f, backdropBlendMeters);
            return Mathf.Clamp01((meters - (boundary - span)) / span);
        }
    }
}
