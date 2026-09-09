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

        static string _localId;
        static string _serverId;
        static string _nickname;
        static bool _loaded;

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
            }
        }

        /// <summary>
        /// 별명을 쓸 수 있는 모양으로 다듬습니다.
        /// 앞뒤 공백을 없애고, 줄바꿈 같은 보이지 않는 글자를 빼고, 길이를 잘라냅니다.
        /// **남들에게 보이는 글자이므로 길이 제한은 반드시 필요합니다.**
        /// (욕설 거르기는 아직 없습니다 — 랭킹을 실제로 열 때 붙일 것)
        /// </summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            var sb = new StringBuilder(raw.Length);
            bool lastWasSpace = false;

            foreach (char c in raw)
            {
                if (char.IsControl(c)) continue;

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

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            _serverId = PlayerPrefs.GetString(ServerIdKey, "");
            _nickname = PlayerPrefs.GetString(NicknameKey, "");
            _localId  = PlayerPrefs.GetString(LocalIdKey, "");

            if (string.IsNullOrEmpty(_localId))
            {
                _localId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(LocalIdKey, _localId);
                PlayerPrefs.Save();
            }
        }
    }
}
