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

        [MenuItem("Tools/Arcade/Build All Scenes", priority = 0)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

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

            screen.Apply();
            return screen;
        }

        // ------------------------------------------------------------------ 로비

        public static LobbyScreen PopulateLobby()
        {
            ShellArt.Sync(force: false);
            var catalog = LoadOrCreateCatalog();

            var lobbySprite = ShellArt.Load(ShellArt.LobbyPath);
            var playSprite = ShellArt.Load(ShellArt.PlayButtonPath);
            if (lobbySprite == null || playSprite == null) return null;

            // 로비는 칸이 잘리면 안 되므로 그림 전체가 보이게 넣고,
            // 남는 위아래 여백은 배경 테두리 색을 어둡게 깔아 액자처럼 보이게 합니다.
            MakeCamera(Darken(SampleEdgeColor(ShellArt.LobbyPath), 0.25f));
            MakeEventSystem();

            var canvas = MakeCanvas("LobbyCanvas");
            var root = canvas.GetComponent<RectTransform>();
            var backdrop = MakeBackdrop(root, lobbySprite, AspectFitMode.Contain);

            var screenGo = new GameObject("LobbyScreen");
            var screen = screenGo.AddComponent<LobbyScreen>();

            // 이름표는 게임마다 다른 그림이라 카탈로그에 들어 있습니다.
            // 여기서는 PLAY 버튼과, 이름표 그림이 아직 없는 게임에 깔릴 빈 이름표를 넘깁니다.
            Wire(screen,
                ("catalog", catalog),
                ("backdrop", backdrop),
                ("fallbackNamePlate", ShellArt.Load(ShellArt.BlankPlatePath)),
                ("playButton", playSprite));

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
        /// 미니게임 화면 오른쪽 위에 붙는 "&lt; LOBBY" 버튼입니다.
        /// 게임마다 따로 만들지 않고 여기 한 곳에서 만들어, 어느 게임에서나
        /// **같은 자리·같은 모양**이 되게 합니다. 게임 중에도 항상 보입니다.
        ///
        /// 오버레이(시작 안내 / 게임 오버)보다 나중에 만들어야 어두운 막 위로 올라옵니다.
        /// 이 버튼을 누른 것이 점프·발사로 세지 않는 것은 각 게임의 `TapInput.OverUI` 가 막아 줍니다.
        /// </summary>
        /// <param name="root">HUD 캔버스의 RectTransform</param>
        /// <param name="round">9-slice 라운드 사각형 스프라이트 (게임이 이미 쓰고 있는 것)</param>
        /// <param name="target">`OnLobbyPressed()` 를 가진 컴포넌트 (각 게임의 HUD)</param>
        public static Button MakeLobbyButton(RectTransform root, Sprite round, Object target)
        {
            var go = new GameObject("LobbyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);
            go.transform.SetAsLastSibling();

            var image = go.GetComponent<Image>();
            image.sprite = round;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.08f, 0.10f, 0.16f, 0.55f);
            image.raycastTarget = true;

            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(232f, 92f);
            rt.anchoredPosition = new Vector2(-36f, -56f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(rt, false);

            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 40;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = "< LOBBY";

            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            BindClick(button, target, "OnLobbyPressed");
            return button;
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
            Wire(fitter, ("mode", (int)mode));
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

        static void Wire(Object target, params (string field, Object value)[] fields)
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

        static void Wire(Object target, params (string field, int value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var pair in fields)
            {
                var prop = so.FindProperty(pair.field);
                if (prop == null) continue;
                prop.enumValueIndex = pair.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

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
