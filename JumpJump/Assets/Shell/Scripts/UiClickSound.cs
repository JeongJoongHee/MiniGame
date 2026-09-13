using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// **앱의 모든 버튼에서 "딸깍" 소리를 냅니다** (2026-09-13).
    ///
    /// 씬이 열릴 때마다 씬 안의 버튼(꺼진 채로 구워진 팝업 속 버튼까지)을 전부 찾아 소리를 붙입니다.
    /// 그래서 **새 미니게임을 만들어도 여기를 고칠 필요가 없습니다.**
    /// 게임 중의 터치(점프 · 발사)는 버튼이 아니라서 소리가 나지 않습니다.
    /// 누를 수 없는 버튼(회색)은 onClick 이 불리지 않으니 소리도 안 납니다.
    ///
    /// 소리 파일은 작업 폴더(또는 `@리소스` 폴더)의 <c>Click.wav</c> — Build All Scenes 때
    /// `Assets/Shell/Resources/Sounds/Click.wav` 로 들어옵니다 (ShellArt). 파일이 없으면 조용히 넘어갑니다.
    /// 소리 크기는 <see cref="ArcadeConfig.clickVolume"/>.
    ///
    /// 이 컴포넌트 자체는 "이미 붙였다" 는 표시일 뿐입니다 — 같은 버튼에 소리가 두 번 붙지 않게.
    /// 배치 미리보기·플레이테스트는 플레이 모드가 아니라서 소리가 나지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiClickSound : MonoBehaviour
    {
        /// <summary>Resources 안의 경로 (확장자 없이).</summary>
        public const string ClickResource = "Sounds/Click";

        static AudioSource _player;
        static AudioListener _ownListener;
        static AudioClip _click;
        static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachAll();

            // 새 씬이 자기 AudioListener 를 가지고 왔으면 우리 것은 치웁니다 (둘이면 Unity 가 경고를 냅니다).
            if (_ownListener != null && CountListeners() > 1)
            {
                Destroy(_ownListener);
                _ownListener = null;
            }
        }

        /// <summary>열려 있는 씬들의 버튼 전부에 소리를 붙입니다.</summary>
        public static void AttachAll()
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Attach(button);
        }

        /// <summary>버튼 하나에 소리를 붙입니다. 이번에 새로 붙였으면 true (이미 붙어 있으면 false).</summary>
        public static bool Attach(Button button)
        {
            if (button == null || button.GetComponent<UiClickSound>() != null) return false;
            button.gameObject.AddComponent<UiClickSound>();
            button.onClick.AddListener(PlayClick);
            return true;
        }

        public static void PlayClick()
        {
            if (!Application.isPlaying) return;

            if (!_loaded)
            {
                _click = Resources.Load<AudioClip>(ClickResource);
                _loaded = true;
            }
            if (_click == null) return;

            if (_player == null)
            {
                var go = new GameObject("UiClickSound");
                DontDestroyOnLoad(go);
                _player = go.AddComponent<AudioSource>();
                _player.playOnAwake = false;
                _player.spatialBlend = 0f;   // 화면 UI 소리라 위치와 상관없이
            }

            // 이 앱의 씬에는 소리를 듣는 귀(AudioListener)가 없어서, 없으면 여기에 하나 둡니다.
            if (_ownListener == null && CountListeners() == 0)
                _ownListener = _player.gameObject.AddComponent<AudioListener>();

            _player.PlayOneShot(_click, ArcadeConfig.Instance.clickVolume);
        }

        static int CountListeners() => FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
    }
}
