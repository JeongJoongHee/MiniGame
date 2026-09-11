using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcade.Online
{
    /// <summary>
    /// **Firebase 익명 로그인**입니다. 사용자는 아무것도 하지 않고, 앱이 알아서 사람마다 다른
    /// 고유 번호(UID)를 받아 옵니다. 그 번호가 랭킹의 PID 가 됩니다 (<see cref="PlayerIdentity.UseServerId"/>).
    ///
    /// 한 번 받은 번호는 폰에 남겨 둔 "갱신 열쇠"(refresh token)로 계속 이어 씁니다.
    /// **앱을 지웠다 다시 깔면 새 번호가 됩니다** — 익명 로그인의 성질입니다 (구글 로그인을 붙이면 이어집니다).
    ///
    /// 서버에 무언가를 쓸 때는 "통행증"(ID 토큰)을 같이 보내야 하는데, 통행증은 1시간짜리라
    /// 끝나기 전에 갱신 열쇠로 새로 받습니다. 그 일을 전부 여기서 합니다.
    /// </summary>
    public class FirebaseAuth
    {
        const string RefreshKey = "Arcade.Auth.Refresh";

        /// <summary>갱신 열쇠가 더는 안 통할 때 서버가 주는 말들. 이때는 새로 로그인합니다.</summary>
        static readonly string[] DeadRefreshErrors =
        {
            "TOKEN_EXPIRED", "USER_DISABLED", "USER_NOT_FOUND", "INVALID_REFRESH_TOKEN", "PROJECT_NUMBER_MISMATCH",
        };

        readonly string _apiKey;
        readonly List<Action<bool>> _waiting = new List<Action<bool>>();

        string _idToken;
        DateTime _expiresUtc;
        bool _busy;

        public FirebaseAuth(string apiKey)
        {
            _apiKey = apiKey;
        }

        /// <summary>로그인해서 받은 번호. 아직 로그인 전이면 null.</summary>
        public string Uid { get; private set; }

        /// <summary>통행증이 살아 있는지.</summary>
        public bool SignedIn => !string.IsNullOrEmpty(_idToken) && DateTime.UtcNow < _expiresUtc;

        /// <summary>서버에 보낼 통행증. 로그인 전이면 null.</summary>
        public string IdToken => SignedIn ? _idToken : null;

        /// <summary>마지막으로 실패한 까닭 (로그용). 익명 로그인이 꺼져 있으면 <c>OPERATION_NOT_ALLOWED</c> 등.</summary>
        public string LastError { get; private set; }

        /// <summary>
        /// 로그인되어 있게 합니다. 이미 되어 있으면 곧바로 true, 아니면 서버에 다녀와서 알려 줍니다.
        /// 여러 곳에서 동시에 불러도 서버에는 한 번만 갑니다.
        /// </summary>
        public void EnsureSignedIn(Action<bool> done)
        {
            // 통행증이 1분 넘게 남아 있으면 그대로 씁니다.
            if (SignedIn && (_expiresUtc - DateTime.UtcNow).TotalSeconds > 60)
            {
                done?.Invoke(true);
                return;
            }

            if (done != null) _waiting.Add(done);
            if (_busy) return;
            _busy = true;

            string refresh = PlayerPrefs.GetString(RefreshKey, "");
            if (refresh.Length > 0) Refresh(refresh);
            else SignUp();
        }

        void SignUp()
        {
            string url = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + _apiKey;
            Http.SendJson("POST", url, "{\"returnSecureToken\":true}", null, response =>
            {
                if (!response.Ok)
                {
                    Fail("익명 로그인", response);
                    return;
                }

                var json = response.Json;
                Accept(MiniJson.DigString(json, "idToken"),
                       MiniJson.DigString(json, "refreshToken"),
                       MiniJson.DigString(json, "expiresIn"),
                       MiniJson.DigString(json, "localId"));
            });
        }

        void Refresh(string refreshToken)
        {
            string url = "https://securetoken.googleapis.com/v1/token?key=" + _apiKey;
            string form = "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refreshToken);

            Http.Send("POST", url, form, "application/x-www-form-urlencoded", null, response =>
            {
                if (!response.Ok)
                {
                    // 열쇠가 죽었으면(계정이 지워졌거나 막혔으면) 새로 로그인합니다. 새 번호가 됩니다.
                    if (!response.networkError && IsDeadRefresh(response.ErrorMessage))
                    {
                        Debug.LogWarning("[Arcade] 로그인 정보가 더는 유효하지 않아 새로 로그인합니다: " + response.ErrorMessage);
                        PlayerPrefs.DeleteKey(RefreshKey);
                        PlayerPrefs.Save();
                        SignUp();
                        return;
                    }

                    Fail("로그인 갱신", response);
                    return;
                }

                var json = response.Json;
                Accept(MiniJson.DigString(json, "id_token"),
                       MiniJson.DigString(json, "refresh_token"),
                       MiniJson.DigString(json, "expires_in"),
                       MiniJson.DigString(json, "user_id"));
            });
        }

        void Accept(string idToken, string refreshToken, string expiresIn, string uid)
        {
            if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(uid))
            {
                LastError = "EMPTY_RESPONSE";
                Finish(false);
                return;
            }

            int seconds = int.TryParse(expiresIn, out int s) ? s : 3600;

            _idToken = idToken;
            _expiresUtc = DateTime.UtcNow.AddSeconds(seconds);
            Uid = uid;
            LastError = null;

            if (!string.IsNullOrEmpty(refreshToken))
            {
                PlayerPrefs.SetString(RefreshKey, refreshToken);
                PlayerPrefs.Save();
            }

            PlayerIdentity.UseServerId(uid);
            Finish(true);
        }

        void Fail(string what, HttpResponse response)
        {
            LastError = response.ErrorMessage;
            bool disabled = LastError.StartsWith("OPERATION_NOT_ALLOWED", StringComparison.Ordinal)
                            || LastError.StartsWith("ADMIN_ONLY_OPERATION", StringComparison.Ordinal);
            Debug.LogWarning("[Arcade] " + what + " 실패 — " + response + " / " + LastError +
                             (disabled
                                 ? "\n  Firebase 콘솔 > Authentication > 로그인 방법에서 '익명' 을 켜 주세요."
                                 : ""));
            Finish(false);
        }

        void Finish(bool ok)
        {
            _busy = false;

            var waiting = new List<Action<bool>>(_waiting);
            _waiting.Clear();
            foreach (var done in waiting) done(ok);
        }

        static bool IsDeadRefresh(string error)
        {
            if (string.IsNullOrEmpty(error)) return false;
            foreach (var dead in DeadRefreshErrors)
                if (error.StartsWith(dead, StringComparison.Ordinal)) return true;
            return false;
        }

        // ------------------------------------------------------------ 점검용

        /// <summary>
        /// **지금 로그인한 계정을 서버에서 지웁니다.** 배치 점검이 스스로 만든 시험 계정을
        /// 치울 때만 씁니다. 게임에서는 부르지 않습니다.
        /// </summary>
        public void DeleteAccount(Action<bool> done)
        {
            if (!SignedIn) { done?.Invoke(false); return; }

            string url = "https://identitytoolkit.googleapis.com/v1/accounts:delete?key=" + _apiKey;
            string body = MiniJson.Serialize(new Dictionary<string, object> { { "idToken", _idToken } });
            Http.SendJson("POST", url, body, null, response =>
            {
                if (response.Ok) Forget();
                done?.Invoke(response.Ok);
            });
        }

        /// <summary>폰에 남긴 로그인 정보를 지웁니다 (점검용). 다음 로그인은 새 번호가 됩니다.</summary>
        public void Forget()
        {
            _idToken = null;
            Uid = null;
            PlayerPrefs.DeleteKey(RefreshKey);
            PlayerPrefs.Save();
        }
    }
}
