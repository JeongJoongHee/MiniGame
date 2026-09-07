using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Archery
{
    /// <summary>
    /// 터치 / 마우스 클릭 / 스페이스바를 하나의 "탭" 신호로 묶어 줍니다.
    /// 같은 프레임에 두 곳에서 소비되지 않도록 Consume() 을 제공합니다.
    ///
    /// (점프점프에도 같은 것이 있습니다. 미니게임끼리는 서로를 참조하지 않는다는
    ///  이 프로젝트의 규칙 때문에 각자 하나씩 가지고 있습니다. 게임이 더 늘어나면
    ///  껍데기(Arcade) 쪽으로 옮겨 공용으로 쓰는 편이 낫습니다)
    /// </summary>
    public static class TapInput
    {
        static int _consumedFrame = -1;

        public static bool Pressed
        {
            get
            {
                if (_consumedFrame == Time.frameCount) return false;
                return Raw();
            }
        }

        /// <summary>
        /// 지금 누른 자리가 버튼 같은 UI 위인지. 게임 오버 화면의 "LOBBY" 버튼을 눌렀을 때
        /// 그게 발사로도 세지 않게 막아 줍니다.
        /// </summary>
        public static bool OverUI
        {
            get
            {
                var events = EventSystem.current;
                return events != null && events.IsPointerOverGameObject();
            }
        }

        public static void Consume()
        {
            _consumedFrame = Time.frameCount;
        }

        static bool Raw()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) return true;

            return false;
        }
    }
}
