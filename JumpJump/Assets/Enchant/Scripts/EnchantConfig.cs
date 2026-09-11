using UnityEngine;

namespace Enchant
{
    /// <summary>
    /// 강화 표 한 줄. @Enchant_Sword.csv 의 한 줄에 해당합니다.
    /// **"지금 이 강화 수치일 때"** 의 값입니다 — +7 줄의 확률은 "+7 에서 +8 로 올라갈 확률" 입니다.
    /// </summary>
    [System.Serializable]
    public class EnchantLevel
    {
        [Tooltip("강화 수치 (Enchant_Level)")]
        public int level;

        [Tooltip("이 수치에서 강화를 누르면 성공할 확률. 만분율 (10000 = 100%). 0 이면 더 이상 강화할 수 없습니다")]
        public int successPer10000 = 10000;

        [Tooltip("이 수치에서 분해하면 받는 안전 강화 주문서 수 (Melt). 0 이면 분해 버튼이 꺼집니다")]
        public int meltScrolls;

        [Tooltip("분해했을 때 주문서가 나올 확률. 만분율. 표에 Melt_Prob 열이 없으면 100% 입니다")]
        public int meltPer10000 = 10000;

        [Tooltip("이 수치의 검 그림 이름 (Image). Art/Swords 의 같은 이름 그림을 씁니다")]
        public string image = "";
    }

    /// <summary>
    /// 검 강화의 튜닝 값. Assets/Enchant/EnchantConfig.asset 을 인스펙터에서 고치면 됩니다.
    ///
    /// **강화 확률 · 분해 보상 · 검 그림은 여기가 아니라 작업 폴더의 @Enchant_Sword.csv 가 원본입니다.**
    /// levels 는 CSV 를 읽은 결과를 눈으로 보라고 구워 둔 거울입니다 (점프점프 · 활쏘기와 같은 방식).
    /// </summary>
    [CreateAssetMenu(fileName = "EnchantConfig", menuName = "Enchant/Enchant Config")]
    public class EnchantConfig : ScriptableObject
    {
        [Header("규칙")]
        [Tooltip("처음 시작할 때 가진 안전 강화 주문서 수. 0 이면 +10 까지 올려 분해해야 첫 주문서가 생깁니다")]
        public int startSafeScrolls = 0;

        [Tooltip("분해 버튼을 누르면 \"분해하시겠습니까?\" 를 한 번 묻습니다. 높이 올린 검을 실수로 누르지 않게")]
        public bool confirmMelt = true;

        [Header("연출 시간 (초)")]
        [Tooltip("강화 버튼을 누르고 결과가 나오기까지. 검이 떨리며 두근거리는 시간입니다")]
        public float workSeconds = 0.6f;

        [Tooltip("\"성공!\" 같은 결과 글자가 화면에 남아 있는 시간")]
        public float bannerSeconds = 1.2f;

        [Tooltip("검이 부서지는 연출 뒤 결과 창이 뜨기까지")]
        public float breakRevealSeconds = 0.8f;

        [Tooltip("검이 부서진 뒤 [새 검 받기] 가 눌리기까지. 강화 버튼을 연타하던 손가락이 " +
                 "그 자리에서 곧바로 넘어가거나 광고를 누르지 않게 하는 잠금입니다")]
        public float newSwordLockSeconds = 1.4f;

        [Header("연출")]
        [Tooltip("검 뒤의 빛 색. 왼쪽 = +0, 오른쪽 = 표의 마지막 강화 수치")]
        public Gradient glowByLevel = DefaultGlow();

        [Tooltip("강화 중 검이 떨리는 최대 폭 (1080 기준 픽셀)")]
        public float workShake = 16f;

        [Header("강화 표")]
        [Tooltip("강화 표 CSV. 지정하면 게임이 실행될 때 이 파일을 읽습니다")]
        public TextAsset levelCsv;

        [Tooltip("CSV 를 읽은 결과가 여기에 구워집니다(눈으로 확인용). CSV 가 없거나 읽기에 실패하면 이 목록이 대신 쓰입니다")]
        public EnchantLevel[] levels = new EnchantLevel[0];

        EnchantLevel[] _levels;

