using System.Collections.Generic;
using System.IO;
using System.Text;
using Arcade;
using Arcade.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Enchant.EditorTools
{
    /// <summary>
    /// 메뉴 한 번으로 플레이 가능한 EnchantScene 을 통째로 만들어 줍니다.
    /// 다시 실행하면 씬을 새로 굽습니다.
    ///
    /// **씬을 직접 편집하지 마세요.** 여기서 다시 구우면 전부 덮어써집니다.
    /// 화면 구성을 바꾸려면 이 파일을, 숫자만 바꾸려면 EnchantConfig.asset 을 고치세요.
    /// (강화 확률 · 분해 보상 · 검 그림 이름은 작업 폴더의 @Enchant_Sword.csv 가 원본입니다)
    ///
    /// 이 게임은 **화면 전체가 UI** 입니다 (월드에 움직이는 물체가 없습니다).
    /// 자리는 1080x1920 기준 픽셀이고, 폰 해상도가 달라도 CanvasScaler 가 같이 늘려 줍니다.
    /// 위쪽 것은 화면 위에, 아래쪽 것은 화면 아래에 붙고, **검 칸은 그 사이를 세로로 채웁니다** —
    /// 그래서 세로로 긴 폰에서는 검이 더 크게 나옵니다.
    /// </summary>
    public static class EnchantSceneBuilder
    {
        public const string ScenePath = "Assets/Enchant/Scenes/EnchantScene.unity";

        // --- 자리 (1080x1920 기준 픽셀) -----------------------------------------------------

        /// <summary>왼쪽 위 강화 수치 동그라미의 지름.</summary>
        const float BadgeSize = 250f;
        /// <summary>검 칸의 폭. 세로는 위 · 아래 여백 사이를 꽉 채웁니다.</summary>
        const float SwordWidth = 560f;
        /// <summary>검 칸 아래끝 (화면 아래에서) / 위끝 (화면 위에서).</summary>
        const float SwordBottom = 640f;
        const float SwordTop = 340f;
        /// <summary>검 뒤의 빛 지름.</summary>
        const float GlowSize = 1000f;

        /// <summary>
        /// [일반 강화] / [안전 강화] : 화면 아래 양쪽. **폭은 화면 절반씩**(바깥 여백 · 가운데 틈을 뺀 만큼)이라
        /// 세로로 긴 폰(화면 폭이 좁아지는)에서도 두 버튼이 겹치지 않습니다.
        /// </summary>
        const float BigButtonHeight = 240f;
        const float BigButtonY = 60f;
        const float BigButtonSide = 36f;
        const float BigButtonGap = 28f;
        /// <summary>[분해] : 두 버튼 사이 위쪽 (기획서 손그림의 자리).</summary>
        static readonly Vector2 MeltButtonSize = new Vector2(400f, 170f);
        const float MeltButtonY = 340f;
        /// <summary>"+14 강화 성공 확률 90%" 줄의 높이.</summary>
        const float ChanceY = 548f;

        /// <summary>버튼 판 그림의 한 점이 화면에서 몇 픽셀이 되는지. 클수록 테두리가 굵습니다.</summary>
        const float ButtonPixel = 6f;

        static readonly Vector2 MeltPopupSize = new Vector2(900f, 640f);

        // --- 색 ----------------------------------------------------------------------------

        static readonly Color NormalColor = new Color(0.90f, 0.40f, 0.22f);
        static readonly Color SafeColor = new Color(0.27f, 0.56f, 0.92f);
        static readonly Color MeltColor = new Color(0.58f, 0.40f, 0.82f);
        static readonly Color NewSwordColor = new Color(0.32f, 0.70f, 0.36f);
        static readonly Color DisabledColor = new Color(0.36f, 0.34f, 0.34f, 1f);
        static readonly Color LabelColor = Color.white;
        static readonly Color SubColor = new Color(1f, 0.94f, 0.82f);
        static readonly Color Shadow = new Color(0.10f, 0.05f, 0.04f, 0.9f);
        static readonly Color Ink = new Color(0.231f, 0.106f, 0.078f);

        [MenuItem("Tools/Enchant/Build Enchant Scene", priority = 0)]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (Populate() == null) return;

            Directory.CreateDirectory("Assets/Enchant/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            Debug.Log("[Enchant] EnchantScene 생성 완료 -> " + ScenePath);
        }

        /// <summary>
        /// 현재 열려 있는 씬에 게임을 전부 구성하고 EnchantGame 을 돌려줍니다.
        /// 씬 빌드 / 자동 플레이테스트 / 미리보기가 같은 코드를 씁니다.
        /// </summary>
        public static EnchantGame Populate()
        {
            var config = LoadOrCreateConfig();
            var imageNames = EnchantArt.ImageNames(config);
            EnchantArt.Prepare(imageNames);

            var background = AssetDatabase.LoadAssetAtPath<Sprite>(EnchantArt.BackgroundPath);
            var badge = AssetDatabase.LoadAssetAtPath<Sprite>(EnchantArt.BadgePath);
            var buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnchantArt.ButtonPath);
            var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnchantArt.GlowPath);
            if (background == null || badge == null || buttonSprite == null || glowSprite == null)
            {
                Debug.LogError("[Enchant] 그림을 찾지 못했습니다. Tools > Enchant > Regenerate Placeholder Art 를 눌러 보세요.\n  " + EnchantArt.ArtFolder);
                return null;
            }

            // ---------- 카메라 (UI 뒤를 칠하는 용도뿐) ----------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.05f, 0.05f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            ShellSceneBuilder.MakeEventSystem();

            // 버튼이 부를 대상이 먼저 있어야 해서 게임을 먼저 만듭니다.
            var game = new GameObject("EnchantGame").AddComponent<EnchantGame>();

            // ---------- 캔버스 ----------
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var hud = canvasGo.AddComponent<EnchantHud>();

            // ---------- 배경 : 화면을 꽉 채우고 넘치는 쪽은 잘립니다 (타이틀과 같은 Cover) ----------
            var bg = ShellUI.AddImage(root, "Background", background);
            var fitter = bg.gameObject.AddComponent<AspectFitter>();
            ShellSceneBuilder.WireInts(fitter, ("mode", (int)AspectFitMode.Cover));
            fitter.Aspect = background.rect.width / background.rect.height;

            // ---------- 가운데 : 검 ----------
            var area = new GameObject("SwordArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(root, false);
            area.anchorMin = new Vector2(0.5f, 0f);
            area.anchorMax = new Vector2(0.5f, 1f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.offsetMin = new Vector2(-SwordWidth * 0.5f, SwordBottom);
            area.offsetMax = new Vector2(SwordWidth * 0.5f, -SwordTop);

            var glow = ShellUI.AddImage(area, "Glow", glowSprite);
            Anchor(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   Vector2.zero, new Vector2(GlowSize, GlowSize));

            var swordRoot = new GameObject("SwordRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            swordRoot.SetParent(area, false);
            Stretch(swordRoot, 0f);

            var sword = ShellUI.AddImage(swordRoot, "Sword", EnchantArt.SwordSprite(imageNames.Count > 0 ? imageNames[0] : ""));
            sword.preserveAspect = true;   // 어떤 크기의 그림이든 칸 안에 비율대로
            Stretch(sword.rectTransform, 0f);

            // 결과 글자. 톡 커지는 연출(최대 1.12배)까지 화면 폭 안에 들어오도록 칸을 900 으로 잡습니다.
            // 문구가 길면 칸에 맞춰 글자가 작아집니다 (Best Fit).
            var banner = Label(area, "Banner", 108, Color.white, 4f);
            Anchor(banner.rectTransform, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f),
                   Vector2.zero, new Vector2(900f, 170f));
            banner.text = "";

            var flash = ShellUI.AddImage(root, "Flash", null);
            Stretch(flash.rectTransform, 0f);
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.enabled = false;

            // ---------- 왼쪽 위 : 강화 수치 ----------
            var badgeImg = ShellUI.AddImage(root, "LevelBadge", badge);
            Anchor(badgeImg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                   new Vector2(36f, -36f), new Vector2(BadgeSize, BadgeSize));
            var level = Label(badgeImg.rectTransform, "LevelText", 96, new Color(1f, 0.93f, 0.70f), 3f);
            Stretch(level.rectTransform, 30f);
            level.text = "+0";

            var bestChip = Chip(root, "BestChip", buttonSprite, new Vector2(0f, 1f), new Vector2(36f, -300f), new Vector2(BadgeSize, 72f));
            var best = Label(bestChip, "BestText", 40, new Color(1f, 0.86f, 0.50f), 2f);
            Stretch(best.rectTransform, 14f);

            // ---------- 아래 : 확률 줄 + 버튼 세 개 ----------
            var chance = Label(root, "ChanceText", 46, new Color(1f, 0.94f, 0.84f), 3f);
            Anchor(chance.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                   new Vector2(0f, ChanceY), new Vector2(1040f, 72f));

            var normal = MakeButton(root, "NormalButton", buttonSprite, NormalColor, new Vector2(0f, 0f),
                                    Vector2.zero, Vector2.zero, game, "PressNormal");
            HalfWidth((RectTransform)normal.transform, right: false);
            ButtonTitle(normal, "enchant.btn.normal", "ENCHANT", 64);
            StaticSub(normal, "enchant.btn.normal.sub", "FAIL = DESTROYED");

            var safe = MakeButton(root, "SafeButton", buttonSprite, SafeColor, new Vector2(1f, 0f),
                                  Vector2.zero, Vector2.zero, game, "PressSafe");
            HalfWidth((RectTransform)safe.transform, right: true);
            ButtonTitle(safe, "enchant.btn.safe", "SAFE", 64);
            var safeSub = Sub(safe, 44);

            var melt = MakeButton(root, "MeltButton", buttonSprite, MeltColor, new Vector2(0.5f, 0f),
                                  new Vector2(0f, MeltButtonY), MeltButtonSize, game, "PressMelt");
            ButtonTitle(melt, "enchant.btn.melt", "MELT", 56);
            var meltSub = Sub(melt, 36);

            // ---------- 검이 부서졌을 때 ----------
            var overlay = ShellUI.AddImage(root, "BrokenOverlay", null, raycast: true);   // 뒤쪽 버튼을 막습니다
            overlay.color = new Color(0.07f, 0.02f, 0.02f, 0.84f);
            Stretch(overlay.rectTransform, 0f);
            var overlayRt = overlay.rectTransform;

            var brokenTitle = Label(overlayRt, "Title", 150, new Color(1f, 0.38f, 0.30f), 5f);
            Anchor(brokenTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, 380f), new Vector2(1000f, 200f));
            Localize(brokenTitle, "enchant.broken.title", "DESTROYED!");

            var brokenBody = Label(overlayRt, "Body", 50, new Color(1f, 0.90f, 0.86f), 2f);
            brokenBody.lineSpacing = 1.2f;
            Anchor(brokenBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0f, 110f), new Vector2(1000f, 300f));

            var newSword = MakeButton(overlayRt, "NewSwordButton", buttonSprite, NewSwordColor, new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, -220f), new Vector2(600f, 210f), game, "PressNewSword");
            ButtonTitle(newSword, "enchant.broken.button", "NEW SWORD", 68, full: true);
            newSword.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);

            // ---------- "분해하시겠습니까?" 창 ----------
            var meltPopup = BuildMeltPopup(root, game, out var meltQuestion);

            // ---------- 로비로 나가기 : 오른쪽 위 공통 화살표 + 확인 팝업 + 랭킹 흐름 (마지막에 = 맨 위) ----------
            ShellSceneBuilder.MakeExitToLobbyUI(root);

            // ---------- 검 그림 목록 : 표의 이름 + 진짜 그림이 들어온 이름 ----------
            var names = new List<string>(imageNames);
            foreach (var name in EnchantArt.RealSwordNames())
                if (!names.Contains(name)) names.Add(name);

            var swordNames = new List<string>();
            var swordSprites = new List<Object>();
            var art = new StringBuilder();
            foreach (var name in names)
            {
                var sprite = EnchantArt.SwordSprite(name);
                if (sprite == null) continue;
                swordNames.Add(name);
                swordSprites.Add(sprite);
                bool real = AssetDatabase.GetAssetPath(sprite).StartsWith(EnchantArt.SwordsFolder);
                art.Append("  ").Append(name).Append(real ? "  진짜 그림" : "  임시 그림").Append('\n');
            }
            Debug.Log("[Enchant] 검 그림 " + swordNames.Count + "장\n" + art);

            // ---------- 연결 ----------
            ShellSceneBuilder.Wire(game, ("config", config), ("hud", hud), ("meltPopup", meltPopup));
            ShellSceneBuilder.Wire(hud,
                ("levelText", level), ("bestText", best),
                ("swordRoot", swordRoot), ("sword", sword), ("glow", glow),
                ("chanceText", chance), ("bannerText", banner), ("flash", flash),
                ("normalButton", normal), ("safeButton", safe), ("meltButton", melt),
                ("safeSub", safeSub), ("meltSub", meltSub),
                ("brokenOverlay", overlay.gameObject), ("brokenBody", brokenBody), ("newSwordButton", newSword.gameObject),
                ("meltQuestion", meltQuestion));
            ShellSceneBuilder.WireStrings(hud, "swordNames", swordNames);
            ShellSceneBuilder.WireArray(hud, "swordSprites", swordSprites);

            return game;
        }

        static EnchantConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<EnchantConfig>(EnchantTableCsv.ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory("Assets/Enchant");
                config = ScriptableObject.CreateInstance<EnchantConfig>();
                AssetDatabase.CreateAsset(config, EnchantTableCsv.ConfigPath);
                AssetDatabase.SaveAssets();
            }

            // 작업 폴더의 강화 표 / 화면 글자표가 더 새로우면 가져옵니다.
            EnchantTableCsv.Sync();
            ShellStringsCsv.Sync();
            EnchantTableCsv.Bake(config);

            AssetDatabase.SaveAssets();
            return config;
        }

        // ------------------------------------------------------------------ 분해 확인 창

        /// <summary>
        /// 껍데기의 빈 판(9-슬라이스) 위에 제목 · 설명 글자 + [확인] [취소] 그림 버튼.
        /// "로비로 나가시겠습니까?" 와 같은 판 · 같은 버튼이라 앱 안에서 모양이 한 벌로 보입니다.
        /// </summary>
        static PopupPanel BuildMeltPopup(RectTransform root, EnchantGame game, out Text question)
        {
            question = null;
            var panelSprite = ShellArt.Load(ShellArt.PanelPath);
            var okSprite = ShellArt.Load(ShellArt.OkButtonPath);
            var cancelSprite = ShellArt.Load(ShellArt.CancelButtonPath);
            if (panelSprite == null || okSprite == null || cancelSprite == null) return null;

            var popup = ShellSceneBuilder.MakeSlicedPopup(root, "MeltPopup", panelSprite, MeltPopupSize, out var panel);
            float aspect = MeltPopupSize.x / MeltPopupSize.y;

            var title = ShellUI.AddText(panel, "Title", 64, Ink);
            ShellUI.Place(title.rectTransform, new Vector2(0.5f, 0.80f), new Vector2(0.84f, 0.14f));
            Localize(title, "enchant.melt.title", "MELT?");

            question = ShellUI.AddText(panel, "Question", 44, Ink);
            question.lineSpacing = 1.2f;
            ShellUI.Place(question.rectTransform, new Vector2(0.5f, 0.54f), new Vector2(0.86f, 0.30f));

            ShellSceneBuilder.MakePanelButton(panel, "Ok", okSprite, new Vector2(0.29f, 0.20f), aspect, game, "OnMeltConfirmed");
            ShellSceneBuilder.MakePanelButton(panel, "Cancel", cancelSprite, new Vector2(0.71f, 0.20f), aspect, game, "OnMeltCancelled");
            return popup;
        }

        // ------------------------------------------------------------------ 부품

        /// <summary>색을 입힌 픽셀 버튼 판. 누를 수 없을 때는 어두운 회색이 됩니다.</summary>
        static Button MakeButton(RectTransform parent, string name, Sprite sprite, Color color, Vector2 corner,
                                 Vector2 pos, Vector2 size, Object target, string method)
        {
            var image = ShellUI.AddImage(parent, name, sprite, raycast: true);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / ButtonPixel;
            Anchor(image.rectTransform, corner, corner, corner, pos, size);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = color;
            colors.selectedColor = color;
            colors.pressedColor = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 1f);
            colors.disabledColor = DisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            image.color = Color.white;   // 색은 버튼 상태 색이 입힙니다

            ShellSceneBuilder.BindClickPublic(button, target, method);
            return button;
        }

        /// <summary>화면 아래의 큰 버튼 하나를 화면 왼쪽(또는 오른쪽) 절반에 맞춥니다.</summary>
        static void HalfWidth(RectTransform rt, bool right)
        {
            rt.anchorMin = new Vector2(right ? 0.5f : 0f, 0f);
            rt.anchorMax = new Vector2(right ? 1f : 0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            float inner = BigButtonGap * 0.5f;
            rt.offsetMin = new Vector2(right ? inner : BigButtonSide, BigButtonY);
            rt.offsetMax = new Vector2(right ? -BigButtonSide : -inner, BigButtonY + BigButtonHeight);
        }

        /// <summary>버튼의 큰 글자. full 이면 버튼 한가운데, 아니면 위쪽 절반.</summary>
        static void ButtonTitle(Button button, string key, string fallback, int size, bool full = false)
        {
            var text = Label((RectTransform)button.transform, "Title", size, LabelColor, 3f);
            if (full) Stretch(text.rectTransform, 20f);
            else ShellUI.Place(text.rectTransform, new Vector2(0.5f, 0.64f), new Vector2(0.9f, 0.44f));
            Localize(text, key, fallback);
        }

        /// <summary>버튼의 작은 글자 (아래쪽). 실행 중에 HUD 가 채웁니다.</summary>
        static Text Sub(Button button, int size)
        {
            var text = Label((RectTransform)button.transform, "Sub", size, SubColor, 2f);
            ShellUI.Place(text.rectTransform, new Vector2(0.5f, 0.28f), new Vector2(0.9f, 0.34f));
            return text;
        }

        /// <summary>버튼의 작은 글자 중 바뀌지 않는 것 (strings.csv 에서).</summary>
        static void StaticSub(Button button, string key, string fallback)
        {
            var text = Sub(button, 36);
            Localize(text, key, fallback);
        }

        /// <summary>그림자 테두리를 두른 글자. 불빛이 일렁이는 배경 위에서도 읽히게.</summary>
        static Text Label(RectTransform parent, string name, int size, Color color, float outline)
        {
            var text = ShellUI.AddText(parent, name, size, color);
            if (outline > 0f) ShellUI.AddOutline(text, outline, Shadow);
            return text;
        }

        /// <summary>글자 뒤에 까는 반투명 판.</summary>
        static RectTransform Chip(RectTransform parent, string name, Sprite sprite, Vector2 corner, Vector2 pos, Vector2 size)
        {
            var chip = ShellUI.AddImage(parent, name, sprite);
            chip.type = Image.Type.Sliced;
            chip.pixelsPerUnitMultiplier = 1f / ButtonPixel;
            chip.color = new Color(0.16f, 0.10f, 0.09f, 0.85f);
            Anchor(chip.rectTransform, corner, corner, corner, pos, size);
            return chip.rectTransform;
        }

        /// <summary>실행할 때 strings.csv 에서 다시 읽는 글자. 굽는 순간의 글자도 같이 넣어 둡니다 (미리보기용).</summary>
        static void Localize(Text text, string key, string fallback)
        {
            text.text = StringTable.Get(key, fallback);
            var localized = text.gameObject.AddComponent<LocalizedText>();
            ShellSceneBuilder.WireText(localized, ("key", key), ("fallback", fallback));
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

        static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
