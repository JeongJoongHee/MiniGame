using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace JumpJump.EditorTools
{
    /// <summary>
    /// 메뉴 한 번으로 플레이 가능한 GameScene 을 통째로 만들어 줍니다.
    /// 하이어라키를 손으로 구성할 필요가 없고, 다시 실행하면 씬을 새로 굽습니다.
    /// </summary>
    public static class JumpJumpSceneBuilder
    {
        const string ScenePath = "Assets/JumpJump/Scenes/GameScene.unity";
        const string ConfigPath = "Assets/JumpJump/GameConfig.asset";

        // 픽셀아트는 그린 그대로 보여야 하므로 틴트 없이 흰색입니다.
        static readonly Color BlockColor = Color.white;
        static readonly Color GroundColor = Color.white;
        static readonly Color HudColor = new Color(0.93f, 0.95f, 1.00f);

        [MenuItem("Tools/JumpJump/Build Game Scene", priority = 0)]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (Populate() == null) return;

            Directory.CreateDirectory("Assets/JumpJump/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            Debug.Log("[JumpJump] GameScene 생성 완료 -> " + ScenePath + "   Play 버튼을 누르면 바로 플레이됩니다.");
        }

        /// <summary>
        /// 현재 열려 있는 씬에 게임 오브젝트를 전부 구성하고 GameManager 를 돌려줍니다.
        /// 씬 빌드와 자동 플레이테스트가 같은 코드를 공유하도록 분리해 두었습니다.
        /// </summary>
        public static GameManager Populate()
        {
            JumpJumpArtGenerator.Generate(force: false);          // UI 용 흰색 라운드 스프라이트
            var config = LoadOrCreateConfig();
            JumpJumpArtSetup.Setup(force: false);                 // 픽셀아트 PPU / 피벗 / Point 필터

            var round = AssetDatabase.LoadAssetAtPath<Sprite>(JumpJumpArtGenerator.RoundPath);
            var blockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(JumpJumpArtSetup.BasePath);
            SyncBackdropBands(config);

            var characterPaths = JumpJumpArtSetup.CharacterPaths();
            var characterSprites = new Object[characterPaths.Length];
            for (int i = 0; i < characterPaths.Length; i++)
                characterSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(characterPaths[i]);

            bool hasBackdrop = config.backdropBands != null && config.backdropBands.Length > 0
                               && config.backdropBands[0].sprite != null;

            if (round == null || blockSprite == null || !hasBackdrop || characterSprites.Length == 0)
            {
                Debug.LogError("[JumpJump] 아트 리소스를 찾지 못했습니다.\n" +
                               "  발판   : " + JumpJumpArtSetup.BasePath + "\n" +
                               "  배경   : " + JumpJumpArtSetup.BackgroundFolder + "/BG_*.png\n" +
                               "  캐릭터 : " + JumpJumpArtSetup.CharacterFolder + "/Char_*.png");
                return null;
            }

            // ---------- 카메라 ----------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, config.cameraPlayerOffset, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = config.orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.10f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            if (camGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var cameraRig = camGo.AddComponent<CameraRig>();

            // ---------- 배경 (세로/가로 무한 반복 + 높이별 구간 전환) ----------
            // 두 겹으로 깔아 두고, 구간 경계에서 위 겹(next)의 투명도를 올려 서서히 갈아탑니다.
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.position = new Vector3(0f, 0f, 10f);
            var backdropCurrent = MakeBackdropLayer(backdrop.transform, "Current", -100,
                                                    config.backdropBands[0].sprite);
            var backdropNext = MakeBackdropLayer(backdrop.transform, "Next", -99, null);
            var backdropTiler = backdrop.AddComponent<BackdropTiler>();

            // ---------- 발판 ----------
            var platformsGo = new GameObject("Platforms");
            var platforms = platformsGo.AddComponent<PlatformManager>();

            // ---------- 플레이어 ----------
            var playerGo = new GameObject("Player");
            var player = playerGo.AddComponent<PlayerController>();

            // 스프라이트 피벗이 발바닥이라, Visual 을 몸 중심에서 반 칸 내려 두면
            // 발이 정확히 발판 윗면에 닿고 스쿼시도 발 기준으로 걸립니다.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(playerGo.transform, false);
            visual.transform.localPosition = new Vector3(0f, -config.playerSize.y * 0.5f, 0f);
            var characterSr = visual.AddComponent<SpriteRenderer>();
            characterSr.sprite = (Sprite)characterSprites[0];
            characterSr.sortingOrder = 10;
            var characterAnimator = visual.AddComponent<CharacterAnimator>();

            // ---------- HUD ----------
            var hud = BuildHud(round);

            // LOBBY 버튼을 누르려면 씬에 EventSystem 이 하나 있어야 합니다.
            Arcade.EditorTools.ShellSceneBuilder.MakeEventSystem();

            // ---------- 게임 매니저 ----------
            var gameGo = new GameObject("GameManager");
            var game = gameGo.AddComponent<GameManager>();

            // ---------- 참조 연결 ----------
            Wire(cameraRig, ("config", (Object)config), ("target", playerGo.transform));
            Wire(platforms, ("config", (Object)config), ("blockSprite", blockSprite));
            WireColor(platforms, ("blockColor", BlockColor), ("groundColor", GroundColor));
            Wire(backdropTiler, ("config", (Object)config), ("cameraTransform", camGo.transform),
                                ("current", backdropCurrent), ("next", backdropNext));
            Wire(characterAnimator, ("config", (Object)config), ("spriteRenderer", characterSr));
            WireArray(characterAnimator, "characterSprites", characterSprites);
            Wire(player, ("config", (Object)config), ("platforms", platforms), ("cameraRig", cameraRig), ("characterAnimator", characterAnimator));
            Wire(game, ("config", (Object)config), ("player", player), ("platforms", platforms), ("cameraRig", cameraRig), ("backdrop", backdropTiler), ("hud", hud));

            return game;
        }

        /// <summary>배경 한 겹. 두 겹을 겹쳐 두고 투명도로 갈아탑니다.</summary>
        static SpriteRenderer MakeBackdropLayer(Transform parent, string name, int order, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.sortingOrder = order;
            sr.enabled = sprite != null;
            return sr;
        }

        /// <summary>
        /// Art/Background 의 BG_*.png 를 파일 이름 순서대로 GameConfig.backdropBands 에 채웁니다.
        /// 그림 장수가 그대로면 사용자가 인스펙터에서 고친 전환 높이(fromMeters)는 건드리지 않고,
        /// 장수가 바뀌었을 때만 기본값(0 / 200 / 500 / 1000 ...)으로 다시 잡습니다.
        /// </summary>
        static void SyncBackdropBands(GameConfig config)
        {
            var paths = JumpJumpArtSetup.BackgroundPaths();
            if (paths.Length == 0)
            {
                Debug.LogError("[JumpJump] 배경 그림이 없습니다 -> " +
                               JumpJumpArtSetup.BackgroundFolder + "/BG_*.png");
                return;
            }

            var old = config.backdropBands;
            bool sameCount = old != null && old.Length == paths.Length;
            var bands = new BackdropBand[paths.Length];

            // 장수가 바뀌었을 때 쓸 기본 전환 높이. config.csv 의 구간 경계와 맞춰 두었습니다.
            float[] defaults = { 0f, 200f, 500f, 1000f, 1500f, 2000f, 2500f, 3000f };

            for (int i = 0; i < paths.Length; i++)
            {
                bands[i] = new BackdropBand
                {
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]),
                    fromMeters = sameCount ? old[i].fromMeters
                               : (i < defaults.Length ? defaults[i] : defaults[defaults.Length - 1] + i * 500f)
                };
            }

            config.backdropBands = bands;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            var sb = new System.Text.StringBuilder("[JumpJump] 높이별 배경 " + bands.Length + "장\n");
            for (int i = 0; i < bands.Length; i++)
                sb.Append($"  {bands[i].fromMeters,6:0} m 부터   {Path.GetFileName(paths[i])}\n");
            sb.Append($"  구간 경계 앞 {config.backdropBlendMeters:0} m 에 걸쳐 서서히 넘어갑니다.");
            Debug.Log(sb.ToString());
        }

        static GameConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory("Assets/JumpJump");
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            // 바깥 작업 폴더의 config.csv 가 더 새로우면 가져온 뒤 GameConfig 에 연결합니다.
            JumpJumpConfigCsv.Sync();
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(JumpJumpConfigCsv.AssetPath);
            if (csv != null && config.difficultyCsv != csv)
            {
                config.difficultyCsv = csv;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            config.InvalidateBands();   // 파일을 고쳤을 수 있으므로 다시 읽게 합니다
            return config;
        }

        static HudController BuildHud(Sprite round)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = canvasGo.AddComponent<HudController>();
            var root = canvasGo.GetComponent<RectTransform>();

            // Score : 좌상단
            var score = MakeText(root, "ScoreText", 42, TextAnchor.MiddleLeft);
            Anchor(score.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                   new Vector2(40f, -80f), new Vector2(620f, 60f));

            // Height : 우상단. LOBBY 버튼이 오른쪽 맨 위를 차지하므로 한 줄 아래입니다.
            var height = MakeText(root, "HeightText", 42, TextAnchor.MiddleRight);
            Anchor(height.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                   new Vector2(-40f, -175f), new Vector2(620f, 60f));

            // 타이머 바 : 점수와 높이 아래, 게이지가 우측에서 좌측으로 줄어듭니다.
            var barBg = new GameObject("TimerBar", typeof(Image)).GetComponent<Image>();
            barBg.transform.SetParent(root, false);
            barBg.sprite = round;
            barBg.type = Image.Type.Sliced;
            barBg.color = new Color(1f, 1f, 1f, 0.14f);
            barBg.raycastTarget = false;
            Anchor(barBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                   new Vector2(0f, -255f), new Vector2(960f, 34f));

            var fill = new GameObject("TimerFill", typeof(Image)).GetComponent<Image>();
            fill.transform.SetParent(barBg.transform, false);
            fill.sprite = round;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            Stretch(fill.rectTransform, 4f);

            // 콤보 표시
            var combo = MakeText(root, "ComboText", 34, TextAnchor.MiddleCenter);
            combo.color = new Color(1f, 0.84f, 0.36f);
            Anchor(combo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                   new Vector2(0f, -310f), new Vector2(900f, 46f));

            // 오버레이 : 시작 안내 / 게임 오버 결과
            var overlay = new GameObject("Overlay", typeof(Image));
            overlay.transform.SetParent(root, false);
            var overlayImg = overlay.GetComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.62f);
            overlayImg.raycastTarget = false;
            var overlayRect = overlay.GetComponent<RectTransform>();
            Stretch(overlayRect, 0f);

            var title = MakeText(overlayRect, "TitleText", 88, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, 300f), new Vector2(1000f, 120f));

            var bodyText = MakeText(overlayRect, "BodyText", 44, TextAnchor.UpperCenter);
            bodyText.color = new Color(0.80f, 0.86f, 1f);
            bodyText.lineSpacing = 1.25f;
            Anchor(bodyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                   new Vector2(0f, 190f), new Vector2(1000f, 460f));

            var hint = MakeText(overlayRect, "HintText", 50, TextAnchor.MiddleCenter);
            hint.color = new Color(1f, 0.84f, 0.36f);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, -320f), new Vector2(1000f, 80f));

            // 로비로 돌아가는 길 : 화면 오른쪽 맨 위 화살표 + "로비로 나가시겠습니까?" 팝업.
            // 오버레이보다 나중에 만들어야 어두운 막 위에 올라오고 터치도 먹습니다.
            Arcade.EditorTools.ShellSceneBuilder.MakeExitToLobbyUI(root);

            Wire(hud,
                ("scoreText", score), ("heightText", height), ("comboText", combo),
                ("timerFill", fill), ("overlay", overlay),
                ("titleText", title), ("bodyText", bodyText), ("hintText", hint));

            return hud;
        }

        static Text MakeText(RectTransform parent, string name, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            // 글꼴은 껍데기가 정합니다. 작업 폴더에 @Font.ttf 를 놓으면 그것으로 바뀝니다.
            text.font = Arcade.ShellUI.GameFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = HudColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = "";
            return text;
        }

        static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static void Stretch(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var pair in fields)
            {
                var prop = so.FindProperty(pair.field);
                if (prop == null)
                {
                    Debug.LogError("[JumpJump] 필드를 찾지 못했습니다: " + target.GetType().Name + "." + pair.field);
                    continue;
                }
                prop.objectReferenceValue = pair.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogError("[JumpJump] 배열 필드를 찾지 못했습니다: " + target.GetType().Name + "." + field);
                return;
            }

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireColor(Object target, params (string field, Color value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var pair in fields)
            {
                var prop = so.FindProperty(pair.field);
                if (prop == null) continue;
                prop.colorValue = pair.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