        /// <summary>
        /// 실제로 쓰이는 강화 표 (강화 수치 오름차순). levelCsv 가 있으면 그 내용이고,
        /// 없거나 읽기에 실패하면 인스펙터의 levels 입니다. 한 번만 읽어 두고 재사용합니다.
        /// </summary>
        public EnchantLevel[] Levels
        {
            get
            {
                if (_levels != null) return _levels;

                if (levelCsv != null)
                {
                    var parsed = EnchantTable.Parse(levelCsv.text, out string report);
                    if (parsed != null)
                    {
                        if (!string.IsNullOrEmpty(report))
                            Debug.LogWarning($"[Enchant] {levelCsv.name} 을(를) 읽었지만 손본 부분이 있습니다.\n{report}");
                        _levels = parsed;
                        return _levels;
                    }

                    Debug.LogError($"[Enchant] {levelCsv.name} 을(를) 읽지 못해 EnchantConfig 의 목록을 대신 씁니다.\n  {report}");
                }

                _levels = levels ?? new EnchantLevel[0];
                return _levels;
            }
        }

        public void InvalidateLevels() => _levels = null;

        void OnEnable() => _levels = null;
        void OnValidate() => _levels = null;

        /// <summary>
        /// 이 강화 수치에 해당하는 줄. 표에서 **이 수치 이하인 마지막 줄**입니다.
        /// 그래서 표에 빠진 수치는 바로 앞 줄 값을, 표보다 높은 수치는 마지막 줄 값을 씁니다.
        /// </summary>
        public EnchantLevel RowFor(int level)
        {
            var rows = Levels;
            if (rows == null || rows.Length == 0) return null;

            EnchantLevel found = rows[0];
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].level > level) break;
                found = rows[i];
            }
            return found;
        }

        /// <summary>이 수치에서 강화에 성공할 확률 (만분율, 0~10000).</summary>
        public int SuccessChance(int level)
        {
            var row = RowFor(level);
            return row != null ? Mathf.Clamp(row.successPer10000, 0, 10000) : 0;
        }

        /// <summary>이 수치에서 강화 버튼을 누를 수 있는지. 확률 0 인 줄이 "최고 단계" 입니다.</summary>
        public bool CanEnchant(int level) => SuccessChance(level) > 0;

        /// <summary>이 수치에서 분해하면 받는 주문서 수. 0 이면 분해할 수 없습니다.</summary>
        public int MeltScrolls(int level)
        {
            var row = RowFor(level);
            return row != null ? Mathf.Max(0, row.meltScrolls) : 0;
        }

        public int MeltChance(int level)
        {
            var row = RowFor(level);
            return row != null ? Mathf.Clamp(row.meltPer10000, 0, 10000) : 10000;
        }

        /// <summary>이 수치의 검 그림 이름.</summary>
        public string ImageFor(int level)
        {
            var row = RowFor(level);
            return row != null ? row.image : "";
        }

        /// <summary>분해가 처음으로 가능해지는 강화 수치. 표에 분해 줄이 없으면 -1.</summary>
        public int FirstMeltLevel
        {
            get
            {
                foreach (var row in Levels)
                    if (row.meltScrolls > 0) return row.level;
                return -1;
            }
        }

        /// <summary>표의 마지막 강화 수치. 빛 색을 고를 때 기준으로 씁니다.</summary>
        public int LastTableLevel
        {
            get
            {
                var rows = Levels;
                return rows != null && rows.Length > 0 ? rows[rows.Length - 1].level : 0;
            }
        }

        public Color GlowColor(int level)
        {
            if (glowByLevel == null) return Color.white;
            float t = Mathf.Clamp01(level / Mathf.Max(1f, LastTableLevel));
            return glowByLevel.Evaluate(t);
        }

        static Gradient DefaultGlow()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.00f, 0.78f, 0.45f), 0.00f),   // 대장간 불빛
                    new GradientColorKey(new Color(0.55f, 0.80f, 1.00f), 0.25f),   // 푸른빛
                    new GradientColorKey(new Color(0.78f, 0.52f, 1.00f), 0.55f),   // 보랏빛
                    new GradientColorKey(new Color(1.00f, 0.86f, 0.30f), 0.80f),   // 금빛
                    new GradientColorKey(new Color(1.00f, 0.36f, 0.26f), 1.00f),   // 붉은빛
                },
                new[]
                {
                    new GradientAlphaKey(0.55f, 0f),
                    new GradientAlphaKey(0.95f, 1f),
                });
            return gradient;
        }
    }
}
