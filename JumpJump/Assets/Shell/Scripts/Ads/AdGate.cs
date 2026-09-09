using System;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **광고를 띄울 때인지 아닌지를 정하는 문지기**입니다. 광고를 실제로 띄우는 일은
    /// <see cref="IAdService"/> 가 하고, 여기서는 "지금 띄워도 되는가" 만 봅니다.
    ///
    /// 두 가지를 함께 봅니다.
    ///  - <b>판 수</b> : 마지막 광고 뒤로 <c>playsPerAd</c> 판(기본 2판)을 채웠는가
    ///  - <b>시간</b>  : 지난 광고로부터 <c>minSecondsBetweenAds</c> 초(기본 90초)가 지났는가
    ///
    /// 시간을 함께 보는 이유는, 두 판을 30초 만에 끝내는 경우가 흔하기 때문입니다.
    /// 판 수만 보면 광고가 연달아 떠서 사람이 앱을 지웁니다.
    ///
    /// 두 값 모두 <see cref="ArcadeConfig"/> 에셋에서 고칩니다. 코드에 숫자를 박지 않습니다.
    /// </summary>
    public static class AdGate
    {
        const string LastShownKey = "Arcade.LastAdUtcTicks";

        /// <summary>지금 광고를 띄울 때인지.</summary>
        public static bool ShouldShow
        {
            get
            {
                var config = ArcadeConfig.Instance;
                if (!config.adsEnabled) return false;
                if (PlayCounter.SinceAd < config.playsPerAd) return false;

                return SecondsSinceLastAd() >= config.minSecondsBetweenAds;
            }
        }

        /// <summary>광고를 보여 줬습니다. 판 수와 시각을 다시 셉니다.</summary>
        public static void MarkShown()
        {
            PlayCounter.ResetSinceAd();
            PlayerPrefs.SetString(LastShownKey, DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 마지막 광고 뒤로 몇 초가 지났는지. 한 번도 안 띄웠으면 아주 큰 값을 돌려주어
        /// **첫 광고는 시간 조건에 걸리지 않게** 합니다.
        /// </summary>
        static double SecondsSinceLastAd()
        {
            string saved = PlayerPrefs.GetString(LastShownKey, "");
            if (string.IsNullOrEmpty(saved) || !long.TryParse(saved, out long ticks))
                return double.MaxValue;

            // 폰의 시각을 뒤로 돌려 놓은 경우에도 음수가 나오지 않게 합니다.
            double seconds = (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
            return seconds < 0 ? double.MaxValue : seconds;
        }
    }
}
