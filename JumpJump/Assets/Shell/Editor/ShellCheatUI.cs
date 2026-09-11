using UnityEngine;

namespace Arcade.EditorTools
{
    /// <summary>
    /// **치트 — 모든 게임 데이터 초기화** 를 로비에 굽습니다. (2026-09-11)
    ///
    ///  - 설정 창의 빈 윗부분(사운드 조절이 들어갈 자리)에 **보이지 않는 누름 영역**을 깔고,
    ///  - 거기를 빠르게 여러 번 누르면 뜨는 **확인 창**을 만듭니다 (빈 판 9-슬라이스 + [확인] [취소]).
    ///
    /// 나중에 설정 창에 사운드 조절을 넣을 때 누름 영역과 겹치면, <see cref="HitCenter"/> 를 옮기면 됩니다.
    /// </summary>
    public static class ShellCheatUI
    {
        static readonly Vector2 PanelSize = new Vector2(900f, 700f);

        /// <summary>설정 판 안에서 누름 영역의 자리와 크기 (판 크기를 1 로 본 값). 판 윗부분의 빈자리입니다.</summary>
        static readonly Vector2 HitCenter = new Vector2(0.5f, 0.86f);
        static readonly Vector2 HitSize = new Vector2(0.86f, 0.20f);

        static readonly Color Ink = new Color(0.231f, 0.106f, 0.078f);
        static readonly Color InkSoft = new Color(0.40f, 0.26f, 0.18f);
        static readonly Color Warn = new Color(0.78f, 0.28f, 0.10f);

        /// <summary>
        /// 에디터에서 쓰는 같은 치트. **이 컴퓨터(에디터)의 게임 기록만** 지웁니다.
        /// 서버 기록까지 지우려면 Play 한 뒤 설정 창의 치트를 쓰세요 (로그인이 필요해서).
        /// </summary>
        [UnityEditor.MenuItem("Tools/Arcade/Reset All Game Data (Editor)", priority = 27)]
        public static void ResetEditorData()
        {
            if (!UnityEditor.EditorUtility.DisplayDialog("모든 게임 데이터 초기화",
                    "이 컴퓨터(에디터)에 저장된 최고 점수 · 판 수 · 별명 · 로그인 정보를 전부 지웁니다.\n" +
                    "서버의 랭킹 기록은 지워지지 않습니다 (Play 한 뒤 설정 창 치트를 쓰세요).",
                    "지우기", "취소"))
                return;

            DataResetPopup.WipeLocal();
        }

        /// <summary>로비에 붙입니다. 설정 창이 먼저 만들어져 있어야 합니다.</summary>
        public static DataResetPopup Build(RectTransform canvasRoot, GameCatalog catalog)
        {
            var panelSprite = ShellArt.Load(ShellArt.PanelPath);
            var okSprite = ShellArt.Load(ShellArt.OkButtonPath);
            var cancelSprite = ShellArt.Load(ShellArt.CancelButtonPath);
            var settingsPanel = canvasRoot.Find("SettingsPopup/Panel") as RectTransform;
            if (panelSprite == null || okSprite == null || cancelSprite == null || settingsPanel == null) return null;

            // --- 확인 창 ---
            var popup = ShellSceneBuilder.MakeSlicedPopup(canvasRoot, "DataResetPopup", panelSprite, PanelSize, out var panel);
            float aspect = PanelSize.x / PanelSize.y;
            var view = panel.gameObject.AddComponent<DataResetPopup>();

            var title = ShellUI.AddText(panel, "Title", 52, Ink);
            ShellUI.Place(title.rectTransform, new Vector2(0.5f, 0.85f), new Vector2(0.86f, 0.12f));

            var body = ShellUI.AddText(panel, "Body", 36, InkSoft);
            ShellUI.Place(body.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.86f, 0.22f));

            var message = ShellUI.AddText(panel, "Message", 36, Warn);
            ShellUI.Place(message.rectTransform, new Vector2(0.5f, 0.40f), new Vector2(0.88f, 0.09f));

            ShellSceneBuilder.MakePanelButton(panel, "Ok", okSprite, new Vector2(0.29f, 0.17f), aspect, view, "OnConfirmPressed");
            ShellSceneBuilder.MakePanelButton(panel, "Cancel", cancelSprite, new Vector2(0.71f, 0.17f), aspect, view, "OnCancelPressed");

            ShellSceneBuilder.Wire(view,
                ("popup", popup), ("titleLabel", title), ("bodyLabel", body), ("messageLabel", message), ("catalog", catalog));

            // --- 설정 창 윗부분의 보이지 않는 누름 영역 ---
            var hit = ShellUI.AddHitArea(settingsPanel, "CheatTapZone");
            ShellUI.Place((RectTransform)hit.transform, HitCenter, HitSize);
            var zone = hit.gameObject.AddComponent<CheatTapZone>();
            ShellSceneBuilder.Wire(zone, ("resetPopup", view));
            ShellSceneBuilder.BindClickPublic(hit, zone, "OnTapped");

            return view;
        }
    }
}
