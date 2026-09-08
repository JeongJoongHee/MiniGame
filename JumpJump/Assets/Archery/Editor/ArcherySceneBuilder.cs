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
        static readonly Color RailColor = new Color(0.60f, 0.66f, 0.76f, 0.9f);

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

            // ---------- 하늘 ----------
            // 8x512 짜리 그라데이션 한 장을 화면보다 넉넉하게 늘려서 깝니다.
            var sky = new GameObject("Sky").AddComponent<SpriteRenderer>();
            sky.sprite = skySprite;
            sky.sortingOrder = -100;
            sky.transform.position = new Vector3(0f, 0f, 5f);
            var skySize = skySprite.bounds.size;
            sky.transform.localScale = new Vector3(24f / skySize.x, (config.orthoSize * 2.2f) / skySize.y, 1f);

            // ---------- 과녁이 미끄러지는 레일 ----------
            var rail = new GameObject("Rail").AddComponent<SpriteRenderer>();
            rail.sprite = round;
            rail.drawMode = SpriteDrawMode.Sliced;
            rail.size = new Vector2(config.playHalfWidth * 2f + 1.6f, 0.16f);
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
            scorePanel.color = new Color(1f, 1f, 1f, 0.72f);
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

            // 과녁 크기 : 좌상단
            var size = MakeText(root, "SizeText", 40, TextAnchor.MiddleLeft);
            Anchor(size.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                   new Vector2(44f, -92f), new Vector2(460f, 60f));

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

            var ammo = MakeText(root, "AmmoText", 38, TextAnchor.MiddleLeft);
            Anchor(ammo.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                   new Vector2(48f, 170f), new Vector2(420f, 54f));

            // 맞힌 자리에 잠깐 뜨는 "+5 PT"
            var popup = MakeText(root, "HitPopup", 66, TextAnchor.MiddleCenter);
            popup.color = new Color(0.94f, 0.42f, 0.24f);
            Anchor(popup.rectTransform, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f),
                   Vector2.zero, new Vector2(340f, 90f));
            popup.gameObject.SetActive(false);

            // 오버레이 : 시작 안내 / 결과
            var overlay = new GameObject("Overlay", typeof(Image));
            overlay.transform.SetParent(root, false);
            var overlayImg = overlay.GetComponent<Image>();
            overlayImg.color = new Color(0.06f, 0.10f, 0.18f, 0.62f);
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

        static Text MakeText(RectTransform parent, string name, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
