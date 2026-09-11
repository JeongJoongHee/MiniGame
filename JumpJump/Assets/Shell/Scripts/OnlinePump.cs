using Arcade.Online;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// 서버 답을 매 프레임 확인해 주는 보이지 않는 물체입니다 (<see cref="Http.Pump"/>).
    /// <see cref="OnlineBoot"/> 가 앱을 켤 때 하나 만들어 두고, 씬을 옮겨도 사라지지 않습니다.
    /// </summary>
    public class OnlinePump : MonoBehaviour
    {
        static OnlinePump _instance;

        public static void Ensure()
        {
            if (_instance != null) return;

            var go = new GameObject("[Online]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<OnlinePump>();
        }

        void Update()
        {
            Http.Pump();
        }
    }
}
