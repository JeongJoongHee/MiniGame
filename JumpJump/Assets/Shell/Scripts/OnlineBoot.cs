using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **앱이 켜질 때 바깥 서비스(랭킹 서버 · 광고)를 끼워 넣는 곳**입니다. 첫 화면이 뜨기 전에 한 번 불립니다.
    ///
    ///  - 랭킹 : Firebase. 서버 주소는 <see cref="ArcadeConfig"/> 에 있고, 작업 폴더의 <c>google-services.json</c>
    ///           에서 Build All Scenes 때 자동으로 채워집니다. 비어 있거나 <c>onlineRanking</c> 을 끄면 폰 안에만 저장합니다.
    ///  - 광고 : AdMob 전면 광고 (<see cref="AdMobService"/>). <c>adsEnabled</c> 를 끄면 광고 없이 돕니다.
    /// **어느 쪽이 없어도 앱은 멈추지 않습니다** — 빈자리는 "아무것도 안 하는" 구현이 채웁니다.
    ///
    /// 배치 모드 미리보기·플레이테스트는 플레이 모드가 아니라서 이 함수가 불리지 않습니다.
    /// 그래서 캡처는 늘 같은 표본 화면으로 찍히고, 검증 중에 서버에 올라가거나 광고가 뜨는 일이 없습니다.
    /// </summary>
    public static class OnlineBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var config = ArcadeConfig.Instance;
            BootRanking(config);
            BootAds(config);
        }

        static void BootRanking(ArcadeConfig config)
        {
            if (!config.onlineRanking)
            {
                Debug.Log("[Arcade] 온라인 랭킹이 꺼져 있습니다 (ArcadeConfig.onlineRanking). 이 폰 안에만 저장합니다.");
                return;
            }

            if (!config.HasFirebase)
            {
                Debug.LogWarning("[Arcade] Firebase 설정이 비어 있어 이 폰 안에만 저장합니다.\n" +
                                 "  작업 폴더에 google-services.json 을 두고 Build All Scenes 를 한 번 돌려 주세요.");
                return;
            }

            var service = new FirebaseRankingService(config.firebaseProjectId, config.firebaseApiKey);
            Services.Use(service);
            OnlinePump.Ensure();
            service.Warmup();
        }

        static void BootAds(ArcadeConfig config)
        {
            if (!config.adsEnabled)
            {
                Debug.Log("[Arcade] 광고가 꺼져 있습니다 (ArcadeConfig.adsEnabled).");
                return;
            }

            if (string.IsNullOrEmpty(config.admobAppId))
            {
                Debug.LogWarning("[Arcade] AdMob 앱 ID 가 비어 있어 광고 없이 돕니다 (ArcadeConfig.admobAppId).");
                return;
            }

            var ads = new AdMobService(config);
            Services.Use(ads);
            OnlinePump.Ensure();
            ads.Start();
        }
    }
}
