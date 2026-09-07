using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Archery.EditorTools
{
    /// <summary>
    /// 플레이 모드에 들어가지 않고 활쏘기 화면을 PNG 로 뽑습니다.
    /// 그림을 갈아 끼운 뒤 "과녁이 레일 위에 제대로 얹혔는지, HUD 가 겹치지 않는지"를
    /// 에디터를 띄우지 않고 확인할 때 씁니다. 결과는 JumpJump_Preview 폴더에 저장됩니다.
    /// </summary>
    public static class ArcheryPreview
    {
        const int Width = 540;
        const int Height = 960;

        [MenuItem("Tools/Archery/Capture Preview PNG", priority = 21)]
        public static void Capture()
        {
            CaptureTo(Path.Combine(Directory.GetCurrentDirectory(), "JumpJump_Preview"));
        }

        public static void CaptureTo(string outputFolder)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(outputFolder);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = ArcherySceneBuilder.Populate();
            if (game == null) return;

            var cam = Object.FindFirstObjectByType<Camera>();
            var target = Object.FindFirstObjectByType<TargetController>();
            PrepareForRenderTexture(cam);

            game.ResetRun();
            Settle(game, 2);   // ArcheryHud.Refresh 가 한 번은 돌아야 Ready 오버레이가 채워집니다
            Canvas.ForceUpdateCanvases();
            Shoot(cam, Path.Combine(outputFolder, "10_archery_ready.png"));

            // 시작한 뒤 몇 발 맞힌 상태
            PlayUntilHits(game, target, targetHits: 6, maxSteps: 3600);
            Settle(game, 4);
            Canvas.ForceUpdateCanvases();
            Shoot(cam, Path.Combine(outputFolder, "11_archery_playing.png"));

            // 많이 맞혀서 과녁이 작아지고 빨라진 상태
            PlayUntilHits(game, target, targetHits: 60, maxSteps: 18000);
            Settle(game, 4);
            Canvas.ForceUpdateCanvases();
            Shoot(cam, Path.Combine(outputFolder, "12_archery_late.png"));

            // 일부러 화살을 다 날려 결과 화면을 만듭니다
            ForceGameOver(game);
            Settle(game, 4);
            Canvas.ForceUpdateCanvases();
            Shoot(cam, Path.Combine(outputFolder, "13_archery_gameover.png"));

            Debug.Log($"[Archery] 미리보기 저장 완료 -> {outputFolder}\n" +
                      $"  최고 명중 {game.Hits}발, 과녁 {game.SizePercent:0.#}%, 상태 {game.State}");
        }

        /// <summary>
        /// 배치 모드에서는 Screen 크기가 더미라 화면 겹치기(Overlay) 캔버스가 렌더 텍스처에 찍히지 않고
        /// CanvasScaler 도 엉뚱하게 계산합니다. 카메라 소속으로 바꾸고 배율을 직접 고정합니다.
        /// </summary>
        static void PrepareForRenderTexture(Camera cam)
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (cam == null || canvas == null) return;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Height / 1920f;
            }
        }

        static void Settle(ArcheryGame game, int frames)
        {
            for (int i = 0; i < frames; i++) game.Step(1f / 60f, false);
        }

        /// <summary>ArcheryPlaytest 의 조준 봇과 같은 방식으로, 원하는 명중 수까지 진행시킵니다.</summary>
        static void PlayUntilHits(ArcheryGame game, TargetController target, int targetHits, int maxSteps)
        {
            const float dt = 1f / 60f;
            var config = game.Config;

            for (int i = 0; i < maxSteps && game.Hits < targetHits; i++)
            {
                bool tap;
                if (game.State != GameState.Playing)
                {
                    tap = true;
                }
                else
                {
                    float speed = config.ArrowSpeed(game.Hits);
                    float travel = config.targetY - (config.arrowStartY + config.arrowLength);
                    float t = travel / Mathf.Max(0.01f, speed);
                    float radius = target.Radius;
                    float limit = Mathf.Max(0.01f, config.playHalfWidth - radius);
                    float v = config.TargetSpeed(game.Hits) * target.Direction;
                    float predicted = Mathf.PingPong(target.X + v * t + limit, 2f * limit) - limit;

                    tap = game.Ammo > 0 && Mathf.Abs(predicted) <= radius / Mathf.Max(1, config.rings) * 0.5f;
                }

                game.Step(dt, tap);
            }
        }

        /// <summary>결과 화면을 찍으려고 일부러 빗나가게 쏴서 화살을 다 씁니다.</summary>
        static void ForceGameOver(ArcheryGame game)
        {
            const float dt = 1f / 60f;
            for (int i = 0; i < 3600 && game.State == GameState.Playing; i++)
            {
                // 과녁이 화면 끝에 가 있을 때만 쏘면 거의 다 빗나갑니다.
                game.Step(dt, i % 12 == 0);
            }
        }

        static void Shoot(Camera cam, string path)
        {
            if (cam == null) return;

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };

            var previousTarget = cam.targetTexture;
            float previousAspect = cam.aspect;

            cam.targetTexture = rt;
            cam.aspect = (float)Width / Height;

            // 첫 렌더는 스프라이트 머티리얼과 동적 폰트 아틀라스가 아직 준비되기 전이라
            // 엉뚱하게 나옵니다. 한 번 버리고 두 번째 결과를 씁니다.
            cam.Render();
            cam.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = rt;

            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
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
