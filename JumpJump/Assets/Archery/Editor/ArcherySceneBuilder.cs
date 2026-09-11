using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Archery.EditorTools
{
    /// <summary>
    /// 메뉴 한 번으로 플레이 가능한 ArcheryScene 을 통째로 만들어 줍니다.
    /// 하이어라키를 손으로 구성할 필요가 없고, 다시 실행하면 씬을 새로 굽습니다.
    ///
    /// **씬을 직접 편집하지 마세요.** 여기서 다시 구우면 전부 덮어써집니다.
    /// 화면 구성을 바꾸려면 이 파일을, 숫자만 바꾸려면 ArcheryConfig.asset 을 고치세요.
    /// (난이도 수치는 archery.csv 가 원본입니다)
    /// </summary>
    public static class ArcherySceneBuilder
    {
        public const string ScenePath = "Assets/Archery/Scenes/ArcheryScene.unity";
        const string ConfigPath = ArcheryStageCsv.ConfigPath;

        static readonly Color HudColor = new Color(0.13f, 0.18f, 0.30f);

        // --- 배경(잔디밭 그림, 2026-09-11)에 맞춘 UI 투명도 ----------------------------------
        // 예전 배경은 밝은 하늘색 그라데이션이라 글자를 그냥 얹어도 읽혔지만, 잔디밭 그림은
        // 위쪽이 어두운 숲이고 아래쪽이 꽃·통나무라 글자가 묻힙니다. 그래서 글자마다 반투명 판을 깔고,
        // 시작·결과 화면의 어두운 막을 짙게, 레일은 운동장 흰 선처럼 반투명 흰색으로 바꿨습니다.
        // (이 프로젝트는 Linear 색 공간이라 같은 알파라도 눈에는 옅게 보입니다 — 팝업 막 0.70 과 같은 이유)

        /// <summary>과녁이 미끄러지는 레일. 운동장에 그어진 흰 선과 어울리게 반투명 흰색.</summary>
        static readonly Color RailColor = new Color(1f, 1f, 1f, 0.6f);

        /// <summary>점수판 · 과녁 크기 · 남은 화살 뒤에 까는 판. 셋 다 같은 판이라 화면이 한 벌로 보입니다.</summary>
        static readonly Color ChipColor = new Color(1f, 1f, 1f, 0.66f);

        /// <summary>시작 안내 / 결과 화면의 막. 밝은 잔디 위에서 흰 글자가 읽히려면 짙어야 합니다 (예전 0.62).</summary>
        static readonly Color OverlayColor = new Color(0.04f, 0.07f, 0.06f, 0.78f);

        [MenuItem("Tools/Archery/Build Archery Scene", priority = 0)]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (Populate() == null) return;

            Directory.CreateDirectory("Assets/Archery/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            Debug.Log("[Archery] ArcheryScene 생성 완료 -> " + ScenePath + "   Play 버튼을 누르면 바로 플레이됩니다.");
        }

        /// <summary>
        /// 현재 열려 있는 씬에 게임 오브젝트를 전부 구성하고 ArcheryGame 을 돌려줍니다.
        /// 씬 빌드 / 자동 플레이테스트 / 미리보기가 같은 코드를 공유합니다.
        /// </summary>
        public static ArcheryGame Populate()
        {
            ArcheryArtGenerator.Generate(force: false);   // 없는 그림만 임시로 만들어 둡니다
            var config = LoadOrCreateConfig();
            ArcheryArtSetup.Setup(force: false);          // 그림을 갈아 끼워도 크기가 유지되도록

            var targetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArcheryArtGenerator.TargetPath);
            var arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArcheryArtGenerator.ArrowPath);
            var bowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArcheryArtGenerator.BowPath);
            var skySprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArcheryArtGenerator.SkyPath);
            var round = AssetDatabase.LoadAssetAtPath<Sprite>(ArcheryArtGenerator.RoundPath);

            if (targetSprite == null || arrowSprite == null || bowSprite == null || skySprite == null || round == null)
            {
                Debug.LogError("[Archery] 그림을 찾지 못했습니다. Tools > Archery > Regenerate Placeholder Art 를 눌러 보세요.\n" +
                               "  " + ArcheryArtGenerator.ArtFolder);
                return null;
            }

            // ---------- 카메라 ----------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = config.orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.92f, 0.98f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            if (camGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            // ---------- 배경 (Art/Sky.png) ----------
            var sky = new GameObject("Sky").AddComponent<SpriteRenderer>();
            sky.sprite = skySprite;
            sky.sortingOrder = -100;
            sky.transform.position = new Vector3(0f, 0f, 5f);
            sky.transform.localScale = SkyScale(skySprite.bounds.size, config.orthoSize);

            // ---------- 과녁이 미끄러지는 레일 ----------
            // 레일 양 끝 = 과녁이 되돌아오는 자리입니다. 과녁은 가장자리가 playHalfWidth 에 닿으면 튕기므로
            // 레일도 딱 그 폭으로 그립니다. (예전에는 양쪽으로 0.8 씩 더 길어서, 과녁이 선 끝까지
            //  가지 않고 중간에서 돌아오는 것처럼 보였습니다 — 수정사항_03)
            var rail = new GameObject("Rail").AddComponent<SpriteRenderer>();
            rail.sprite = round;
            rail.drawMode = SpriteDrawMode.Sliced;
            rail.size = new Vector2(config.playHalfWidth * 2f, 0.16f);
            rail.color = RailColor;
            rail.sortingOrder = -10;
            rail.transform.position = new Vector3(0f, config.targetY, 0f);

            // ---------- 과녁 ----------
            var targetGo = new GameObject("Target");
            targetGo.transform.position = new Vector3(0f, config.targetY, 0f);
            var targetSr = targetGo.AddComponent<SpriteRenderer>();
            targetSr.sprite = targetSprite;
            targetSr.sortingOrder = 10;
            var target = targetGo.AddComponent<TargetController>();

            // ---------- 활 ----------
            var bowGo = new GameObject("Bow");
            bowGo.transform.position = new Vector3(0f, config.bowY, 0f);
            var bow = bowGo.AddComponent<BowController>();

            // 반동으로 움직이는 것은 Body 뿐입니다. 뿌리(Bow)는 제자리에 고정입니다.
            var bowBody = new GameObject("Body");
            bowBody.transform.SetParent(bowGo.transform, false);
            var bowSr = bowBody.AddComponent<SpriteRenderer>();
            bowSr.sprite = bowSprite;
            bowSr.sortingOrder = 20;

            // 시위에 걸린 화살. 쏠 화살이 남아 있을 때만 보이고, 활 앞에 그려집니다.
            var nocked = new GameObject("NockedArrow");
            nocked.transform.SetParent(bowBody.transform, false);
            nocked.transform.localPosition = new Vector3(0f, config.arrowStartY - config.bowY, 0f);
            var nockedSr = nocked.AddComponent<SpriteRenderer>();
            nockedSr.sprite = arrowSprite;
            nockedSr.sortingOrder = 25;

            // ---------- 화살 ----------
            var arrowsGo = new GameObject("Arrows");
            var arrows = arrowsGo.AddComponent<ArrowPool>();

            var arrow0 = new GameObject("Arrow_00");
            arrow0.transform.SetParent(arrowsGo.transform, false);
            var arrow0Sr = arrow0.AddComponent<SpriteRenderer>();
            arrow0Sr.sprite = arrowSprite;
            arrow0Sr.sortingOrder = 8;
            arrow0.SetActive(false);   // 풀이 복제해서 쓸 원본입니다

            // ---------- HUD ----------
            var hud = BuildHud(round, arrowSprite);

            // 오버레이의 "LOBBY" 버튼을 누르려면 씬에 EventSystem 이 하나 있어야 합니다.
            Arcade.EditorTools.ShellSceneBuilder.MakeEventSystem();

            // ---------- 게임 매니저 ----------
            var gameGo = new GameObject("ArcheryGame");
            var game = gameGo.AddComponent<ArcheryGame>();

            // ---------- 참조 연결 ----------
            Wire(target, ("config", (Object)config), ("spriteRenderer", targetSr));
            Wire(arrows, ("config", (Object)config), ("arrowPrefabRenderer", arrow0Sr));
            Wire(bow, ("config", (Object)config), ("body", bowBody.transform), ("nockedArrow", nocked));
            Wire(game, ("config", (Object)config), ("target", target), ("arrows", arrows),
                       ("bow", bow), ("hud", hud));

            return game;
        }

        static ArcheryConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArcheryConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory("Assets/Archery");
                config = ScriptableObject.CreateInstance<ArcheryConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            // 작업 폴더의 archery.csv 가 더 새로우면 가져온 뒤 ArcheryConfig 에 연결합니다.
            ArcheryStageCsv.Sync();

            // 화면에 나오는 글자표도 여기서 같이 가져옵니다. (점프점프 쪽과 같은 이유)
            Arcade.EditorTools.ShellStringsCsv.Sync();
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(ArcheryStageCsv.AssetPath);
            if (csv != null && config.stageCsv != csv)
            {
                config.stageCsv = csv;
                EditorUtility.SetDirty(config);
            }

            config.InvalidateBands();   // 파일을 고쳤을 수 있으므로 다시 읽게 합니다

            // 읽은 결과를 인스펙터 목록에 구워 둡니다 (눈으로 확인 + CSV 가 깨졌을 때의 예비값).
            if (csv != null)
            {
                var bands = StageTable.Parse(csv.text, out string report);
                if (bands != null)
                {
                    config.stageBands = bands;
                    EditorUtility.SetDirty(config);

                    if (!string.IsNullOrEmpty(report))
                        Debug.LogWarning("[Archery] archery.csv 를 읽었지만 손본 부분이 있습니다.\n" + report);

                    Debug.Log($"[Archery] 난이도 구간표 {bands.Length}개 구간 (출처: {csv.name}.csv)\n"
                              + StageTable.Describe(bands));
                }
                else
                {
                    Debug.LogError("[Archery] archery.csv 를 읽지 못했습니다.\n  " + report);
                }
            }

            AssetDatabase.SaveAssets();
            return config;
        }

        // ------------------------------------------------------------------ HUD

        static ArcheryHud BuildHud(Sprite round, Sprite arrowSprite)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = canvasGo.AddComponent<ArcheryHud>();
            var root = canvasGo.GetComponent<RectTransform>();

            // 점수 / 최고 점수 : 우상단. 오른쪽 맨 위는 LOBBY 버튼이 차지하므로 한 줄 아래입니다.
            var scorePanel = new GameObject("ScorePanel", typeof(Image)).GetComponent<Image>();
            scorePanel.transform.SetParent(root, false);
            scorePanel.sprite = round;
            scorePanel.type = Image.Type.Sliced;
            scorePanel.color = ChipColor;
            scorePanel.raycastTarget = false;
            Anchor(scorePanel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                   new Vector2(-40f, -168f), new Vector2(560f, 150f));

            var score = MakeText(scorePanel.rectTransform, "ScoreText", 44, TextAnchor.MiddleRight);
            Anchor(score.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                   new Vector2(-24f, -6f), new Vector2(-48f, -18f));

            var best = MakeText(scorePanel.rectTransform, "BestText", 34, TextAnchor.MiddleRight);
            best.color = new Color(0.34f, 0.40f, 0.55f);
            Anchor(best.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-24f, 6f), new Vector2(-48f, -18f));

            // 과녁 크기 : 좌상단. 뒤쪽이 어두운 숲이라 판을 깔고 그 위에 씁니다.
            var sizeChip = MakeChip(root, "SizeChip", round, new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(360f, 76f));
            var size = MakeText(sizeChip, "SizeText", 40, TextAnchor.MiddleLeft);
            Stretch(size.rectTransform, 22f);

            // 남은 화살 : 좌하단. 아이콘 줄 + 숫자
            var ammoIcons = new Object[12];
            for (int i = 0; i < ammoIcons.Length; i++)
            {
                var icon = new GameObject("Ammo_" + i.ToString("00"), typeof(Image)).GetComponent<Image>();
                icon.transform.SetParent(root, false);
                icon.sprite = arrowSprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Anchor(icon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                       new Vector2(48f + i * 58f, 250f), new Vector2(44f, 190f));
                ammoIcons[i] = icon;
            }

            // 남은 화살 숫자 : 뒤쪽이 꽃밭·통나무라 판을 깔고 그 위에 씁니다.
            var ammoChip = MakeChip(root, "AmmoChip", round, new Vector2(0f, 0f), new Vector2(30f, 160f), new Vector2(330f, 72f));
            var ammo = MakeText(ammoChip, "AmmoText", 38, TextAnchor.MiddleLeft);
            Stretch(ammo.rectTransform, 22f);

            // 맞힌 자리에 잠깐 뜨는 "+5 PT". 잔디 위에서도 보이게 짙은 테두리를 두릅니다.
            var popup = MakeText(root, "HitPopup", 66, TextAnchor.MiddleCenter);
            popup.color = new Color(1f, 0.55f, 0.24f);
            Arcade.ShellUI.AddOutline(popup, 3f, new Color(0.18f, 0.08f, 0.02f, 0.9f));
            Anchor(popup.rectTransform, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f),
                   Vector2.zero, new Vector2(340f, 90f));
            popup.gameObject.SetActive(false);

            // 오버레이 : 시작 안내 / 결과
            var overlay = new GameObject("Overlay", typeof(Image));
            overlay.transform.SetParent(root, false);
            var overlayImg = overlay.GetComponent<Image>();
            overlayImg.color = OverlayColor;
            overlayImg.raycastTarget = false;
            var overlayRect = overlay.GetComponent<RectTransform>();
            Stretch(overlayRect, 0f);

            var title = MakeText(overlayRect, "TitleText", 88, TextAnchor.MiddleCenter);
            title.color = Color.white;
            Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, 300f), new Vector2(1000f, 120f));

            var body = MakeText(overlayRect, "BodyText", 42, TextAnchor.UpperCenter);
            body.color = new Color(0.86f, 0.92f, 1f);
            body.lineSpacing = 1.25f;
            Anchor(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                   new Vector2(0f, 190f), new Vector2(1000f, 460f));

            var hint = MakeText(overlayRect, "HintText", 50, TextAnchor.MiddleCenter);
            hint.color = new Color(1f, 0.84f, 0.36f);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, -320f), new Vector2(1000f, 80f));

            // 로비로 돌아가는 길 : 화면 오른쪽 맨 위 화살표 + "로비로 나가시겠습니까?" 팝업.
            // 껍데기가 만들어 주므로 점프점프와 자리·모양이 똑같습니다.
            Arcade.EditorTools.ShellSceneBuilder.MakeExitToLobbyUI(root);

            Wire(hud,
                ("scoreText", score), ("bestText", best), ("sizeText", size),
                ("ammoText", ammo), ("hitPopup", popup),
                ("overlay", overlay), ("titleText", title), ("bodyText", body), ("hintText", hint));
            WireArray(hud, "ammoIcons", ammoIcons);

            return hud;
        }

        /// <summary>
        /// 배경 그림의 크기를 정합니다.
        ///  - **진짜 그림**(2026-09-11 부터 잔디밭)은 비율을 지킨 채 **화면 위아래를 꽉 채웁니다.**
        ///    세로로 가장 넓은 폰(9:16)에서도 좌우가 비지 않는지 같이 보고, 남는 좌우는 잘립니다 (20:9 폰은 양옆이 조금 잘림).
        ///    예전처럼 가로세로를 따로 늘리면 그림이 옆으로 두 배쯤 퍼져 보입니다.
        ///  - 가느다란 그라데이션 띠(임시 그림, 8x512)는 예전처럼 화면보다 넉넉하게 늘립니다.
        /// </summary>
        static Vector3 SkyScale(Vector3 size, float orthoSize)
        {
            if (size.x <= 0f || size.y <= 0f) return Vector3.one;

            float screenHeight = orthoSize * 2f;
            if (size.x / size.y < 0.2f)
                return new Vector3(24f / size.x, (screenHeight * 1.1f) / size.y, 1f);

            float widestPortraitWidth = screenHeight * 9f / 16f;
            float scale = Mathf.Max(screenHeight / size.y, widestPortraitWidth / size.x);
            return new Vector3(scale, scale, 1f);
        }

        /// <summary>HUD 글자 뒤에 까는 반투명 판 하나 (점수판과 같은 모양 · 같은 투명도).</summary>
        static RectTransform MakeChip(RectTransform parent, string name, Sprite round, Vector2 corner, Vector2 pos, Vector2 size)
        {
            var chip = new GameObject(name, typeof(Image)).GetComponent<Image>();
            chip.transform.SetParent(parent, false);
            chip.sprite = round;
            chip.type = Image.Type.Sliced;
            chip.color = ChipColor;
            chip.raycastTarget = false;
            Anchor(chip.rectTransform, corner, corner, corner, pos, size);
            return chip.rectTransform;
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

        // ------------------------------------------------------------------ 직렬화 도우미

        static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var pair in fields)
            {
                var prop = so.FindProperty(pair.field);
                if (prop == null)
                {
                    Debug.LogError("[Archery] 필드를 찾지 못했습니다: " + target.GetType().Name + "." + pair.field);
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
                Debug.LogError("[Archery] 배열 필드를 찾지 못했습니다: " + target.GetType().Name + "." + field);
                return;
            }

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
