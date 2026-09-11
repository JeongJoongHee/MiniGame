using System.Collections.Generic;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **보이지 않는 치트 버튼**입니다. 설정 창의 빈 윗부분에 깔려 있고,
    /// 정해진 시간 안에 정해진 횟수(기본 3초 안에 5번)를 누르면 <see cref="DataResetPopup"/> 을 엽니다.
    /// 일반 사용자가 우연히 열 일이 없도록 숨겨 둔 것입니다.
    /// 횟수 · 시간 · 켜기/끄기는 <see cref="ArcadeConfig"/> (<c>cheatTaps</c> / <c>cheatTapSeconds</c> / <c>cheatsEnabled</c>).
    /// </summary>
    public class CheatTapZone : MonoBehaviour
    {
        [SerializeField] DataResetPopup resetPopup;

        readonly Queue<float> _taps = new Queue<float>();

        /// <summary>눌렸을 때 (버튼이 부릅니다).</summary>
        public void OnTapped()
        {
            var config = ArcadeConfig.Instance;
            if (!config.cheatsEnabled || resetPopup == null) return;

            float now = Time.unscaledTime;
            _taps.Enqueue(now);
            while (_taps.Count > 0 && now - _taps.Peek() > config.cheatTapSeconds) _taps.Dequeue();

            if (_taps.Count < config.cheatTaps) return;

            _taps.Clear();
            resetPopup.Open();
        }
    }
}
