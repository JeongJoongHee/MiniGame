using System.Collections.Generic;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// 화면 한가운데 뜨는 팝업 한 장입니다. 설정 / "종료하시겠습니까?" /
    /// "로비로 나가시겠습니까?" 가 전부 이 컴포넌트를 하나씩 달고 있습니다.
    ///
    /// 팝업은 씬에 **꺼진 채로 미리 만들어져** 있고, Open() / Close() 로 켜고 끕니다.
    /// (실행 중에 만들지 않기 때문에 첫 등장이 끊기지 않습니다)
    ///
    /// 열려 있는 팝업을 static 목록으로 들고 있는 이유는 두 가지입니다.
    ///  - 폰의 뒤로 버튼이 **맨 위 팝업부터** 닫을 수 있어야 하고 (`CloseTop`),
    ///  - 팝업이 떠 있는 동안 미니게임이 멈춰야 하기 때문입니다 (`Blocking`).
    /// 미니게임의 Update 가 `Blocking` 이면 Step 을 건너뜁니다. 이렇게 하면
    /// **게임 로직(Step)은 손대지 않아도 되므로** 배치 모드 플레이테스트가 그대로 돕니다.
    /// </summary>
    public class PopupPanel : MonoBehaviour
    {
        static readonly List<PopupPanel> Stack = new List<PopupPanel>();

        static int _lastCloseFrame = -1;

        /// <summary>지금 화면에 팝업이 하나라도 떠 있는지. 미니게임을 멈출 때 씁니다.</summary>
        public static bool AnyOpen
        {
            get { Prune(); return Stack.Count > 0; }
        }

        /// <summary>
        /// 팝업이 떠 있거나 **바로 이 프레임에 닫혔는지.** 미니게임은 이 값을 봅니다.
        /// 닫힌 프레임까지 막는 이유는, [취소] 를 누른 그 터치가 손을 떼기도 전에
        /// 곧바로 점프·발사로 이어지지 않게 하려는 것입니다.
        /// </summary>
        public static bool Blocking => AnyOpen || _lastCloseFrame == Time.frameCount;

        /// <summary>가장 나중에 열린 팝업. 없으면 null.</summary>
        public static PopupPanel Top
        {
            get { Prune(); return Stack.Count > 0 ? Stack[Stack.Count - 1] : null; }
        }

        /// <summary>맨 위 팝업을 닫습니다. 닫을 것이 있었으면 true.</summary>
        public static bool CloseTop()
        {
            var top = Top;
            if (top == null) return false;
            top.Close();
            return true;
        }

        public bool IsOpen => gameObject.activeInHierarchy;

        public void Open()
        {
            if (!Stack.Contains(this)) Stack.Add(this);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();   // 팝업이 겹치면 나중에 연 것이 위로
        }

        public void Close()
        {
            Stack.Remove(this);
            _lastCloseFrame = Time.frameCount;
            gameObject.SetActive(false);
        }

        /// <summary>씬을 옮기거나 꺼지면 목록에서도 빠집니다.</summary>
        void OnDisable()
        {
            Stack.Remove(this);
        }

        /// <summary>씬이 통째로 사라진 뒤 남아 있을 수 있는 껍데기를 걸러 냅니다.</summary>
        static void Prune()
        {
            for (int i = Stack.Count - 1; i >= 0; i--)
                if (Stack[i] == null || !Stack[i].gameObject.activeInHierarchy)
                    Stack.RemoveAt(i);
        }
    }
}
