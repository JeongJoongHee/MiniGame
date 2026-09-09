using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **판 수를 셉니다.** 광고를 "2판마다" 띄우려면 이 값이 필요합니다.
    ///
    /// 폰에 저장하기 때문에 **로비로 나갔다 들어와도, 앱을 껐다 켜도 이어집니다.**
    /// (게임 씬을 새로 열 때마다 게임 코드는 처음부터 시작하므로, 세는 일은 여기서 맡습니다)
    ///
    /// 미니게임이 늘어나도 판 수는 **앱 전체로 하나**입니다.
    /// 점프점프 한 판 + 활쏘기 한 판 = 두 판입니다.
    /// </summary>
    public static class PlayCounter
    {
        const string TotalKey   = "Arcade.PlayCount";
        const string SinceAdKey = "Arcade.PlaysSinceAd";

        /// <summary>앱을 깐 뒤로 끝낸 총 판 수. 화면에 보여 줄 일은 없고 참고용입니다.</summary>
        public static int Total => PlayerPrefs.GetInt(TotalKey, 0);

        /// <summary>마지막 광고 뒤로 끝낸 판 수.</summary>
        public static int SinceAd => PlayerPrefs.GetInt(SinceAdKey, 0);

        /// <summary>한 판이 끝났습니다. <see cref="GameSession"/> 이 대신 불러 주므로 직접 부를 일은 없습니다.</summary>
        public static void RecordPlay()
        {
            PlayerPrefs.SetInt(TotalKey, Total + 1);
            PlayerPrefs.SetInt(SinceAdKey, SinceAd + 1);
            PlayerPrefs.Save();
        }

        /// <summary>광고를 띄웠으니 다시 셉니다.</summary>
        public static void ResetSinceAd()
        {
            PlayerPrefs.SetInt(SinceAdKey, 0);
            PlayerPrefs.Save();
        }
    }
}
