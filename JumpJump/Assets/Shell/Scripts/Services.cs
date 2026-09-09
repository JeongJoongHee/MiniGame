namespace Arcade
{
    /// <summary>
    /// **바깥 서비스(랭킹 · 광고)를 꺼내 쓰는 한 곳**입니다.
    ///
    /// 처음에는 서버도 광고도 없는 구현이 들어 있습니다. 그래서
    /// <b>SDK 를 하나도 넣지 않은 지금 상태에서도 화면과 흐름이 전부 돌아갑니다.</b>
    ///
    /// 2 · 3단계에서 진짜 구현을 만들면, 앱이 켜질 때 <see cref="Use(IRankingService)"/> /
    /// <see cref="Use(IAdService)"/> 로 갈아 끼우기만 하면 됩니다.
    /// <b>게임 코드와 화면 코드는 한 줄도 고치지 않습니다.</b>
    /// </summary>
    public static class Services
    {
        static IRankingService _ranking;
        static IAdService _ads;

        /// <summary>랭킹. 아직 아무것도 붙이지 않았으면 이 폰 안에만 저장하는 구현입니다.</summary>
        public static IRankingService Ranking => _ranking ??= new LocalRankingService();

        /// <summary>광고. 아직 아무것도 붙이지 않았으면 그냥 넘어가는 구현입니다.</summary>
        public static IAdService Ads => _ads ??= new NoAdService();

        public static void Use(IRankingService ranking)
        {
            if (ranking != null) _ranking = ranking;
        }

        public static void Use(IAdService ads)
        {
            if (ads != null) _ads = ads;
        }
    }
}
