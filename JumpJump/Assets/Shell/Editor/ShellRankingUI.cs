using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.EditorTools
{
    /// <summary>
    /// **랭킹 창과 별명 창을 굽는 곳**입니다. `ShellSceneBuilder` 가 길어져서 따로 두었습니다.
    ///
    ///  - 랭킹 창  : 로비에만. 게임 칸마다 붙은 랭킹 아이콘으로 엽니다. 게임 이름표가 탭입니다.
    ///  - 별명 창  : 미니게임 씬(처음 랭킹에 오를 때 한 번)과 로비(설정의 [별명 바꾸기]).
    ///
    /// 두 창 다 <b>빈 판 그림(Popup_Panel) 하나를 9-슬라이스로 늘려서</b> 만듭니다.
    /// 그래서 세로로 긴 창을 만들어도 테두리 두께가 그대로입니다 — 새 그림이 필요 없습니다.
    ///
    /// **씬을 손으로 편집하지 마세요.** 여기서 다시 구우면 전부 덮어써집니다.
    /// </summary>
    public static class ShellRankingUI
    {
        // 1080x1920 기준 픽셀입니다. 폰 해상도가 달라도 CanvasScaler 가 같이 늘려 줍니다.
        static readonly Vector2 RankingPanelSize = new Vector2(960f, 1320f);
        static readonly Vector2 NicknamePanelSize = new Vector2(900f, 640f);

        /// <summary>랭킹 창에 한 번에 보여 줄 줄 수. 실제 값은 ArcadeConfig 에서 읽습니다.</summary>
        const int FallbackRows = 8;

        static readonly Color Ink = new Color(0.231f, 0.106f, 0.078f);
        static readonly Color InkSoft = new Color(0.40f, 0.26f, 0.18f);

        // ------------------------------------------------------------------ 랭킹 창

        /// <summary>로비에 랭킹 창을 만들어 붙입니다. 돌려주는 것은 켜고 끄는 손잡이입니다.</summary>
        public static PopupPanel BuildRankingPopup(RectTransform canvasRoot, GameCatalog catalog, Object closeTarget)
        {
            var panelSprite = ShellArt.Load(ShellArt.PanelPath);
            var closeSprite = ShellArt.Load(ShellArt.CloseButtonPath);
            if (panelSprite == null || closeSprite == null) return null;

            var popup = ShellSceneBuilder.MakeSlicedPopup(canvasRoot, "RankingPopup", panelSprite,
                                                          RankingPanelSize, out var panel);
            float aspect = RankingPanelSize.x / RankingPanelSize.y;

            var view = panel.gameObject.AddComponent<RankingPopup>();

            // --- 머리 : "── 랭킹 ──" / 게임 이름 / "────  ────" (수정사항_03) ---
            // 예전에는 게임 이름표 그림이 탭으로 늘어서 있었는데, 게임 칸마다 여는 버튼이 생겨서
            // 창 안에서 게임을 갈아탈 일이 없어졌습니다. 그래서 그 게임의 이름만 글자로 보여 줍니다.
            var title = ShellUI.AddText(panel, "Title", 56, Ink);
            ShellUI.Place(title.rectTransform, new Vector2(0.5f, TitleY), new Vector2(0.22f, 0.06f));
            AddRule(panel, "TitleRuleLeft", 0.08f, 0.39f, TitleY);
            AddRule(panel, "TitleRuleRight", 0.61f, 0.92f, TitleY);

            var gameName = ShellUI.AddText(panel, "GameName", 76, Ink);
            ShellUI.Place(gameName.rectTransform, new Vector2(0.5f, GameNameY), new Vector2(0.84f, 0.075f));
            AddRule(panel, "NameRuleLeft", 0.08f, 0.46f, NameRuleY);
            AddRule(panel, "NameRuleRight", 0.54f, 0.92f, NameRuleY);

            var ids = new List<string>();
            var names = new List<string>();
            foreach (var entry in PlayableGames(catalog))
            {
                ids.Add(entry.id);
                names.Add(string.IsNullOrEmpty(entry.displayName) ? entry.id : entry.displayName.TrimEnd('!', ' '));
            }

            // --- 줄 ---
            int rowCount = Mathf.Max(1, RowCount());
            var rankLabels = new List<Object>();
            var nickLabels = new List<Object>();
            var scoreLabels = new List<Object>();

            const float top = 0.745f, bottom = 0.30f;
            float step = rowCount > 1 ? (top - bottom) / (rowCount - 1) : 0f;
            float rowHeight = Mathf.Min(0.055f, step * 0.9f);

            for (int i = 0; i < rowCount; i++)
            {
                float y = top - step * i;

                var rank = ShellUI.AddText(panel, "Rank_" + i, 40, Ink);
                rank.alignment = TextAnchor.MiddleRight;
                ShellUI.Place(rank.rectTransform, new Vector2(0.155f, y), new Vector2(0.14f, rowHeight));

                var nick = ShellUI.AddText(panel, "Nick_" + i, 40, Ink);
                nick.alignment = TextAnchor.MiddleLeft;
                ShellUI.Place(nick.rectTransform, new Vector2(0.45f, y), new Vector2(0.40f, rowHeight));

                var score = ShellUI.AddText(panel, "Score_" + i, 40, Ink);
                score.alignment = TextAnchor.MiddleRight;
                // 오른쪽 끝은 판 테두리에 물리므로 여유를 둡니다 (숫자가 잘렸었습니다).
                ShellUI.Place(score.rectTransform, new Vector2(0.80f, y), new Vector2(0.26f, rowHeight));

                rankLabels.Add(rank);
                nickLabels.Add(nick);
                scoreLabels.Add(score);
            }

            // --- 안내 글 / 내 순위 / 닫기 ---
            var status = ShellUI.AddText(panel, "Status", 40, InkSoft);
            ShellUI.Place(status.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(0.84f, 0.10f));

            var myLine = ShellUI.AddText(panel, "MyLine", 42, new Color(0.78f, 0.28f, 0.10f));
            ShellUI.Place(myLine.rectTransform, new Vector2(0.5f, 0.225f), new Vector2(0.86f, 0.055f));

            ShellSceneBuilder.MakePanelButton(panel, "Close", closeSprite, new Vector2(0.5f, 0.10f),
                                              aspect, closeTarget, "OnCancelPressed");

            ShellSceneBuilder.Wire(view,
                ("titleLabel", title), ("statusLabel", status), ("myLine", myLine), ("gameNameLabel", gameName));
            ShellSceneBuilder.WireArray(view, "rankLabels", rankLabels);
            ShellSceneBuilder.WireArray(view, "nickLabels", nickLabels);
            ShellSceneBuilder.WireArray(view, "scoreLabels", scoreLabels);
            ShellSceneBuilder.WireStrings(view, "gameIds", ids);
            ShellSceneBuilder.WireStrings(view, "gameNames", names);

            return popup;
        }

        // 랭킹 창 머리의 높이 (판 높이를 1 로 본 값)
        const float TitleY = 0.93f;
        const float GameNameY = 0.862f;
        const float NameRuleY = 0.806f;

        /// <summary>판 안에 가는 가로줄 하나. 그림 없이 색만 칠한 얇은 사각형입니다.</summary>
        static void AddRule(RectTransform panel, string name, float fromX, float toX, float y)
        {
            var line = ShellUI.AddImage(panel, name, null);
            line.color = new Color(Ink.r, Ink.g, Ink.b, 0.55f);
            ShellUI.Place(line.rectTransform, new Vector2((fromX + toX) * 0.5f, y), new Vector2(toX - fromX, 0.0026f));
        }

        // ------------------------------------------------------------------ 별명 창

        /// <summary>별명 창을 만들어 붙입니다 (미니게임 씬 · 로비 둘 다 이 함수입니다).</summary>
        public static NicknamePopup BuildNicknamePopup(RectTransform canvasRoot)
        {
            var panelSprite = ShellArt.Load(ShellArt.PanelPath);
            var okSprite = ShellArt.Load(ShellArt.OkButtonPath);
            var cancelSprite = ShellArt.Load(ShellArt.CancelButtonPath);
            if (panelSprite == null || okSprite == null || cancelSprite == null) return null;

            var popup = ShellSceneBuilder.MakeSlicedPopup(canvasRoot, "NicknamePopup", panelSprite,
                                                          NicknamePanelSize, out var panel);
            float aspect = NicknamePanelSize.x / NicknamePanelSize.y;

            var view = panel.gameObject.AddComponent<NicknamePopup>();

            var title = ShellUI.AddText(panel, "Title", 52, Ink);
            ShellUI.Place(title.rectTransform, new Vector2(0.5f, 0.855f), new Vector2(0.86f, 0.14f));

            var hint = ShellUI.AddText(panel, "Hint", 34, InkSoft);
            ShellUI.Place(hint.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.86f, 0.09f));

            var field = MakeInputField(panel, aspect);

            var message = ShellUI.AddText(panel, "Message", 34, new Color(0.78f, 0.28f, 0.10f));
            ShellUI.Place(message.rectTransform, new Vector2(0.5f, 0.395f), new Vector2(0.88f, 0.09f));

            ShellSceneBuilder.MakePanelButton(panel, "Ok", okSprite, new Vector2(0.29f, 0.185f),
                                              aspect, view, "OnConfirmPressed");
            ShellSceneBuilder.MakePanelButton(panel, "Cancel", cancelSprite, new Vector2(0.71f, 0.185f),
                                              aspect, view, "OnCancelPressed");

            ShellSceneBuilder.Wire(view,
                ("popup", popup), ("titleLabel", title), ("hintLabel", hint),
                ("messageLabel", message), ("field", field));

            return view;
        }

        /// <summary>
        /// 별명을 적는 칸입니다. 옛 방식(UnityEngine.UI.InputField)으로 만듭니다 —
        /// 이 프로젝트는 TMP 를 아직 임포트하지 않았고, 글자는 전부 이 방식으로 그립니다.
        /// </summary>
        static InputField MakeInputField(RectTransform panel, float panelAspect)
        {
            var background = ShellUI.AddImage(panel, "NicknameField", null, raycast: true);
            background.color = new Color(1f, 0.965f, 0.898f);
            ShellUI.Place(background.rectTransform, new Vector2(0.5f, 0.565f), new Vector2(0.74f, 0.135f));

            var text = ShellUI.AddText(background.rectTransform, "Text", 46, Ink);
            text.resizeTextForBestFit = false;         // 입력 칸에서는 글자 크기가 흔들리면 안 됩니다
            text.supportRichText = false;
            ShellUI.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.8f));

            var placeholder = ShellUI.AddText(background.rectTransform, "Placeholder", 46,
                                              new Color(0.55f, 0.45f, 0.35f, 0.7f));
            placeholder.resizeTextForBestFit = false;
            placeholder.text = StringTable.Get("nick.placeholder", "NICKNAME");
            ShellUI.Place(placeholder.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.8f));

            var field = background.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = background;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 8;
            return field;
        }

        static int RowCount()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArcadeConfig>(ShellConfigAsset.Path);
            return config != null ? config.rankingVisibleRows : FallbackRows;
        }

        static List<MiniGameEntry> PlayableGames(GameCatalog catalog)
        {
            var games = new List<MiniGameEntry>();
            if (catalog == null) return games;

            foreach (var entry in catalog.games)
                if (entry != null && entry.IsPlayable)
                    games.Add(entry);

            games.Sort((a, b) => a.slot.CompareTo(b.slot));
            return games;
        }
    }
}
