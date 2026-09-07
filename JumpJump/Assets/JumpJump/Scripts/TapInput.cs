using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace JumpJump
{
    /// <summary>
    /// 터치 / 마우스 클릭 / 스페이스바를 하나의 "탭" 신호로 묶어 줍니다.
    /// 같은 프레임에 두 곳에서 소비되지 않도록 Consume() 을 제공합니다.
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
        /// 지금 누른 자리가 버튼 같은 UI 위인지. 게임 오버 화면의 "로비로" 버튼을 눌렀을 때
        /// 그게 점프(=재시작)로도 세지 않게 막아 줍니다.
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
