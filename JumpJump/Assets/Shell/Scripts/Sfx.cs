using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arcade
{
    /// <summary>
    /// **효과음을 내는 단 한 곳입니다** (2026-09-14). 버튼 딸깍 소리도 여기서 납니다.
    ///
    /// 소리 파일은 작업 폴더의 <c>Sound/</c> 폴더 — Build All Scenes 때 `Assets/Shell/Resources/Sounds/` 로
    /// 같은 이름 그대로 들어옵니다 (ShellArt). **파일 이름(확장자 빼고)이 곧 소리 이름**이고,
    /// 아래 상수가 코드가 부르는 이름입니다. 파일이 없으면 조용히 넘어갑니다 — 소리가 없어도 게임은 돕니다.
    /// wav / ogg / mp3 어느 것이든 됩니다.
    ///
    /// **플레이 모드가 아니면 아무것도 하지 않습니다.** 그래서 게임의 Step 안에서 불러도
    /// 배치 플레이테스트 · 미리보기가 그대로 돕니다 (소리만 안 날 뿐).
    ///
    /// 크기는 <see cref="ArcadeConfig.sfxVolume"/> (전체) × <see cref="ArcadeConfig.clickVolume"/> (딸깍 소리만).
    /// </summary>
    public static class Sfx
    {
        /// <summary>Resources 안의 폴더 (끝에 / 포함).</summary>
        public const string Folder = "Sounds/";

        // ---- 코드가 부르는 소리 이름 = 파일 이름 -----------------------------------
        public const string Click = "Click";
        public const string GameEnter = "GameEnter";
        public const string GameOver = "GameOver";
        public const string NewRecord = "NewRecord";

        public const string Jump = "Jump";
        public const string JumpLand = "Jump_Land";
        public const string JumpFall = "Jump_Fall";
        public const string JumpTimeUp = "Jump_TimeUp";

        public const string ArcheryShoot = "Archery_Shoot";
        public const string ArcheryHit = "Archery_Hit";
        public const string ArcheryBullseye = "Archery_Bullseye";

        public const string EnchantHammer = "Enchant_Hammer";
        public const string EnchantSuccess = "Enchant_Success";
        public const string EnchantSafeFail = "Enchant_SafeFail";
        public const string EnchantDestroy = "Enchant_Destroy";
        public const string EnchantMelt = "Enchant_Melt";

        /// <summary>코드가 부르는 소리 전부. 자체 점검이 파일이 다 들어왔는지 봅니다.</summary>
        public static readonly string[] All =
        {
            Click, GameEnter, GameOver, NewRecord,
            Jump, JumpLand, JumpFall, JumpTimeUp,
            ArcheryShoot, ArcheryHit, ArcheryBullseye,
            EnchantHammer, EnchantSuccess, EnchantSafeFail, EnchantDestroy, EnchantMelt,
        };

        /// <summary>한꺼번에 겹쳐 날 수 있는 소리 수. 넘치면 가장 먼저 끝날 소리를 끊습니다.</summary>
        const int Voices = 8;

        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static AudioSource[] _voices;
        static float[] _busyUntil;
        static string[] _voiceName;
        static int[] _voiceFrame;
        static AudioListener _ownListener;
        static int _skipClickFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 새 씬이 자기 AudioListener 를 가지고 왔으면 우리 것은 치웁니다 (둘이면 Unity 가 경고를 냅니다).
            if (_ownListener != null && CountListeners() > 1)
            {
                Object.Destroy(_ownListener);
                _ownListener = null;
            }
        }

        /// <summary>소리 파일. 없으면 null (한 번 찾은 결과를 기억합니다).</summary>
        public static AudioClip Load(string name)
        {
            if (!Clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>(Folder + name);
                Clips[name] = clip;
            }
            return clip;
        }

        /// <summary>소리 길이(초). 플레이 모드가 아니거나 파일이 없으면 0.</summary>
        public static float Length(string name)
        {
            if (!Application.isPlaying) return 0f;
            var clip = Load(name);
            return clip != null ? clip.length : 0f;
        }

        /// <summary>
        /// 소리를 냅니다. <paramref name="pitch"/> 1 = 원래 높이 (1.2 = 20% 높고 빠르게),
        /// <paramref name="delay"/> 초 뒤에 나게 할 수도 있습니다.
        /// </summary>
        public static void Play(string name, float volume = 1f, float pitch = 1f, float delay = 0f)
        {
            if (!Application.isPlaying) return;   // 배치 플레이테스트 · 미리보기
            if (name == Click && _skipClickFrame == Time.frameCount) return;

            var clip = Load(name);
            if (clip == null) return;

            var config = ArcadeConfig.Instance;
            float master = config.sfxVolume * (name == Click ? config.clickVolume : 1f);
            if (master * volume <= 0f) return;

            int i = PickVoice();
            var voice = _voices[i];
            voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(master * volume);
            voice.pitch = pitch;
            if (delay > 0f) voice.PlayDelayed(delay);
            else voice.Play();

            _busyUntil[i] = Time.unscaledTime + delay + clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            _voiceName[i] = name;
            _voiceFrame[i] = Time.frameCount;
        }

        /// <summary>
        /// 이 버튼만 딸깍 대신 다른 소리를 냅니다 (게임 들어가기 등).
        /// 같은 프레임에 이미 난 딸깍은 끊고, 뒤에 올 딸깍은 건너뜁니다 — 버튼 리스너 순서와 상관없이.
        /// </summary>
        public static void PlayInsteadOfClick(string name)
        {
            if (!Application.isPlaying) return;
            _skipClickFrame = Time.frameCount;

            if (_voices != null)
                for (int i = 0; i < _voices.Length; i++)
                    if (_voices[i] != null && _voiceName[i] == Click && _voiceFrame[i] == Time.frameCount)
                    {
                        _voices[i].Stop();
                        _busyUntil[i] = 0f;
                    }

            Play(name);
        }

        static int PickVoice()
        {
            EnsureVoices();

            int best = 0;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (_busyUntil[i] <= Time.unscaledTime) return i;
                if (_busyUntil[i] < _busyUntil[best]) best = i;
            }
            return best;
        }

        static void EnsureVoices()
        {
            if (_voices == null || _voices[0] == null)
            {
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);

                _voices = new AudioSource[Voices];
                _busyUntil = new float[Voices];
                _voiceName = new string[Voices];
                _voiceFrame = new int[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    var source = go.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;   // 화면 소리라 위치와 상관없이
                    _voices[i] = source;
                }
            }

            // 이 앱의 씬에는 소리를 듣는 귀(AudioListener)가 없어서, 없으면 여기에 하나 둡니다.
            if (_ownListener == null && CountListeners() == 0)
                _ownListener = _voices[0].gameObject.AddComponent<AudioListener>();
        }

        static int CountListeners() => Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
    }
}
