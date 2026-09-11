using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// **치트 — 모든 게임 데이터 초기화** 창입니다. 로비 설정 창의 빈 윗부분을 빠르게 여러 번 누르면 뜹니다
    /// (<see cref="CheatTapZone"/>). 앱을 막 설치한 상태로 되돌려서 처음부터 다시 시험할 때 씁니다.
    ///
    /// [확인] 을 누르면
    ///  1) 서버의 내 기록(게임마다 점수 줄 · 별명 자리 · 익명 계정)을 지우고
    ///  2) 폰 안의 기록(최고 점수 · 판 수 · 광고 카운트 · 별명 · 로그인 정보)을 전부 지운 뒤
    ///  3) 타이틀 화면으로 돌아갑니다.
    /// 인터넷이 없어서 1) 을 못 해도 2) 와 3) 은 합니다 — 그때는 서버에 옛 기록이 주인 없이 남습니다.
    ///
    /// 이 앱의 기록은 전부 폰 저장소(PlayerPrefs) 한 곳에 있어서 "전부 지우기" 한 번이면 됩니다.
    /// **스토어 빌드에서는 <c>ArcadeConfig.cheatsEnabled</c> 를 꺼서 열리지 않게 합니다.**
    /// </summary>
    public class DataResetPopup : MonoBehaviour
    {
        [SerializeField] PopupPanel popup;
        [SerializeField] Text titleLabel;
        [SerializeField] Text bodyLabel;
        [SerializeField] Text messageLabel;

        [Tooltip("서버에서 지울 게임 목록을 여기서 가져옵니다")]
        [SerializeField] GameCatalog catalog;

        bool _busy;

        /// <summary>창을 엽니다. 치트가 꺼져 있으면 아무것도 하지 않습니다.</summary>
        public void Open()
        {
            if (!ArcadeConfig.Instance.cheatsEnabled || _busy) return;
            if (popup != null) popup.Open();
            Refresh();
        }

        /// <summary>글자를 채웁니다. 배치 미리보기도 이 함수를 직접 부릅니다.</summary>
        public void Refresh()
        {
            if (titleLabel != null) titleLabel.text = StringTable.Get("cheat.reset.title", "RESET ALL DATA");
            if (bodyLabel != null) bodyLabel.text = StringTable.Get("cheat.reset.body",
                "BEST SCORES, PLAY COUNT, NICKNAME AND RANKING\nWILL BE DELETED. THIS CANNOT BE UNDONE.");
            if (!_busy) SetMessage("");
        }

        /// <summary>[확인].</summary>
        public void OnConfirmPressed()
        {
            if (_busy) return;
            _busy = true;
            SetMessage(StringTable.Get("cheat.reset.working", "DELETING..."));

            var ids = new List<string>();
            if (catalog != null)
                foreach (var entry in catalog.games)
                    if (entry != null && !string.IsNullOrEmpty(entry.id)) ids.Add(entry.id);

            Services.Ranking.DeleteMyData(ids, serverOk =>
            {
                WipeLocal();
                SetMessage(serverOk
                    ? StringTable.Get("cheat.reset.done", "ALL DATA DELETED")
                    : StringTable.Get("cheat.reset.partial", "PHONE DATA DELETED (SERVER NOT REACHED)"));

                if (isActiveAndEnabled) StartCoroutine(GoToTitleSoon());
                else AppFlow.GoToTitle();
            });
        }

        /// <summary>[취소].</summary>
        public void OnCancelPressed()
        {
            if (_busy) return;
            if (popup != null) popup.Close();
        }

        /// <summary>
        /// 이 폰에 저장된 게임 기록을 전부 지웁니다. 에디터 메뉴(Tools > Arcade > Reset All Game Data)도 이것을 씁니다.
        /// </summary>
        public static void WipeLocal()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            PlayerIdentity.Reload();
            Debug.Log("[Arcade] 이 기기의 게임 데이터를 전부 지웠습니다.");
        }

        IEnumerator GoToTitleSoon()
        {
            // 결과 글자를 잠깐 읽을 수 있게 기다립니다. 광고·팝업과 무관하게 실제 시간으로 잽니다.
            yield return new WaitForSecondsRealtime(1.2f);
            _busy = false;
            AppFlow.GoToTitle();
        }

        void SetMessage(string text)
        {
            if (messageLabel == null) return;
            messageLabel.text = text;
            messageLabel.enabled = !string.IsNullOrEmpty(text);
        }
    }
}
