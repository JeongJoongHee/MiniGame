using UnityEngine;
using UnityEngine.InputSystem;

namespace Arcade
{
    /// <summary>
    /// 화면을 나가는 길을 한 곳에서 맡는 부품입니다. 타이틀 / 로비 / 미니게임 씬에
    /// 하나씩 들어가고, **그 씬에 필요한 팝업만 연결되어 있습니다.**
    ///
    ///  - 타이틀 : quitPopup 만            -> 뒤로 = "종료하시겠습니까?"
    ///  - 로비   : settingsPopup + quitPopup + nicknamePopup
    ///             -> 톱니바퀴 = 설정, 설정의 [종료] = 종료 확인, 설정의 [별명 바꾸기] = 별명 창
    ///  - 미니게임 : exitToLobbyPopup 만     -> 화살표 = "로비로 나가시겠습니까?"
    ///
    /// 버튼은 씬을 구울 때 이 함수들에 미리 걸려 있습니다 (ShellSceneBuilder.BindClick).
    /// **폰의 하드웨어 뒤로 버튼도 같은 길을 씁니다** — 아래 Update 참고.
    /// </summary>
    public class ShellMenu : MonoBehaviour
    {
        [Tooltip("로비의 톱니바퀴로 여는 설정 팝업. 다른 씬에서는 비워 둡니다")]
        [SerializeField] PopupPanel settingsPopup;

        [Tooltip("\"종료하시겠습니까?\" - 타이틀과 로비에만 있습니다")]
        [SerializeField] PopupPanel quitPopup;

        [Tooltip("\"로비로 나가시겠습니까?\" - 미니게임 씬에만 있습니다")]
        [SerializeField] PopupPanel exitToLobbyPopup;

        [Tooltip("설정 창의 [별명 바꾸기] 로 여는 별명 창. 로비에만 있습니다")]
        [SerializeField] NicknamePopup nicknamePopup;

        // ------------------------------------------------------------ 버튼이 부르는 함수들

        /// <summary>로비 오른쪽 위 톱니바퀴.</summary>
        public void OnSettingsPressed()
        {
            if (settingsPopup != null) settingsPopup.Open();
        }

        /// <summary>설정 팝업의 [종료]. 설정을 닫고 종료 확인을 띄웁니다.</summary>
        public void OnEndPressed()
        {
            if (settingsPopup != null) settingsPopup.Close();
            if (quitPopup != null) quitPopup.Open();
        }

        /// <summary>
        /// 설정 창의 [별명 바꾸기]. 설정 창은 열어 둔 채 그 위에 별명 창을 띄웁니다 —
        /// 다 바꾸고 닫으면 설정 창의 "내 별명" 이 새 별명으로 바뀌어 있습니다.
        /// 횟수 제한은 없습니다 (2026-09-11 사용자 결정 — "추천 설정대로").
        /// </summary>
        public void OnChangeNicknamePressed()
        {
            if (nicknamePopup != null) nicknamePopup.AskChange();
        }

        /// <summary>미니게임 오른쪽 위 뒤로가기 화살표.</summary>
        public void OnBackPressed()
        {
            if (exitToLobbyPopup != null) exitToLobbyPopup.Open();
        }

        /// <summary>[닫기] 와 [취소] 가 전부 이걸 부릅니다. 맨 위 팝업만 닫습니다.</summary>
        public void OnCancelPressed()
        {
            PopupPanel.CloseTop();
        }

        /// <summary>"종료하시겠습니까?" 의 [확인].</summary>
        public void OnQuitConfirmed()
        {
#if UNITY_EDITOR
            // 에디터에서는 Application.Quit 이 아무 일도 하지 않아서 확인이 안 됩니다.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// "로비로 나가시겠습니까?" 의 [확인]. 기획서대로 **게임 내용은 따로 저장하지 않습니다** —
        /// 씬을 새로 열기 때문에 다시 들어오면 처음부터입니다. (최고 점수만 PlayerPrefs 에 남습니다)
        /// </summary>
        public void OnExitToLobbyConfirmed()
        {
            if (exitToLobbyPopup != null) exitToLobbyPopup.Close();
            AppFlow.GoToLobby();
        }

        // ------------------------------------------------------------ 폰의 뒤로 버튼

        /// <summary>
        /// 안드로이드의 하드웨어 뒤로 버튼은 Input System 에서 Esc 키로 들어옵니다.
        /// (윈도우 에디터에서 Esc 를 눌러도 똑같이 확인할 수 있습니다)
        /// </summary>
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                OnHardwareBack();
        }

        /// <summary>
        /// 뒤로 버튼 한 번의 뜻은 "지금 화면에서 한 걸음 물러나기" 입니다.
        ///  1) 팝업이 떠 있으면 그것부터 닫고,
        ///  2) 미니게임이면 "로비로 나가시겠습니까?",
        ///  3) 타이틀 / 로비면 "종료하시겠습니까?" 를 띄웁니다.
        /// </summary>
        public void OnHardwareBack()
        {
            if (PopupPanel.CloseTop()) return;
            if (exitToLobbyPopup != null) { exitToLobbyPopup.Open(); return; }
            if (quitPopup != null) quitPopup.Open();
        }
    }
}
