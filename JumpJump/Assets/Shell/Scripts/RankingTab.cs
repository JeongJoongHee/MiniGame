using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// 랭킹 창의 게임 탭 하나. 버튼이 부를 수 있는 함수는 인자가 없어야 해서,
    /// **몇 번째 탭인지를 들고 있는 작은 부품**을 탭마다 하나씩 붙입니다.
    /// </summary>
    public class RankingTab : MonoBehaviour
    {
        [SerializeField] RankingPopup popup;
        [SerializeField] int index;

        public void OnPressed()
        {
            if (popup != null) popup.SelectTab(index);
        }
    }
}
