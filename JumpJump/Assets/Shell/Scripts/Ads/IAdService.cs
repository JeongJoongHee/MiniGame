using System;

namespace Arcade
{
    /// <summary>
    /// 광고를 띄우는 물건의 **겉모양**입니다. 게임 쪽은 이것만 알고,
    /// 안에 무엇이 들었는지(AdMob 인지 아무것도 아닌지)는 모릅니다.
    ///
    /// 이렇게 나눠 두면
    ///  - 광고 SDK 를 넣기 전에도 화면과 흐름을 다 만들 수 있고,
    ///  - 배치 모드 검증에서는 <see cref="NoAdService"/> 를 써서 광고를 건너뛸 수 있으며,
    ///  - 나중에 AdMob 을 붙일 때 <b>게임 코드는 한 줄도 고치지 않습니다.</b>
    /// </summary>
    public interface IAdService
    {
        /// <summary>지금 바로 보여 줄 수 있는 광고가 준비되어 있는지.</summary>
        bool IsReady { get; }

        /// <summary>
        /// 다음 광고를 미리 불러 둡니다. **띄울 때 불러오면 몇 초 멈춰 보이므로**
        /// 게임을 시작할 때와 광고를 한 번 보여 준 뒤에 미리 부릅니다.
        /// </summary>
        void Load();

        /// <summary>
        /// 전면 광고를 띄웁니다. 닫히면 <paramref name="onClosed"/> 를 부릅니다.
        /// <b>준비가 안 되어 있으면 광고를 건너뛰고 곧바로 <paramref name="onClosed"/> 를 불러야 합니다</b> —
        /// 광고 때문에 게임이 막히는 일은 없어야 합니다.
        /// </summary>
        void ShowInterstitial(Action onClosed);
    }
}
