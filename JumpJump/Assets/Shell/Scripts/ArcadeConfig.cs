using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// 껍데기(랭킹 · 광고)의 **튜닝 값을 모아 둔 곳**입니다.
    /// 각 미니게임의 `GameConfig` / `ArcheryConfig` 와 같은 자리이고,
    /// 난이도처럼 자주 고치는 값이 아니라서 CSV 가 아니라 에셋으로 둡니다.
    ///
    /// 에셋은 `Assets/Shell/Resources/ArcadeConfig.asset` 한 장이고,
    /// **Resources 폴더인 것이 중요합니다** — 씬에 참조를 굽지 않으므로
    /// 값만 고칠 때는 씬을 다시 굽지 않아도 됩니다. (글꼴 · 글자표와 같은 방식)
    ///
    /// **에셋이 없어도 앱은 돌아갑니다.** 그 경우 아래 기본값을 그대로 씁니다.
    /// 수치를 바꿔 달라는 요청이 오면 코드가 아니라 이 에셋을 고칠 것.
    /// </summary>
    [CreateAssetMenu(menuName = "Arcade/Arcade Config", fileName = "ArcadeConfig")]
    public class ArcadeConfig : ScriptableObject
    {
        /// <summary>Resources 안에서의 이름. 확장자는 빼고 적습니다.</summary>
        public const string ResourceName = "ArcadeConfig";

        [Header("광고")]
        [Tooltip("몇 판마다 전면 광고를 띄울지. 기획: 2판")]
        [Min(1)] public int playsPerAd = 2;

        [Tooltip("광고와 광고 사이의 최소 간격(초). 2판을 금방 끝내도 연달아 뜨지 않게 합니다")]
        [Min(0f)] public float minSecondsBetweenAds = 90f;

        [Tooltip("끄면 광고를 아예 띄우지 않습니다. 스토어별로 다르게 굽고 싶을 때 씁니다")]
        public bool adsEnabled = true;

        [Header("랭킹")]
        [Tooltip("서버에서 가져올 상위 몇 명까지")]
        [Min(1)] public int rankingTopCount = 100;

        [Tooltip("팝업 한 장에 보여 줄 줄 수")]
        [Min(1)] public int rankingVisibleRows = 8;

        [Tooltip("별명 최대 글자 수. 한글도 한 글자로 셉니다")]
        [Min(1)] public int nicknameMaxLength = 8;

        static ArcadeConfig _instance;

        /// <summary>
        /// 어디서나 쓰는 설정 한 벌. 에셋이 없으면 기본값이 담긴 임시 인스턴스를 만들어 줍니다.
        /// (그래서 <c>Instance</c> 는 절대 null 이 아닙니다)
        /// </summary>
        public static ArcadeConfig Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<ArcadeConfig>(ResourceName);
                if (_instance == null) _instance = CreateInstance<ArcadeConfig>();
                return _instance;
            }
        }
    }
}
