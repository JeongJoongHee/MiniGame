using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.EditorTools
{
    /// <summary>
    /// 플레이 모드에 들어가지 않고 타이틀 / 로비 화면을 PNG 로 뽑습니다.
    /// 그림을 갈아 끼운 뒤 "버튼이 제자리에 붙었는지" 를 눈으로 바로 확인할 때 씁니다.
    /// 결과는 JumpJump_Preview 폴더에 저장됩니다.
    /// </summary>
    public static class ShellPreview
    {
        const int Width = 540;

        /// <summary>
        /// 두 가지 화면 비율로 찍습니다. 9:16 은 기획서 목업과 같은 비율이고,
        /// 20:9 는 요즘 폰의 실제 비율입니다. 로비는 20:9 에서 위아래에 여백이 생깁니다.
        /// </summary>
        static readonly (string suffix, int height)[] Shapes =
        {
            ("", 960),        // 540x960  = 9:16
            ("_tall", 1200),  // 540x1200 = 20:9  (요즘 폰)
        };

        [MenuItem("Tools/Arcade/Capture Shell Preview PNG", priority = 21)]
        public static void Capture()
        {
            CaptureTo(Path.Combine(Directory.GetCurrentDirectory(), "JumpJump_Preview"));
        }

        public static void CaptureTo(string outputFolder)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(outputFolder);

            foreach (var (suffix, height) in Shapes)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var title = ShellSceneBuilder.PopulateTitle();
                if (title != null)
                {
                    PrepareForRenderTexture();
                    title.Apply();
                    Shoot(Path.Combine(outputFolder, "00_title" + suffix + ".png"), height);
                }

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var lobby = ShellSceneBuilder.PopulateLobby();
                if (lobby != null)
                {
                    PrepareForRenderTexture();
                    lobby.Build();
                    Shoot(Path.Combine(outputFolder, "00_lobby" + suffix + ".png"), height);

                    // 팝업은 씬에 꺼진 채로 들어 있습니다. 하나씩 켜서 자리를 확인합니다.
                    ShootPopup("SettingsPopup", Path.Combine(outputFolder, "00_popup_settings" + suffix + ".png"), height);
                    ShootPopup("QuitPopup", Path.Combine(outputFolder, "00_popup_quit" + suffix + ".png"), height);
                    ShootPopup("RankingPopup", Path.Combine(outputFolder, "00_popup_ranking" + suffix + ".png"), height);

                    // 두 번째 게임 칸의 랭킹 아이콘으로 연 모양 = 두 번째 탭이 골라진 창 (탭 버그 수정 확인용)
                    ShootPopup("RankingPopup", Path.Combine(outputFolder, "00_popup_ranking_tab2" + suffix + ".png"), height,
                               popup => { foreach (var v in popup.GetComponentsInChildren<RankingPopup>(true)) v.SelectTab(1); });

                    // 설정 창 위에 뜨는 [별명 바꾸기] 창
                    ShootPopup("NicknamePopup", Path.Combine(outputFolder, "00_popup_nickname_change" + suffix + ".png"), height,
                               popup => { foreach (var v in popup.GetComponentsInChildren<NicknamePopup>(true)) v.SetChangingForPreview(true); });
                }
            }

            Debug.Log("[Arcade] 타이틀 / 로비 미리보기 저장 완료 -> " + outputFolder);
        }

        /// <summary>
        /// 씬에 꺼진 채로 들어 있는 팝업 하나를 켜서 찍고 다시 끕니다.
        /// (Find 는 꺼진 물체를 못 찾으므로 컴포넌트로 훑습니다)
        /// </summary>
        public static void ShootPopup(string name, string path, int height, System.Action<PopupPanel> prepare = null)
        {
            PopupPanel found = null;
            foreach (var popup in Object.FindObjectsByType<PopupPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (popup.name == name) found = popup;

            if (found == null)
            {
                Debug.LogWarning("[Arcade] 팝업을 찾지 못했습니다: " + name);
                return;
            }

            found.Open();
            RefreshViews(found);
            prepare?.Invoke(found);
            foreach (var view in found.GetComponentsInChildren<NicknamePopup>(true)) view.Refresh();
            Relayout();
            Shoot(path, height);
            found.Close();
        }

        /// <summary>
        /// 에디터에서는 OnEnable 이 불리지 않아 창 안이 비어 있습니다.
        /// 실행 중에 채워지는 글자(랭킹 줄 / 별명 안내)를 여기서 직접 채워 줍니다.
        /// </summary>
        static void RefreshViews(PopupPanel popup)
        {
            foreach (var view in popup.GetComponentsInChildren<RankingPopup>(true)) view.Refresh();
            foreach (var view in popup.GetComponentsInChildren<NicknamePopup>(true)) view.Refresh();
            foreach (var view in popup.GetComponentsInChildren<NicknameLabel>(true)) view.Refresh();
        }

        /// <summary>
        /// 배치 모드에서는 Screen 크기가 더미라 화면 겹치기(Overlay) 캔버스가 렌더 텍스처에 찍히지 않고
        /// CanvasScaler 도 엉뚱하게 계산합니다. 카메라 소속으로 바꾸고 배율을 직접 고정합니다.
        /// </summary>
        static void PrepareForRenderTexture()
        {
            var cam = Object.FindFirstObjectByType<Camera>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (cam == null || canvas == null) return;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Width / 1080f;
            }

            Relayout();
        }

        /// <summary>
        /// 캔버스 크기가 바뀌면 배경 사각형을 다시 맞춰야 합니다.
        /// 플레이 모드에서는 AspectFitter 가 알아서 하지만, 에디터에서는 Update 가 돌지 않아
        /// 여기서 직접 불러 줍니다.
        /// </summary>
        static void Relayout()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var fitter in Object.FindObjectsByType<AspectFitter>(FindObjectsSortMode.None))
                fitter.Apply();
            Canvas.ForceUpdateCanvases();
        }

        static void Shoot(string path, int height)
        {
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) return;

            var rt = new RenderTexture(Width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };

            var previousTarget = cam.targetTexture;
            float previousAspect = cam.aspect;

            cam.targetTexture = rt;
            cam.aspect = (float)Width / height;

            // 캔버스는 "그려질 때" 비로소 렌더 텍스처 크기를 알게 됩니다. 그래서 한 번 그려 보고,
            // 그 크기로 배경을 다시 맞춘 뒤에 진짜 사진을 찍습니다.
            // (첫 렌더는 스프라이트 머티리얼과 동적 폰트 아틀라스도 아직 준비 전이라 어차피 버립니다)
            cam.Render();
            Relayout();
            cam.Render();
            cam.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = rt;

            var texture = new Texture2D(Width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, height), 0, 0);
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
