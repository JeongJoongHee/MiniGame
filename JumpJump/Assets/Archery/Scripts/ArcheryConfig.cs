using UnityEngine;

namespace Archery
{
    /// <summary>
    /// 난이도 구간표 한 줄. archery.csv 의 한 줄에 해당합니다.
    /// 구간을 가르는 기준은 **누적 명중 수**입니다. (점프점프는 높이였습니다)
    /// </summary>
    [System.Serializable]
    public class StageBand
    {
        [Tooltip("누적 명중 수가 이 값까지면 이 구간이 적용됩니다. 목록의 마지막 항목이 그 위쪽 전부를 담당합니다.")]
        public int upToHits = 4;
        [Tooltip("과녁이 좌우로 오가는 속도 (월드 단위/초)")]
        public float targetSpeed = 1.6f;
        [Tooltip("명중 1회당 과녁이 작아지는 비율(%). 1 = 맞힐 때마다 1%씩")]
        public float shrinkPercent = 1f;
        [Tooltip("과녁이 이보다 더 작아지지는 않습니다(%)")]
        public float sizeMinPercent = 35f;
        [Tooltip("화살 최대 수. 명중하면 이 값으로 다시 채워집니다")]
        public int ammoMax = 5;
        [Tooltip("화살이 날아가는 속도 (월드 단위/초)")]
        public float arrowSpeed = 16f;
    }

    /// <summary>
    /// 활쏘기의 모든 튜닝 값. Assets/Archery/ArcheryConfig.asset 을 인스펙터에서 수정하면
    /// 코드를 건드리지 않고 손맛을 조절할 수 있습니다.
    ///
    /// **난이도 수치(과녁 속도 / 축소율 / 화살 수)는 여기가 아니라 archery.csv 가 원본입니다.**
    /// stageBands 는 CSV 를 읽은 결과를 눈으로 보라고 구워 둔 거울입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "ArcheryConfig", menuName = "Archery/Archery Config")]
    public class ArcheryConfig : ScriptableObject
    {
        [Header("레이아웃 (월드 단위)")]
        [Tooltip("과녁이 오갈 수 있는 좌우 한계 (중앙 기준 절반 폭). 과녁 **가장자리**가 여기 닿으면 튕기고, " +
                 "과녁 밑의 선도 딱 이 폭으로 그려집니다. 세로로 긴 폰(21:9 = 화면 절반 폭 약 3.43)에서도 " +
                 "화면 안에 있어야 하므로 3.4 보다 키우지 마세요")]
        public float playHalfWidth = 3.4f;
        [Tooltip("과녁의 높이. 화살은 이 높이를 지나는 순간 명중 판정을 받습니다")]
        public float targetY = 4.6f;
        [Tooltip("활 **밑동**이 놓이는 높이. 활은 고정이고 좌우로 움직이지 않습니다. " +
                 "그림의 피벗을 아래끝에 찍기 때문에 이 값이 활의 바닥선입니다")]
        public float bowY = -7.3f;
        [Tooltip("활 그림의 가로 폭. 어떤 그림을 넣어도 이 폭이 되도록 PPU 가 자동 계산됩니다 " +
                 "(Tools > Archery > Setup Art Assets)")]
        public float bowWidth = 3.2f;
        [Tooltip("화살이 출발하는 높이 (화살 밑동 기준)")]
        public float arrowStartY = -6.4f;

        [Header("과녁")]
        [Tooltip("크기 100% 일 때 과녁의 반지름. 명중 판정도 이 값을 씁니다")]
        public float targetRadius = 1.45f;
        [Tooltip("과녁 링 개수. 가운데가 이 점수이고 바깥으로 갈수록 1점씩 줄어듭니다 (5개 = 가운데 5점, 가장자리 1점)")]
        public int rings = 5;
        [Tooltip("과녁 그림이 세로로 납작해 보이는 정도. 보이기만 바꿀 뿐 명중 판정과는 무관합니다")]
        [Range(0.2f, 1f)] public float targetFlatten = 0.42f;

        [Header("화살")]
        [Tooltip("화살 한 발의 길이. 화살 촉이 과녁 높이를 지나면 명중 판정을 합니다. " +
                 "어떤 그림을 넣어도 이 길이가 되도록 PPU 가 자동 계산됩니다")]
        public float arrowLength = 1.6f;
        [Tooltip("화살 한 발을 쏜 뒤 다음 발을 쏠 수 있기까지의 간격(초). 2026-09-14 사용자 요청으로 0.12 -> 0.5")]
        public float fireCooldown = 0.5f;
        [Tooltip("빗나간 화살이 화면 위로 이 만큼 더 올라가면 회수합니다")]
        public float arrowRecycleMargin = 3f;
        [Tooltip("화살이 한 번에 날 수 있는 최대 개수. 화살 수보다 넉넉하게 잡아 둡니다")]
        public int arrowPoolSize = 12;

