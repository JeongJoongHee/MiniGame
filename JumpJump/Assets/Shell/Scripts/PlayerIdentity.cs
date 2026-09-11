using System;
using System.Text;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **플레이어를 알아보는 값(PID)과 화면에 보이는 별명**을 맡습니다.
    ///
    /// PID 는 두 가지가 될 수 있습니다.
    ///  - 이 폰에서 만든 번호 : 앱을 처음 켤 때 임의로 만들어 저장합니다. 서버가 없어도 됩니다.
    ///  - 서버가 준 번호     : 나중에 Firebase 익명 로그인을 붙이면 그 UID 를 <see cref="UseServerId"/>
    ///                         로 넘깁니다. 그때부터는 이쪽이 PID 입니다.
    /// 그래서 **랭킹 서버를 붙이기 전에도 지금 구조 그대로 돌아갑니다.**
    ///
    /// PID 는 사람이 볼 값이 아닙니다(길고 뜻이 없습니다). 랭킹표에 나오는 것은 <see cref="Nickname"/> 입니다.
    ///
    /// 안드로이드 광고 ID · 기기 고유번호는 쓰지 않습니다 — 정책상 다른 용도로 쓰면 안 되고,
    /// 스토어 심사에서 문제가 됩니다.
    /// </summary>
    public static class PlayerIdentity
    {
        const string LocalIdKey  = "Arcade.PlayerId";
        const string ServerIdKey = "Arcade.ServerId";
        const string NicknameKey = "Arcade.Nickname";
        const string RegisteredKey = "Arcade.NicknameRegistered";

        static string _localId;
        static string _serverId;
        static string _nickname;
        static string _registered;
        static bool _loaded;

        /// <summary>별명이 바뀌었을 때. 설정 창의 "내 별명" 글자가 여기에 귀를 답니다.</summary>
        public static event Action NicknameChanged;

        /// <summary>랭킹을 매길 때 쓰는 값. 서버가 준 번호가 있으면 그것을, 없으면 이 폰의 번호를 씁니다.</summary>
        public static string Id
        {
            get
            {
                Load();
                return string.IsNullOrEmpty(_serverId) ? _localId : _serverId;
            }
        }

        /// <summary>서버가 준 번호를 쓰고 있는지. (Firebase 를 붙이기 전에는 false)</summary>
        public static bool HasServerId
        {
            get { Load(); return !string.IsNullOrEmpty(_serverId); }
        }

        /// <summary>
        /// 로그인해서 받은 번호(Firebase 익명 UID)를 PID 로 삼습니다.
        /// 2단계에서 로그인에 성공한 직후 한 번 부릅니다.
        /// </summary>
        public static void UseServerId(string id)
        {
            Load();
            id = id == null ? "" : id.Trim();
            if (_serverId == id) return;

            _serverId = id;
            PlayerPrefs.SetString(ServerIdKey, _serverId);
            PlayerPrefs.Save();
        }

        /// <summary>별명을 정한 적이 있는지. 없으면 랭킹에 올리기 전에 물어봐야 합니다.</summary>
        public static bool HasNickname
        {
            get { Load(); return !string.IsNullOrEmpty(_nickname); }
        }

        /// <summary>랭킹표에 나오는 이름. 넣을 때 <see cref="Sanitize"/> 를 거칩니다.</summary>
        public static string Nickname
        {
            get { Load(); return _nickname; }
            set
            {
                Load();
                string clean = Sanitize(value);
                if (_nickname == clean) return;

                _nickname = clean;
                PlayerPrefs.SetString(NicknameKey, _nickname);
                PlayerPrefs.Save();

                NicknameChanged?.Invoke();
            }
        }

        // ------------------------------------------------------------ 서버에 등록됐는지

        /// <summary>
        /// 지금 별명이 **이 PID 로 서버에 등록되어 있는지.** (별명은 앱 전체에서 하나뿐이라,
        /// 서버에 "이 이름은 내 것" 이라고 표시해 둔 뒤에야 랭킹에 올릴 수 있습니다)
        ///
        /// 1단계(서버 없음)에서 정한 별명은 false 입니다 — 랭킹 서버가 처음 쓰일 때 알아서 등록합니다.
        /// </summary>
        public static bool IsNicknameRegistered
        {
            get
            {
                Load();
                return HasNickname && HasServerId && _registered == RegisteredStamp(_serverId, _nickname);
            }
        }

        /// <summary>서버에 등록을 마쳤다고 적어 둡니다. 랭킹 서비스가 부릅니다.</summary>
        public static void MarkNicknameRegistered(string serverId, string nickname)
        {
            Load();
            _registered = RegisteredStamp(serverId, nickname);
            PlayerPrefs.SetString(RegisteredKey, _registered);
            PlayerPrefs.Save();
        }

        /// <summary>등록 표시를 지웁니다 (서버와 어긋난 것을 알았을 때). 다음에 다시 등록합니다.</summary>
        public static void ForgetNicknameRegistration()
        {
            Load();
            _registered = "";
            PlayerPrefs.DeleteKey(RegisteredKey);
            PlayerPrefs.Save();
        }

        static string RegisteredStamp(string serverId, string nickname)
        {
            return (serverId ?? "") + "|" + KeyOf(nickname);
        }

        /// <summary>
        /// 별명의 **서버 쪽 이름표**입니다. 영문 대소문자를 가리지 않도록 소문자로 맞춥니다 —
        /// "Tom" 이 있으면 "tom" 도 못 쓰게 하려는 것입니다. 화면에는 사용자가 적은 그대로 나옵니다.
        /// </summary>
        public static string KeyOf(string nickname)
        {
            return Sanitize(nickname).ToLowerInvariant();
        }

        /// <summary>서버에 문서 이름으로 쓸 수 있는 모양인지. (<c>.</c> / <c>..</c> / <c>__이름__</c> 은 안 됩니다)</summary>
        public static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (key == "." || key == "..") return false;
            if (key.Length >= 4 && key.StartsWith("__") && key.EndsWith("__")) return false;
            return true;
        }

        /// <summary>
        /// 별명을 쓸 수 있는 모양으로 다듬습니다.
        /// 앞뒤 공백을 없애고, 줄바꿈 같은 보이지 않는 글자를 빼고, 길이를 잘라냅니다.
        /// **남들에게 보이는 글자이므로 길이 제한은 반드시 필요합니다.**
        /// 빗금(<c>/</c> <c>\</c>)도 뺍니다 — 서버에서 별명을 문서 이름으로 쓰는데, 빗금은 폴더 구분이라 쓸 수 없습니다.
        /// 욕설 거르기는 따로 합니다 (<see cref="NicknameRules"/>).
        /// </summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            var sb = new StringBuilder(raw.Length);
            bool lastWasSpace = false;

            foreach (char c in raw)
            {
                if (char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) continue;
                if (c == '/' || c == '\\') continue;

                bool isSpace = char.IsWhiteSpace(c);
                if (isSpace)
                {
                    if (sb.Length == 0 || lastWasSpace) continue;   // 앞 공백 · 연속 공백은 버립니다
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(c);
                }

                lastWasSpace = isSpace;
            }

            string text = sb.ToString().TrimEnd();

            int max = Mathf.Max(1, ArcadeConfig.Instance.nicknameMaxLength);
            if (text.Length > max) text = text.Substring(0, max);

            return text;
        }

        /// <summary>PlayerPrefs 에서 다시 읽습니다. 자체 점검이 값을 되돌린 뒤에 부릅니다.</summary>
        public static void Reload()
        {
            _loaded = false;
            Load();
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            _serverId = PlayerPrefs.GetString(ServerIdKey, "");
            _nickname = PlayerPrefs.GetString(NicknameKey, "");
            _localId  = PlayerPrefs.GetString(LocalIdKey, "");
            _registered = PlayerPrefs.GetString(RegisteredKey, "");

            if (string.IsNullOrEmpty(_localId))
            {
                _localId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(LocalIdKey, _localId);
                PlayerPrefs.Save();
            }
        }
    }
}
