using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Enchant.EditorTools
{
    /// <summary>
    /// 플레이 모드에 들어가지 않고 검 강화 화면을 PNG 로 뽑습니다 (JumpJump_Preview/20_~26_enchant_*.png).
    /// 그림을 갈아 끼운 뒤 "검이 칸에 잘 들어가는지, 버튼 글자가 넘치지 않는지" 를 에디터 없이 확인할 때 씁니다.
    ///
    /// 게임을 Step 으로 굴려서 원하는 장면(성공 직후 · 강화 중 · 부서짐 ...)을 만든 뒤 찍습니다.
    /// **폰의 기록은 건드리지 않습니다** (SaveProgress 를 끄고, 판 종료 신호도 끕니다).
    /// </summary>
    public static class EnchantPreview
    {
        const int Width = 540;
        const int Height = 960;
        const int TallHeight = 1200;   // 20:9 폰

        const float Dt = 1f / 60f;

        [MenuItem("Tools/Enchant/Capture Preview PNG", priority = 21)]
        public static void Capture()
        {
            CaptureTo(Path.Combine(Directory.GetCurrentDirectory(), "JumpJump_Preview"));
        }

        public static void CaptureTo(string outputFolder)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(outputFolder);

            Arcade.GameSession.Recording = false;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var game = EnchantSceneBuilder.Populate();
                if (game == null) return;

                game.SaveProgress = false;
                game.Boot();
                game.Seed(31);

                var cam = Object.FindFirstObjectByType<Camera>();
                var canvas = Object.FindFirstObjectByType<Canvas>();
                PrepareForRenderTexture(cam, canvas);

                // 처음 들어왔을 때 (+0)
                game.LoadProgress(0, 0, 0);
                Settle(game, 3);
                Shoot(cam, canvas, Path.Combine(outputFolder, "20_enchant_start.png"), Width, Height);

                // +12 -> +13 성공 직후 (분해 가능 · 주문서 2장)
                SucceedAt(game, 12, 2, 23);
                Settle(game, 16);   // 번쩍임이 지나가고 "성공!" 글자가 남아 있는 순간
                Shoot(cam, canvas, Path.Combine(outputFolder, "21_enchant_success.png"), Width, Height);

                // 같은 장면을 세로로 긴 폰(20:9)에서
                Shoot(cam, canvas, Path.Combine(outputFolder, "26_enchant_tall.png"), Width, TallHeight);

                // 강화 중 (검이 떨리고 "강화 중...")
                game.LoadProgress(13, 2, 23);
                game.Step(Dt, EnchantAction.Normal);
                for (int i = 0; i < 20; i++) game.Step(Dt, EnchantAction.None);
                Shoot(cam, canvas, Path.Combine(outputFolder, "22_enchant_working.png"), Width, Height);

                // 부서짐 -> 결과 창 + [새 검 받기]
                BreakAt(game, 17, 2, 23);
                game.Step(game.Config.newSwordLockSeconds + 0.1f, EnchantAction.None);
                Shoot(cam, canvas, Path.Combine(outputFolder, "23_enchant_broken.png"), Width, Height);

                // "분해하시겠습니까?"
                game.LoadProgress(13, 2, 23);
                Settle(game, 2);
                game.PressMelt();
                Canvas.ForceUpdateCanvases();
                Shoot(cam, canvas, Path.Combine(outputFolder, "24_enchant_melt_ask.png"), Width, Height);
                Arcade.PopupPanel.CloseTop();

                // 높은 수치 (+35, 금빛 검) 에서 안전 강화 실패 직후
                SafeFailAt(game, 35, 7, 35);
                Settle(game, 4);
                Shoot(cam, canvas, Path.Combine(outputFolder, "25_enchant_high.png"), Width, Height);

                Debug.Log("[Enchant] 미리보기 저장 완료 -> " + outputFolder);
            }
            finally
            {
                Arcade.GameSession.Recording = true;
            }
        }

        static void SucceedAt(EnchantGame game, int level, int scrolls, int best)
        {
            for (int i = 0; i < 500; i++)
            {
                game.LoadProgress(level, scrolls, best);
                Enchant(game, EnchantAction.Normal);
                if (game.LastOutcome == EnchantOutcome.Success) return;
            }
        }

        static void SafeFailAt(EnchantGame game, int level, int scrolls, int best)
        {
            for (int i = 0; i < 500; i++)
            {
                game.LoadProgress(level, scrolls, best);
                Enchant(game, EnchantAction.Safe);
                if (game.LastOutcome == EnchantOutcome.SafeFail) return;
            }
        }

        static void BreakAt(EnchantGame game, int level, int scrolls, int best)
        {
            for (int i = 0; i < 500; i++)
            {
                game.LoadProgress(level, scrolls, best);
                Enchant(game, EnchantAction.Normal);
                if (game.LastOutcome == EnchantOutcome.Destroyed) return;
            }
        }

        static void Enchant(EnchantGame game, EnchantAction kind)
        {
            game.Step(Dt, kind);
            if (game.State == EnchantState.Working) game.Step(game.Config.workSeconds, EnchantAction.None);
        }

        static void Settle(EnchantGame game, int frames)
        {
            for (int i = 0; i < frames; i++) game.Step(Dt, EnchantAction.None);
        }

        /// <summary>
        /// 배치 모드에서는 Screen 크기가 더미라 화면 겹치기(Overlay) 캔버스가 렌더 텍스처에 찍히지 않고
        /// CanvasScaler 도 엉뚱하게 계산합니다. 카메라 소속으로 바꾸고 배율을 직접 정합니다 (Shoot 에서).
        /// </summary>
        static void PrepareForRenderTexture(Camera cam, Canvas canvas)
        {
            if (cam == null || canvas == null) return;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;
        }

        static void Shoot(Camera cam, Canvas canvas, string path, int width, int height)
        {
            if (cam == null) return;

            // ScaleWithScreenSize(1080x1920, 가로세로 반반) 과 같은 배율을 손으로 넣습니다.
            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Sqrt(width / 1080f * (height / 1920f));
            }

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var previousTarget = cam.targetTexture;
            float previousAspect = cam.aspect;

            cam.targetTexture = rt;
            cam.aspect = (float)width / height;

            // 캔버스 크기가 정해진 뒤에 배경(Cover)을 다시 맞춥니다.
            Canvas.ForceUpdateCanvases();
            foreach (var fitter in Object.FindObjectsByType<Arcade.AspectFitter>(FindObjectsSortMode.None)) fitter.Apply();
            Canvas.ForceUpdateCanvases();

            // 첫 렌더는 동적 글꼴 아틀라스가 준비되기 전이라 엉뚱하게 나옵니다. 한 번 버리고 두 번째를 씁니다.
            cam.Render();
            cam.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());

            RenderTexture.active = previousActive;
            cam.targetTexture = previousTarget;
            cam.aspect = previousAspect;
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