        [Header("카메라")]
        public float orthoSize = 8f;

        [Header("난이도 구간표")]
        [Tooltip("난이도 구간표 CSV. 지정하면 게임이 실행될 때 이 파일을 읽습니다. " +
                 "엑셀로 편집하고 저장하면 다음 Play 부터 바로 반영됩니다.")]
        public TextAsset stageCsv;
        [Tooltip("CSV 를 읽은 결과가 여기에 구워집니다(눈으로 확인용). " +
                 "CSV 가 없거나 읽기에 실패하면 이 목록이 대신 쓰입니다.")]
        public StageBand[] stageBands = new StageBand[0];

        [Header("연출")]
        [Tooltip("명중했을 때 과녁이 움찔하는 크기")]
        public float targetHitPunch = 0.12f;
        [Tooltip("움찔한 과녁이 원래대로 돌아오는 속도")]
        public float targetHitRecovery = 9f;
        [Tooltip("발사 순간 활이 뒤로 밀리는 거리")]
        public float bowRecoil = 0.18f;
        [Tooltip("밀린 활이 제자리로 돌아오는 속도")]
        public float bowRecovery = 10f;
        [Tooltip("'+5 PT' 표시가 화면에 남아 있는 시간(초)")]
        public float hitPopupSeconds = 0.7f;

        StageBand[] _bands;

        /// <summary>
        /// 실제로 쓰이는 구간표. stageCsv 가 있으면 그 내용이고,
        /// 없거나 읽기에 실패하면 인스펙터의 stageBands 입니다.
        /// 결과는 한 번만 파싱해 두고 재사용합니다.
        /// </summary>
        public StageBand[] Bands
        {
            get
            {
                if (_bands != null) return _bands;

                if (stageCsv != null)
                {
                    var parsed = StageTable.Parse(stageCsv.text, out string report);

                    if (parsed != null)
                    {
                        if (!string.IsNullOrEmpty(report))
                            Debug.LogWarning($"[Archery] {stageCsv.name} 을(를) 읽었지만 손본 부분이 있습니다.\n{report}");

                        _bands = parsed;
                        return _bands;
                    }

                    Debug.LogError($"[Archery] {stageCsv.name} 을(를) 읽지 못해 ArcheryConfig 의 목록을 대신 씁니다.\n  {report}");
                }

                _bands = stageBands;
                return _bands;
            }
        }

        /// <summary>CSV 를 다시 읽게 합니다. (에디터에서 파일을 고친 뒤)</summary>
        public void InvalidateBands() => _bands = null;

        void OnEnable() => _bands = null;
        void OnValidate() => _bands = null;

        /// <summary>누적 명중 수가 속한 구간. 표를 위에서부터 훑고, 넘어서면 마지막 구간입니다.</summary>
        public StageBand BandForHits(int hits)
        {
            var bands = Bands;
            if (bands == null || bands.Length == 0) return null;

            for (int i = 0; i < bands.Length; i++)
                if (hits <= bands[i].upToHits) return bands[i];

            return bands[bands.Length - 1];
        }

        public float TargetSpeed(int hits)
        {
            var band = BandForHits(hits);
            return band != null ? band.targetSpeed : 1.6f;
        }

        public int AmmoMax(int hits)
        {
            var band = BandForHits(hits);
            return band != null ? Mathf.Max(1, band.ammoMax) : 5;
        }

        public float ArrowSpeed(int hits)
        {
            var band = BandForHits(hits);
            return band != null ? Mathf.Max(1f, band.arrowSpeed) : 16f;
        }

        public float ShrinkPercent(int hits)
        {
            var band = BandForHits(hits);
            return band != null ? band.shrinkPercent : 1f;
        }

        public float SizeMinPercent(int hits)
        {
            var band = BandForHits(hits);
            return band != null ? band.sizeMinPercent : 35f;
        }

        /// <summary>
        /// 과녁 중심에서 dx 만큼 떨어진 곳에 맞았을 때의 점수.
        /// 가운데가 rings 점, 한 링 바깥으로 갈 때마다 1점씩 줄고, 과녁을 벗어나면 0점입니다.
        /// </summary>
        public int PointsForOffset(float dx, float radius)
        {
            if (radius <= 0f) return 0;

            float r = Mathf.Abs(dx) / radius;
            if (r > 1f) return 0;

            int ringIndex = Mathf.Clamp(Mathf.FloorToInt(r * rings), 0, rings - 1);
            return rings - ringIndex;
        }
    }
}
