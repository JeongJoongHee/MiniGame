using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **앱이 켜질 때 랭킹을 서버(Firebase)로 갈아 끼우는 곳**입니다. 첫 화면이 뜨기 전에 한 번 불립니다.
    ///
    /// 서버 주소(프로젝트 ID · API 키)는 <see cref="ArcadeConfig"/> 에 들어 있고, 그 값은
    /// 작업 폴더의 <c>google-services.json</c> 에서 Build All Scenes 때 자동으로 채워집니다.
    /// **값이 비어 있거나 <c>onlineRanking</c> 을 끄면 서버를 쓰지 않고 폰 안에만 저장합니다** —
    /// 1단계와 똑같이 돌아가므로 앱이 멈추는 일은 없습니다.
    ///
    /// 배치 모드 미리보기·플레이테스트는 플레이 모드가 아니라서 이 함수가 불리지 않습니다.
    /// 그래서 캡처는 늘 같은 표본 화면으로 찍히고, 검증 중에 서버에 무언가 올라가는 일도 없습니다.
    /// </summary>
    public static class OnlineBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var config = ArcadeConfig.Instance;

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
    }
}
