using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Arcade.EditorTools
{
    /// <summary>
    /// 타이틀 화면과 로비(게임 선택) 화면을 메뉴 한 번으로 통째로 굽습니다.
    /// JumpJump 의 씬 빌더와 같은 방식이라, 하이어라키를 손으로 만질 일이 없습니다.
    ///
    /// **씬을 직접 편집하지 마세요.** 여기서 다시 구우면 전부 덮어써집니다.
    /// 화면 구성을 바꾸려면 이 파일(또는 TitleScreen / LobbyScreen 의 배치 값)을 고치세요.
    /// </summary>
    public static class ShellSceneBuilder
    {
        public const string TitleScenePath = "Assets/Shell/Scenes/TitleScene.unity";
        public const string LobbyScenePath = "Assets/Shell/Scenes/LobbyScene.unity";
        public const string CatalogPath = "Assets/Shell/GameCatalog.asset";
        public const string GameScenePath = "Assets/JumpJump/Scenes/GameScene.unity";
        public const string ArcheryScenePath = "Assets/Archery/Scenes/ArcheryScene.unity";
        public const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        // --- 팝업 배치 ---------------------------------------------------------
        // 팝업은 배경 그림이 아니라 **화면(캔버스)** 한가운데 뜹니다. 그래서 크기를
        // 1080x1920 기준 픽셀로 잡습니다. 폰 해상도가 달라도 CanvasScaler 가 같이 늘려 줍니다.

        /// <summary>팝업 판의 가로 폭 (1080 중 900 = 화면 폭의 83%). 세로는 그림 비율대로.</summary>
        const float PopupWidth = 900f;
        /// <summary>판 안에서 버튼 하나의 가로 폭 (판 폭을 1 로 본 값).</summary>
        const float PopupButtonWidth = 0.30f;
        /// <summary>판 안에서 버튼 두 개의 좌우 위치.</summary>
        const float PopupButtonLeftX = 0.29f;
        const float PopupButtonRightX = 0.71f;
        /// <summary>글자가 그려진 판(질문 팝업)의 버튼 높이. 판 아래쪽 빈자리입니다.</summary>
        const float AskButtonY = 0.28f;
        /// <summary>빈 판(설정 팝업)의 버튼 높이. 위쪽은 나중에 사운드 조절을 넣을 자리로 비워 둡니다.</summary>
        const float SettingsButtonY = 0.24f;

        /// <summary>미니게임 오른쪽 위 뒤로가기 화살표의 가로 폭 (1080 기준 픽셀).</summary>
        const float BackButtonWidth = 240f;

        // --- 로비 톱니바퀴 -----------------------------------------------------
        // 2026-09-07 에 로비 그림에서 지웠던 톱니바퀴를, 이번에는 **진짜 버튼**으로 다시 얹습니다.
        // 자리는 원래 그림에 그려져 있던 그 자리입니다 (Lobby_원본_톱니바퀴포함.png 에서 재서 넣었습니다).
        // 값은 로비 그림 안에서의 비율이라 폰 해상도가 달라도 같은 자리에 붙습니다.

        static readonly Vector2 GearCenter = new Vector2(0.9268f, 0.9496f);
        // (랭킹 아이콘은 2026-09-11 에 왼쪽 위에서 **게임 칸마다**로 옮겼습니다. 자리는 LobbyScreen.rankCenter)
        const float GearWidth = 0.085f;

        // --- 설정 창 안의 "내 별명" 줄 (2026-09-11) --------------------------------
        // 판 크기를 1 로 본 비율입니다. 버튼(SettingsButtonY)보다 위, 나중에 사운드 조절이 들어갈 자리보다 아래.

        /// <summary>"내 별명 : 홍길동" 과 [별명 바꾸기] 가 놓이는 높이.</summary>
        const float NicknameRowY = 0.63f;
        /// <summary>"내 별명 : 홍길동" 글자 칸의 가운데 x 와 폭.</summary>
        const float NicknameLabelX = 0.37f;
        const float NicknameLabelWidth = 0.50f;
        /// <summary>[별명 바꾸기] 버튼의 가운데 x 와 폭.</summary>
        const float NicknameButtonX = 0.76f;
        const float NicknameButtonWidth = 0.30f;
        /// <summary>손가락으로 누를 영역은 그림보다 넉넉하게 잡습니다.</summary>
        const float GearHitWidth = 0.135f;

        [MenuItem("Tools/Arcade/Build All Scenes", priority = 0)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 0) 화면에 나오는 글자표. 두 게임이 다 쓰므로 미니게임 씬보다 먼저 가져옵니다.
            ShellStringsCsv.Sync();

            // 0b) 껍데기 설정 에셋 (광고 간격 · 랭킹 줄 수 · 별명 길이). 없으면 기본값으로 만들어 둡니다.
            ShellConfigAsset.LoadOrCreate();

            // 0c) 랭킹 서버 주소. 작업 폴더의 google-services.json 에서 설정 에셋으로 옮겨 적습니다.
            ShellFirebaseConfig.Sync();

            // 1) 미니게임 씬들 (각 게임의 빌더가 자기 CSV 동기화까지 같이 합니다)
            JumpJump.EditorTools.JumpJumpSceneBuilder.BuildScene();
            Archery.EditorTools.ArcherySceneBuilder.BuildScene();

            // 2) 껍데기 화면 두 장
            BuildTitleScene();
            BuildLobbyScene();

            RegisterBuildSettings();
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // 앱을 켜면 타이틀부터 나오도록, 마지막에 타이틀 씬을 열어 둡니다.
            EditorSceneManager.OpenScene(TitleScenePath);

            Debug.Log("[Arcade] 화면 4장을 모두 구웠습니다.\n" +
                      "  0. " + TitleScenePath + "  (앱 시작 지점)\n" +
                      "  1. " + LobbyScenePath + "\n" +
                      "  2. " + GameScenePath + "   (점프점프)\n" +
                      "  3. " + ArcheryScenePath + "   (활쏘기)\n" +
                      "  Play 를 누르면 타이틀 -> 로비 -> 미니게임 순으로 들어갑니다.");
        }

        [MenuItem("Tools/Arcade/Build Title Scene", priority = 1)]
        public static void BuildTitleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (PopulateTitle() == null) return;

            Directory.CreateDirectory("Assets/Shell/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        [MenuItem("Tools/Arcade/Build Lobby Scene", priority = 2)]
        public static void BuildLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (PopulateLobby() == null) return;

            Directory.CreateDirectory("Assets/Shell/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        // ------------------------------------------------------------------ 타이틀

        public static TitleScreen PopulateTitle()
        {
            ShellArt.Sync(force: false);

            var titleSprite = ShellArt.Load(ShellArt.TitlePath);
            var titleTextSprite = ShellArt.Load(ShellArt.TitleTextPath);
            var startSprite = ShellArt.Load(ShellArt.StartButtonPath);
            if (titleSprite == null || titleTextSprite == null || startSprite == null) return null;

            MakeCamera(SampleEdgeColor(ShellArt.TitlePath));
            MakeEventSystem();

            var canvas = MakeCanvas("TitleCanvas");
            var root = canvas.GetComponent<RectTransform>();

            // 타이틀은 풍경이라 잘려도 상관없으므로 화면을 꽉 채웁니다.
            var backdrop = MakeBackdrop(root, titleSprite, AspectFitMode.Cover);

            // 게임 이름은 글자가 아니라 그림입니다. (수정 리포트 2번)
            // 배경이 아니라 **캔버스(화면)** 에 붙입니다 — 폰이 길어서 배경 좌우가 잘려도
            // 이름이 같이 잘리지 않게 하려는 것입니다. 자세한 이유는 TitleScreen.Apply 주석.
            var titleImage = ShellUI.AddImage(root, "GameTitle", titleTextSprite);

            var startImage = ShellUI.AddImage(backdrop, "StartButton", startSprite, raycast: true);
            var startButton = startImage.gameObject.AddComponent<Button>();
            startButton.targetGraphic = startImage;
            startButton.transition = Selectable.Transition.ColorTint;
            var colors = startButton.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            startButton.colors = colors;
            startImage.gameObject.AddComponent<PulseScale>();

            var screenGo = new GameObject("TitleScreen");
            var screen = screenGo.AddComponent<TitleScreen>();

            Wire(screen,
                ("backdrop", backdrop),
                ("startButton", startImage),
                ("titleImage", titleImage));

            // 버튼이 눌렸을 때 TitleScreen.OnStartPressed 가 불리도록 씬에 저장해 둡니다.
            BindClick(startButton, screen, "OnStartPressed");

            // 타이틀에는 나가기 버튼이 없습니다. 대신 **폰의 뒤로 버튼**으로 앱을 끌 수 있도록
            // 종료 확인 팝업만 얹어 둡니다. (수정사항_02 3쪽)
            MakeQuitMenu(root, quitOnly: true);

            screen.Apply();
            return screen;
        }

        /// <summary>
        /// "종료하시겠습니까?" 팝업과 그것을 여는 ShellMenu 를 만듭니다.
        /// quitOnly 가 false 면 설정 팝업(빈 판 + [종료] [닫기])도 같이 만들어 붙입니다.
        /// </summary>
        static ShellMenu MakeQuitMenu(RectTransform root, bool quitOnly)
        {
            var quitSprite = ShellArt.Load(ShellArt.AskQuitPath);
            var okSprite = ShellArt.Load(ShellArt.OkButtonPath);
            var cancelSprite = ShellArt.Load(ShellArt.CancelButtonPath);
            if (quitSprite == null || okSprite == null || cancelSprite == null) return null;

            var menuGo = new GameObject("ShellMenu");
            var menu = menuGo.AddComponent<ShellMenu>();

            // 설정 팝업을 먼저 만듭니다. 종료 확인이 나중이라 설정 위로 뜹니다.
            PopupPanel settings = null;
            if (!quitOnly)
            {
                var panelSprite = ShellArt.Load(ShellArt.PanelPath);
                var endSprite = ShellArt.Load(ShellArt.EndButtonPath);
                var closeSprite = ShellArt.Load(ShellArt.CloseButtonPath);
                if (panelSprite != null && endSprite != null && closeSprite != null)
                {
                    settings = MakePopup(root, "SettingsPopup", panelSprite, out var panel, out float aspect);

                    // 판 맨 위쪽은 일부러 비워 둡니다 — 나중에 사운드 조절이 들어갈 자리입니다.
                    MakeNicknameRow(panel, aspect, menu);

                    MakePopupButton(panel, "End", endSprite, new Vector2(PopupButtonLeftX, SettingsButtonY),
                                    aspect, menu, "OnEndPressed");
                    MakePopupButton(panel, "Close", closeSprite, new Vector2(PopupButtonRightX, SettingsButtonY),
                                    aspect, menu, "OnCancelPressed");
                }
            }

            var quit = MakeAskPopup(root, "QuitPopup", quitSprite, okSprite, cancelSprite,
                                    menu, "OnQuitConfirmed", "OnCancelPressed");

            Wire(menu, ("quitPopup", quit));
            if (settings != null) Wire(menu, ("settingsPopup", settings));
            return menu;
        }

        /// <summary>
        /// 설정 창 안의 한 줄 — **"내 별명 : 홍길동"  [별명 바꾸기]**. (수정사항_02, 2026-09-11)
        ///
        /// [별명 바꾸기] 는 그림 버튼이 아니라 **빈 이름표 그림 위에 글자**를 얹은 버튼입니다.
        /// 한글 글꼴이 들어 있어서 새 그림 없이도 만들 수 있고, 문구는 strings.csv 에서 바꿉니다.
        /// 진짜 버튼 그림을 받으면 이 자리를 그림 버튼으로 바꾸면 됩니다.
        /// </summary>
        static void MakeNicknameRow(RectTransform panel, float panelAspect, ShellMenu menu)
        {
            var ink = new Color(0.231f, 0.106f, 0.078f);

            var label = ShellUI.AddText(panel, "MyNickname", 40, ink);
            label.alignment = TextAnchor.MiddleLeft;
            ShellUI.Place(label.rectTransform, new Vector2(NicknameLabelX, NicknameRowY),
                          new Vector2(NicknameLabelWidth, 0.14f));
            var view = label.gameObject.AddComponent<NicknameLabel>();
            Wire(view, ("label", label));
            view.Refresh();

            var plateSprite = ShellArt.Load(ShellArt.BlankPlatePath);
            var button = ShellUI.AddImage(panel, "ChangeNickname", plateSprite, raycast: true);
            float height = plateSprite != null
                ? ShellUI.HeightForWidth(plateSprite, NicknameButtonWidth, panelAspect)
                : 0.16f;
            ShellUI.Place(button.rectTransform, new Vector2(NicknameButtonX, NicknameRowY),
                          new Vector2(NicknameButtonWidth, height));
            MakeButton(button, menu, "OnChangeNicknamePressed");

            var text = ShellUI.AddText(button.rectTransform, "Label", 36, ink);
            ShellUI.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.84f, 0.62f));
            text.text = StringTable.Get("settings.nick.change", "CHANGE");
            var localized = text.gameObject.AddComponent<LocalizedText>();
            WireText(localized, ("key", "settings.nick.change"), ("fallback", "CHANGE"));
        }

        // ------------------------------------------------------------------ 로비

        public static LobbyScreen PopulateLobby()
        {
            ShellArt.Sync(force: false);
            var catalog = LoadOrCreateCatalog();

            var lobbySprite = ShellArt.Load(ShellArt.LobbyPath);
            var playSprite = ShellArt.Load(ShellArt.PlayButtonPath);
            var gearSprite = ShellArt.Load(ShellArt.SettingButtonPath);
            if (lobbySprite == null || playSprite == null || gearSprite == null) return null;

            // 로비는 칸이 잘리면 안 되므로 그림 전체가 보이게 넣고,
            // 남는 위아래 여백은 배경 테두리 색을 어둡게 깔아 액자처럼 보이게 합니다.
            MakeCamera(Darken(SampleEdgeColor(ShellArt.LobbyPath), 0.25f));
            MakeEventSystem();

            var canvas = MakeCanvas("LobbyCanvas");
            var root = canvas.GetComponent<RectTransform>();
            var backdrop = MakeBackdrop(root, lobbySprite, AspectFitMode.Contain);

            // 설정 팝업 + 종료 확인 팝업 + 폰 뒤로 버튼 처리. (수정사항_02 1쪽)
            var menu = MakeQuitMenu(root, quitOnly: false);

            // 오른쪽 위 톱니바퀴. 그림과 누를 영역을 따로 두는 이유는 톱니가 작아서
            // 손가락으로 누르기 빠듯하기 때문입니다. 누를 영역이 나중에 만들어져 위에 옵니다.
            float frameAspect = lobbySprite.rect.width / lobbySprite.rect.height;

            var gear = ShellUI.AddImage(backdrop, "SettingsIcon", gearSprite);
            ShellUI.Place(gear.rectTransform, GearCenter,
                          new Vector2(GearWidth, ShellUI.HeightForWidth(gearSprite, GearWidth, frameAspect)));

            var gearHit = ShellUI.AddHitArea(backdrop, "SettingsButton");
            ShellUI.Place((RectTransform)gearHit.transform, GearCenter, new Vector2(GearHitWidth, GearHitWidth * frameAspect));
            if (menu != null) BindClick(gearHit, menu, "OnSettingsPressed");

            // 랭킹 창. 게임 이름표가 탭이라 카탈로그가 필요합니다.
            // 여는 버튼은 로비 왼쪽 위가 아니라 **게임 칸마다** 있습니다 (수정사항_02, 2026-09-11).
            // 칸은 실행할 때 LobbyScreen 이 만들므로, 아이콘 그림과 창을 LobbyScreen 에 넘겨 둡니다.
            // 그림이 없으면 ShellArt 가 임시 그림(시상대 모양)을 만들어 둡니다.
            var rankSprite = ShellArt.Load(ShellArt.RankButtonPath);
            var rankingPopup = ShellRankingUI.BuildRankingPopup(root, catalog, menu);
            var rankingView = rankingPopup != null ? rankingPopup.GetComponentInChildren<RankingPopup>(true) : null;

            // 별명 창. 설정 창의 [별명 바꾸기] 로 엽니다 (미니게임 씬의 것과 같은 창입니다).
            var nicknamePopup = ShellRankingUI.BuildNicknamePopup(root);
            if (menu != null && nicknamePopup != null) Wire(menu, ("nicknamePopup", nicknamePopup));

            var screenGo = new GameObject("LobbyScreen");
            var screen = screenGo.AddComponent<LobbyScreen>();

            // 이름표는 게임마다 다른 그림이라 카탈로그에 들어 있습니다.
            // 여기서는 PLAY 버튼과, 이름표 그림이 아직 없는 게임에 깔릴 빈 이름표를 넘깁니다.
            Wire(screen,
                ("catalog", catalog),
                ("backdrop", backdrop),
                ("fallbackNamePlate", ShellArt.Load(ShellArt.BlankPlatePath)),
                ("playButton", playSprite),
                ("rankIcon", rankSprite),
                ("rankBadge", ShellArt.Load(ShellArt.RankBadgePath)),
                ("ranking", rankingView));

            screen.Build();
            return screen;
        }

        // ------------------------------------------------------------------ 공통 부품

        static Camera MakeCamera(Color background)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            if (go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
                go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            return cam;
        }

        /// <summary>
        /// 미니게임 화면의 **나가는 길 한 벌**을 통째로 만들어 붙입니다.
        /// 오른쪽 맨 위의 뒤로가기 화살표 + "로비로 나가시겠습니까?" 팝업 + ShellMenu.
        ///
        /// 게임마다 따로 만들지 않고 여기 한 곳에서 만들어, 어느 게임에서나
        /// **같은 자리·같은 모양**이 되게 합니다. 게임 중에도 항상 보입니다.
        /// 미니게임을 새로 만들 때도 이 함수 한 줄만 부르면 됩니다 — 게임 쪽 코드는 필요 없습니다.
        ///
        /// 오버레이(시작 안내 / 게임 오버)보다 나중에 불러야 어두운 막 위로 올라옵니다.
        /// 이 버튼을 누른 것이 점프·발사로 세지 않는 것은 각 게임의 `TapInput.OverUI` 가 막아 주고,
        /// 팝업이 떠 있는 동안 게임이 멈추는 것은 각 게임 Update 의 `PopupPanel.AnyOpen` 이 맡습니다.
        /// </summary>
        /// <param name="root">HUD 캔버스의 RectTransform</param>
        public static ShellMenu MakeExitToLobbyUI(RectTransform root)
        {
            ShellArt.Sync(force: false);

            var backSprite = ShellArt.Load(ShellArt.BackButtonPath);
            var askSprite = ShellArt.Load(ShellArt.AskExitPath);
            var okSprite = ShellArt.Load(ShellArt.OkButtonPath);
            var cancelSprite = ShellArt.Load(ShellArt.CancelButtonPath);
            if (backSprite == null || askSprite == null || okSprite == null || cancelSprite == null) return null;

            // 캔버스에 GraphicRaycaster 가 없으면 **버튼이 아예 눌리지 않고**
            // `TapInput.OverUI` 도 항상 false 가 됩니다. 미니게임의 HUD 캔버스는 글자만
            // 그리려고 만들어져 있어서 붙어 있지 않은 경우가 많으므로 여기서 보장합니다.
            // (2026-09-08 에 발견 — 그전까지 "< LOBBY" 버튼이 이 이유로 안 눌렸습니다)
            if (root.GetComponent<GraphicRaycaster>() == null)
                root.gameObject.AddComponent<GraphicRaycaster>();

            var menuGo = new GameObject("ShellMenu");
            var menu = menuGo.AddComponent<ShellMenu>();

            // 1) 오른쪽 맨 위 화살표
            var back = ShellUI.AddImage(root, "BackButton", backSprite, raycast: true);
            var backRt = back.rectTransform;
            backRt.anchorMin = backRt.anchorMax = backRt.pivot = new Vector2(1f, 1f);
            backRt.sizeDelta = new Vector2(BackButtonWidth, BackButtonWidth * backSprite.rect.height / backSprite.rect.width);
            backRt.anchoredPosition = new Vector2(-36f, -46f);
            back.transform.SetAsLastSibling();
            MakeButton(back, menu, "OnBackPressed");

            // 2) "로비로 나가시겠습니까?" 팝업. 화살표보다 나중에 만들어 화살표 위를 덮습니다.
            var popup = MakeAskPopup(root, "ExitToLobbyPopup", askSprite, okSprite, cancelSprite,
                                     menu, "OnExitToLobbyConfirmed", "OnCancelPressed");

            Wire(menu, ("exitToLobbyPopup", popup));

            // 3) 랭킹 : 한 판이 끝나면 점수를 올립니다.
            //    별명이 아직 없으면 이 창으로 한 번만 물어봅니다 (처음 랭킹에 오를 때).
            //    게임 쪽 코드는 EndRun 의 한 줄이 전부이고, 나머지는 RankingFlow 가 합니다.
            var nickname = ShellRankingUI.BuildNicknamePopup(root);
            var flow = new GameObject("RankingFlow").AddComponent<RankingFlow>();
            if (nickname != null) Wire(flow, ("nicknamePopup", nickname));

            return menu;
        }

        // ------------------------------------------------------------------ 팝업 부품

        /// <summary>
        /// 질문이 그려진 판(Popup_Ask_*) + [확인] [취소] 두 개짜리 팝업을 만듭니다.
        /// 만들어진 팝업은 **꺼진 채로** 씬에 남아 있다가 ShellMenu 가 켭니다.
        /// </summary>
        static PopupPanel MakeAskPopup(RectTransform canvasRoot, string name, Sprite panelSprite,
                                       Sprite okSprite, Sprite cancelSprite,
                                       Object target, string okMethod, string cancelMethod)
        {
            var popup = MakePopup(canvasRoot, name, panelSprite, out var panel, out float panelAspect);

            MakePopupButton(panel, "Ok", okSprite, new Vector2(PopupButtonLeftX, AskButtonY),
                            panelAspect, target, okMethod);
            MakePopupButton(panel, "Cancel", cancelSprite, new Vector2(PopupButtonRightX, AskButtonY),
                            panelAspect, target, cancelMethod);
            return popup;
        }

        /// <summary>
        /// 팝업 한 장의 뼈대입니다. 화면 전체를 덮는 어두운 막 + 한가운데 판.
        ///
        /// **어두운 막이 터치를 전부 먹기 때문에** 팝업이 떠 있는 동안 뒤쪽 버튼
        /// (PLAY / 게임 화면)이 눌리지 않습니다. 따로 막는 코드가 필요 없습니다.
        /// </summary>
        /// <param name="panel">판의 사각형. 여기에 버튼을 비율로 붙입니다</param>
        /// <param name="panelAspect">판의 화면상 가로/세로 비율 (버튼이 안 찌그러지게 하는 데 씁니다)</param>
        static PopupPanel MakePopup(RectTransform canvasRoot, string name, Sprite panelSprite,
                                    out RectTransform panel, out float panelAspect)
        {
            var rootGo = new GameObject(name, typeof(RectTransform));
            rootGo.transform.SetParent(canvasRoot, false);
            rootGo.transform.SetAsLastSibling();

            var root = (RectTransform)rootGo.transform;
            Stretch(root);

            var dim = ShellUI.AddImage(root, "Dim", null, raycast: true);
            // 이 프로젝트는 Linear 컬러 공간이라 같은 알파라도 감마 때보다 옅게 보입니다.
            // 0.70 이 화면에서 "확실히 뒤가 죽는" 정도입니다.
            dim.color = new Color(0f, 0f, 0f, 0.70f);
            Stretch(dim.rectTransform);

            panelAspect = panelSprite.rect.width / Mathf.Max(1f, panelSprite.rect.height);

            var image = ShellUI.AddImage(root, "Panel", panelSprite, raycast: true);
            panel = image.rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(PopupWidth, PopupWidth / panelAspect);

            var popup = rootGo.AddComponent<PopupPanel>();
            rootGo.SetActive(false);
            return popup;
        }

        /// <summary>판 안에 버튼 한 개. 위치·크기는 판 크기를 1 로 본 비율입니다.</summary>
        static Button MakePopupButton(RectTransform panel, string name, Sprite sprite, Vector2 center,
                                      float panelAspect, Object target, string method)
        {
            var image = ShellUI.AddImage(panel, name, sprite, raycast: true);
            float height = ShellUI.HeightForWidth(sprite, PopupButtonWidth, panelAspect);
            ShellUI.Place(image.rectTransform, center, new Vector2(PopupButtonWidth, height));
            return MakeButton(image, target, method);
        }

        /// <summary>그림에 Button 을 붙이고 누를 때 살짝 어두워지게 합니다.</summary>
        static Button MakeButton(Image image, Object target, string method)
        {
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            button.colors = colors;

            BindClick(button, target, method);
            return button;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>버튼을 누르려면 씬에 EventSystem 이 하나 있어야 합니다.</summary>
        public static void MakeEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();

            // 프로젝트에 이미 있는 입력 설정(UI 맵)을 씁니다. 씬에 에셋 참조로 저장되므로
            // 실행할 때 따로 만들 필요가 없습니다. (없으면 실행 시 기본값이 자동으로 붙습니다)
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputActionsPath);
            if (actions != null) module.actionsAsset = actions;
        }

        static Canvas MakeCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        static RectTransform MakeBackdrop(RectTransform parent, Sprite sprite, AspectFitMode mode)
        {
            var image = ShellUI.AddImage(parent, "Backdrop", sprite);
            var rt = image.rectTransform;

            var fitter = image.gameObject.AddComponent<AspectFitter>();
            WireInts(fitter, ("mode", (int)mode));
            fitter.Aspect = sprite.rect.width / sprite.rect.height;
            fitter.Apply();

            return rt;
        }

        /// <summary>
        /// 그림 왼쪽 테두리에서 색을 하나 뽑습니다. 화면 여백(레터박스)을 그림과 어울리는
        /// 색으로 칠할 때 씁니다. 임포트 설정을 건드리지 않으려고 파일을 직접 읽습니다.
        /// </summary>
        static Color SampleEdgeColor(string assetPath)
        {
            var fallback = new Color(0.10f, 0.12f, 0.14f);
            if (!File.Exists(assetPath)) return fallback;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(assetPath))) return fallback;
                return texture.GetPixel(Mathf.Max(1, texture.width / 200), texture.height / 2);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        static Color Darken(Color c, float amount)
        {
            return new Color(c.r * amount, c.g * amount, c.b * amount, 1f);
        }

        static GameCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory("Assets/Shell");
                catalog = ScriptableObject.CreateInstance<GameCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            // 미니게임 항목이 아직 없으면 한 번만 만들어 줍니다. 이후 수정은 사용자 몫입니다.
            // (칸 위치 slot 이나 이름을 바꾸고 싶으면 GameCatalog.asset 을 인스펙터에서 고치세요)
            if (catalog.FindById("jumpjump") == null)
            {
                catalog.games.Add(new MiniGameEntry
                {
                    id = "jumpjump",
                    displayName = "점프점프!",
                    sceneName = Path.GetFileNameWithoutExtension(GameScenePath),
                    slot = 0,
                    available = true,
                });
            }

            if (catalog.FindById("archery") == null)
            {
                catalog.games.Add(new MiniGameEntry
                {
                    id = "archery",
                    displayName = "활쏘기!",
                    sceneName = Path.GetFileNameWithoutExtension(ArcheryScenePath),
                    slot = 1,
                    available = true,
                });
            }

            // 아이콘과 이름표가 비어 있으면 Art/Games, Art/Names 에서 순서대로 붙여 줍니다.
            // (Game_01_Jump / Name_01_점프점프 -> 첫 번째 게임)
            var icons = ShellArt.GameIconPaths();
            var plates = ShellArt.NamePlatePaths();
            for (int i = 0; i < catalog.games.Count; i++)
            {
                var entry = catalog.games[i];
                if (entry == null) continue;
                if (entry.icon == null && i < icons.Count)
                    entry.icon = AssetDatabase.LoadAssetAtPath<Sprite>(icons[i]);
                if (entry.namePlate == null && i < plates.Count)
                    entry.namePlate = AssetDatabase.LoadAssetAtPath<Sprite>(plates[i]);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        static void RegisterBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
                new EditorBuildSettingsScene(ArcheryScenePath, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ 직렬화 도우미

        public static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var pair in fields)
            {
                var prop = so.FindProperty(pair.field);
                if (prop == null)
                {
                    Debug.LogError("[Arcade] 필드를 찾지 못했습니다: " + target.GetType().Name + "." + pair.field);
                    continue;
                }
                prop.objectReferenceValue = pair.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 숫자 필드와 열거형(enum) 필드를 채웁니다.
        ///
        /// **둘은 넣는 방법이 다릅니다.** 예전에는 전부 열거형 방식(`enumValueIndex`)으로 넣었는데,
        /// 그러면 보통 숫자 필드에는 값이 들어가지 않고 0 으로 남습니다. 그래서 랭킹 창의 탭이
        /// 전부 0번으로 구워져 **어느 이름표를 눌러도 첫 번째 게임만 나왔습니다.** (수정사항_02, 2026-09-11)
        /// </summary>
        public static void WireInts(Object target, params (string field, int value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var pair in fields)
            {
                var prop = so.FindProperty(pair.field);
                if (prop == null)
                {
                    Debug.LogError("[Arcade] 필드를 찾지 못했습니다: " + target.GetType().Name + "." + pair.field);
                    continue;
                }

                if (prop.propertyType == SerializedPropertyType.Enum) prop.enumValueIndex = pair.value;
                else prop.intValue = pair.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>배열 필드를 채웁니다 (랭킹 창의 줄 글자들처럼 개수가 여럿인 것).</summary>
        public static void WireArray(Object target, string field, IList<Object> values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError("[Arcade] 배열 필드를 찾지 못했습니다: " + target.GetType().Name + "." + field);
                return;
            }

            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>글자 필드를 채웁니다.</summary>
        public static void WireText(Object target, params (string field, string value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var pair in fields)
            {
                var prop = so.FindProperty(pair.field);
                if (prop == null)
                {
                    Debug.LogError("[Arcade] 글자 필드를 찾지 못했습니다: " + target.GetType().Name + "." + pair.field);
                    continue;
                }
                prop.stringValue = pair.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>글자 배열 필드를 채웁니다 (랭킹 창의 게임 id 목록).</summary>
        public static void WireStrings(Object target, string field, IList<string> values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError("[Arcade] 글자 배열 필드를 찾지 못했습니다: " + target.GetType().Name + "." + field);
                return;
            }

            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).stringValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>판 안에 버튼 한 개 (바깥에서도 쓰는 판). 위치·크기는 판 크기를 1 로 본 비율입니다.</summary>
        public static Button MakePanelButton(RectTransform panel, string name, Sprite sprite, Vector2 center,
                                             float panelAspect, Object target, string method)
        {
            return MakePopupButton(panel, name, sprite, center, panelAspect, target, method);
        }

        /// <summary>이미 만들어 둔 그림에 버튼을 붙입니다.</summary>
        public static Button MakeButtonOn(Image image, Object target, string method)
        {
            return MakeButton(image, target, method);
        }

        /// <summary>
        /// **크기를 내가 정하는 팝업**입니다. 판 그림을 9-슬라이스로 늘리기 때문에
        /// 세로로 긴 창(랭킹)이든 낮은 창(별명)이든 테두리 두께가 그대로입니다.
        /// 그래서 창을 새로 만들 때마다 그림을 새로 그릴 필요가 없습니다.
        /// </summary>
        public static PopupPanel MakeSlicedPopup(RectTransform canvasRoot, string name, Sprite panelSprite,
                                                 Vector2 sizePixels, out RectTransform panel)
        {
            var popup = MakePopup(canvasRoot, name, panelSprite, out panel, out _);

            var image = panel.GetComponent<Image>();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = PanelSliceScale;
            panel.sizeDelta = sizePixels;
            return popup;
        }

        /// <summary>
        /// 9-슬라이스로 늘릴 때 테두리를 몇 배로 볼지. 판 그림의 테두리가 34px 인데
        /// 그대로 쓰면 1080 폭 화면에서 너무 두껍게 나와서 조금 줄입니다.
        /// </summary>
        const float PanelSliceScale = 0.55f;

        /// <summary>버튼의 OnClick 에 "이 컴포넌트의 이 함수" 를 씬에 저장되는 형태로 걸어 줍니다.</summary>
        static void BindClick(Button button, Object target, string methodName)
        {
            var so = new SerializedObject(button);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            calls.arraySize = 1;

            var call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = methodName;
            call.FindPropertyRelative("m_Mode").enumValueIndex = 1;      // Void
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
