using System;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **광고가 없는 광고 서비스**입니다. 언제 불려도 아무것도 띄우지 않고 곧바로 넘어갑니다.
    ///
    /// 광고를 껐을 때(<c>adsEnabled</c>)와 배치 모드 검증에서 이것을 씁니다. (진짜 광고는 <see cref="AdMobService"/>)
    /// 로그만 남기므로 "2판마다 여기서 광고가 떴겠구나" 를 배치 모드에서도 확인할 수 있습니다.
    /// </summary>
    public class NoAdService : IAdService
    {
        readonly bool _log;

        public NoAdService(bool log = true)
        {
            _log = log;
        }

        /// <summary>없는 광고도 "준비된" 셈 칩니다. 흐름이 막히지 않아야 하기 때문입니다.</summary>
        public bool IsReady => true;

        public void Load() { }

        public void ShowInterstitial(Action onClosed)
        {
            if (_log) Debug.Log("[Arcade] (광고 자리) 전면 광고를 띄울 차례입니다 — 아직 붙이지 않아 그냥 넘어갑니다.");
            onClosed?.Invoke();
        }
    }
}
