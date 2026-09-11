using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// 설정 창의 **"내 별명 : 홍길동"** 한 줄입니다. 창이 켜질 때와 별명이 바뀔 때 새로 씁니다.
    /// </summary>
    public class NicknameLabel : MonoBehaviour
    {
        [SerializeField] Text label;

        void OnEnable()
        {
            PlayerIdentity.NicknameChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            PlayerIdentity.NicknameChanged -= Refresh;
        }

        /// <summary>글자를 다시 씁니다. 배치 미리보기도 이 함수를 직접 부릅니다.</summary>
        public void Refresh()
        {
            if (label == null) return;

            label.text = PlayerIdentity.HasNickname
                ? StringTable.Format("settings.nick", "NICKNAME : {nick}", ("nick", PlayerIdentity.Nickname))
                : StringTable.Get("settings.nick.none", "NO NICKNAME YET");
        }
    }
}
