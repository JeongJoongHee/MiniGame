using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace JumpJump.EditorTools
{
    /// <summary>
    /// 플레이 모드에 들어가지 않고 게임 화면을 PNG 로 뽑습니다.
    /// 아트를 교체한 뒤 "캐릭터가 발판 위에 제대로 서는지, 타일 이음매가 보이지 않는지"를
    /// 에디터를 띄우지 않고 확인할 때 씁니다.
    /// </summary>
    public static class JumpJumpPreview
    {
        const int Width = 540;
        const int Height = 960;

        [MenuItem("Tools/JumpJump/Capture Preview PNG", priority = 21)]
        public static void Capture()
        {
            CaptureTo(Path.Combine(Directory.GetCurrentDirectory(), "JumpJump_Preview"));
        }

        public static void CaptureTo(string outputFolder)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(outputFolder);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = JumpJumpSceneBuilder.Populate();
            if (game == null) return;

            var cam = Object.FindFirstObjectByType<Camera>();
            var canvas = Object.FindFirstObjectByType<Canvas>();

            // 오버레이 캔버스는 렌더 텍스처에 안 찍히므로 카메라 소속으로 잠시 바꿉니다.
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                canvas.sortingOrder = 100;   // 미리보기에서 발판보다 앞에 나오도록

                // 배치 모드에서는 Screen 크기가 더미라 CanvasScaler 가 엉뚱하게 계산합니다.
                // 렌더 텍스처 기준으로 직접 배율을 고정해야 실제 레이아웃과 같아집니다.
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = Height / 1920f;
                }
            }

            game.ResetRun();
            Settle(game, 2);   // HudController.Refresh 가 한 번은 돌아야 Ready 오버레이가 채워집니다
            Canvas.ForceUpdateCanvases();
            Shoot(cam, Path.Combine(outputFolder, "01_ready.png"));

            // 시작한 뒤 봇처럼 몇 칸 올라간 상태를 찍습니다.
            StepUntilRow(game, targetRow: 6, maxSteps: 1800);
            Settle(game, 30);
            Shoot(cam, Path.Combine(outputFolder, "02_playing.png"));

            StepUntilRow(game, targetRow: 20, maxSteps: 3600);
            Settle(game, 30);
            Shoot(cam, Path.Combine(outputFolder, "03_higher.png"));

            // 오른쪽 위 화살표를 눌렀을 때 뜨는 "로비로 나가시겠습니까?" 팝업.
            // 껍데기가 만들어 주므로 활쏘기도 똑같이 생겼습니다. 여기서 한 번만 찍습니다.
            ShootPopup(cam, "ExitToLobbyPopup", Path.Combine(outputFolder, "04_exit_popup.png"));

            // 처음 랭킹에 오를 때 한 번 뜨는 별명 창. 이것도 껍데기가 만들어 주므로
            // 활쏘기에서도 똑같이 생겼습니다.
            ShootPopup(cam, "NicknamePopup", Path.Combine(outputFolder, "05_nickname.png"));

            CaptureBackdropBands(game, cam, outputFolder);

            Debug.Log("[JumpJump] 미리보기 저장 완료 -> " + outputFolder +
                      "   (최고 칸 " + game.TopRow + ", 상태 " + game.State + ")");
        }

        /// <summary>씬에 꺼진 채로 들어 있는 팝업 하나를 켜서 찍고 다시 끕니다.</summary>
        static void ShootPopup(Camera cam, string name, string path)
        {
            Arcade.PopupPanel found = null;
            foreach (var popup in Object.FindObjectsByType<Arcade.PopupPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (popup.name == name) found = popup;

            if (found == null)
            {
                Debug.LogWarning("[JumpJump] 팝업을 찾지 못했습니다: " + name);
                return;
            }

            found.Open();

            // 에디터에서는 OnEnable 이 불리지 않아 창 안이 비어 있습니다. 직접 채워 줍니다.
            foreach (var view in found.GetComponentsInChildren<Arcade.NicknamePopup>(true)) view.Refresh();

            Canvas.ForceUpdateCanvases();
            Shoot(cam, path);
            found.Close();
        }

        /// <summary>
        /// 높이별 배경이 실제로 바뀌는지 확인용 캡처.
        /// 봇이 500m 까지 올라가려면 오래 걸리므로, 배경에만 높이를 직접 넣어서 찍습니다.
        /// 발판과 캐릭터는 그대로라 배경 비교가 쉽습니다.
        /// </summary>
        static void CaptureBackdropBands(GameManager game, Camera cam, string outputFolder)
        {
            var tiler = Object.FindFirstObjectByType<BackdropTiler>();
            var config = game.Config;
            if (tiler == null || config == null || config.backdropBands == null) return;

            // 각 구간의 한가운데와, 넘어가는 도중(경계 직전 절반)을 찍습니다.
            var marks = new System.Collections.Generic.List<float>();
            var bands = config.backdropBands;
            for (int i = 0; i < bands.Length; i++)
            {
                float from = bands[i].fromMeters;
                float to = i + 1 < bands.Length ? bands[i + 1].fromMeters : from + 400f;
                marks.Add(from + 10f);                    // 구간에 막 들어선 지점
                if (i + 1 < bands.Length)
                    marks.Add(to - config.backdropBlendMeters * 0.5f);   // 절반쯤 겹친 지점
            }

            foreach (float meters in marks)
            {
                tiler.Tick(meters);
                Canvas.ForceUpdateCanvases();
                int index = config.BackdropIndexForMeters(meters);
                float blend = config.BackdropBlend(meters, index);
                Shoot(cam, Path.Combine(outputFolder,
                      $"bg_{meters:0000}m_구간{index + 1}_겹침{blend * 100f:00}.png"));
            }

            Debug.Log("[JumpJump] 배경 구간 캡처 " + marks.Count + "장");
        }

        /// <summary>착지 스쿼시가 가라앉을 때까지 입력 없이 몇 프레임 흘려보냅니다.</summary>
        static void Settle(GameManager game, int frames)
        {
            for (int i = 0; i < frames; i++) game.Step(1f / 60f, false);
        }

        static void StepUntilRow(GameManager game, int targetRow, int maxSteps)
        {
            const float dt = 1f / 60f;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var platforms = Object.FindFirstObjectByType<PlatformManager>();

            for (int i = 0; i < maxSteps && game.TopRow < targetRow; i++)
            {
                bool tap;
                if (game.State != GameState.Playing)
                {
                    tap = true;
                }
                else
                {
                    // 다음 칸 발판이 머리 위에 가까우면 누릅니다.
                    var next = platforms.GetRow(player.CurrentRow + 1);
                    tap = player.IsGrounded && next != null &&
                          Mathf.Abs(next.X - player.transform.position.x) <= next.HalfWidth * 0.45f;
                }

                game.Step(dt, tap);
            }
        }

        static void Shoot(Camera cam, string path)
        {
            if (cam == null) return;

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };

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
