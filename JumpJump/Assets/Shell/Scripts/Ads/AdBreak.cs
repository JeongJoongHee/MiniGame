using System;
using Arcade.Online;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **"다음으로 넘어가는 길목"에서 전면 광고를 띄우는 곳**입니다. 두 군데에서 부릅니다.
    ///  - 게임 오버 화면에서 <b>다시하기</b>를 누른 순간 (각 게임의 <c>Update</c> 한 줄)
    ///  - "로비로 나가시겠습니까?" 의 <b>[확인]</b> (<see cref="ShellMenu"/>)
    ///
    /// <b>게임 오버 "직후"가 아니라 사용자가 누른 "뒤"에 띄우는 이유</b> — 점프점프는 계속 화면을 두드리는 게임이라,
    /// 죽는 순간 광고가 튀어나오면 두드리던 손가락이 광고를 누르게 됩니다. 이런 "실수 클릭"은 AdMob 정책 위반이고
    /// 계정이 정지될 수 있습니다. 누른 뒤에 뜨는 광고는 "다음 판으로 가는 문" 이라 정책상 권장되는 자리입니다.
    ///
    /// 광고가 닫히면 <c>then</c> 을 부릅니다. 다시하기에서는 곧바로 판을 시작하지 않고
    /// <b>"터치하면 시작합니다" 화면으로</b> 돌려놓습니다 — 광고를 닫자마자 게임이 굴러가면 안 되니까요.
    /// </summary>
    public static class AdBreak
    {
        /// <summary>
        /// 광고가 닫힌다는 소식이 이 시간 안에 안 오면 닫힌 것으로 칩니다 (초).
        /// 어떤 이유로든 소식이 끊겨도 **게임이 영영 멈춰 있지 않게** 하는 안전장치입니다.
        /// </summary>
        const float GiveUpSeconds = 120f;

        static int _show;

        /// <summary>광고가 떠 있는 동안 true. 이때는 게임이 멈춥니다 (<see cref="PopupPanel.Blocking"/> 이 봅니다).</summary>
        public static bool Showing { get; private set; }

        /// <summary>
        /// 띄울 차례면 광고를 띄우고 true 를 돌려줍니다 — 부른 쪽은 하려던 일을 멈추고,
        /// 광고가 닫히면 <paramref name="then"/> 이 대신 이어서 합니다.
        /// 차례가 아니거나 광고가 준비되지 않았으면 false — 부른 쪽은 하던 대로 하면 됩니다.
        /// </summary>
        public static bool TryShow(Action then)
        {
            if (Showing) return true;
            if (!GameSession.Recording) return false;     // 배치 플레이테스트에서는 광고 없음
            if (!AdGate.ShouldShow) return false;

            var ads = Services.Ads;
            if (!ads.IsReady)
            {
                // 아직 못 불러왔으면 이번에는 건너뜁니다. 차례는 그대로 남아서 다음 길목에서 다시 봅니다.
                ads.Load();
                return false;
            }

            Showing = true;
            AdGate.MarkShown();

            int show = ++_show;
            bool done = false;
            void Close()
            {
                if (done || show != _show) return;
                done = true;
                Showing = false;
                PopupPanel.HoldInputThisFrame();   // 광고를 닫은 그 터치가 게임 입력으로 이어지지 않게
                then?.Invoke();
            }

            MainThread.PostDelayed(GiveUpSeconds, () =>
            {
                if (!done) Debug.LogWarning("[Arcade] 광고가 닫혔다는 소식이 없어 닫힌 것으로 칩니다.");
                Close();
            });

            ads.ShowInterstitial(Close);
            return true;
        }
    }
}
