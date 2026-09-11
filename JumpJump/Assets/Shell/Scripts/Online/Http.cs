using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Arcade.Online
{
    /// <summary>서버가 돌려준 답 한 개.</summary>
    public class HttpResponse
    {
        /// <summary>HTTP 상태 번호. 200 번대면 성공입니다. 인터넷이 끊겼으면 0.</summary>
        public long code;

        /// <summary>답의 본문 (대개 JSON 글자).</summary>
        public string text;

        /// <summary>서버에 닿지도 못했는지 (비행기 모드 · 시간 초과 등).</summary>
        public bool networkError;

        object _json;
        bool _parsed;

        public bool Ok => !networkError && code >= 200 && code < 300;

        /// <summary>본문을 JSON 으로 읽은 것. 한 번만 읽고 기억해 둡니다.</summary>
        public object Json
        {
            get
            {
                if (!_parsed) { _json = MiniJson.Parse(text); _parsed = true; }
                return _json;
            }
        }

        /// <summary>
        /// 실패했을 때 서버가 알려 준 까닭. Firestore 는 <c>PERMISSION_DENIED</c> 같은 상태 이름을,
        /// 로그인 서버는 <c>TOKEN_EXPIRED</c> 같은 메시지를 줍니다. 로그와 판정에 씁니다.
        /// </summary>
        public string Error
        {
            get
            {
                if (networkError) return "NETWORK";

                object root = Json;
                if (root is List<object> list && list.Count > 0) root = list[0];   // runQuery 는 배열로 답합니다

                return MiniJson.DigString(root, "error", "status")
                       ?? MiniJson.DigString(root, "error", "message")
                       ?? (Ok ? "" : "HTTP " + code);
            }
        }

        /// <summary>
        /// 서버가 적어 보낸 설명 글. 로그인 서버는 까닭을 여기에 적습니다
        /// (<c>TOKEN_EXPIRED</c>, <c>OPERATION_NOT_ALLOWED : ...</c> 등). 없으면 <see cref="Error"/>.
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                if (networkError) return "NETWORK";

                object root = Json;
                if (root is List<object> list && list.Count > 0) root = list[0];
                return MiniJson.DigString(root, "error", "message") ?? Error;
            }
        }

        /// <summary>로그 한 줄로 보기 좋게.</summary>
        public override string ToString()
        {
            return networkError ? "인터넷 연결 실패" : "HTTP " + code + " " + Error;
        }
    }

    /// <summary>
    /// **서버에 요청을 보내고 답을 받는 가장 아래층**입니다. Unity 에 들어 있는
    /// <c>UnityWebRequest</c> 만 씁니다 — Firebase SDK 를 넣지 않아도 되는 이유가 이것입니다.
    ///
    /// 답이 왔는지는 <see cref="Pump"/> 가 매 프레임 확인합니다. 게임 중에는 <see cref="OnlinePump"/>
    /// 가 부르고, 배치 모드 점검에서는 에디터가 부릅니다. 그래서 **게임 화면과 배치 점검이 같은 코드**로 돕니다.
    /// </summary>
    public static class Http
    {
        /// <summary>이 시간 안에 답이 없으면 실패로 봅니다 (초). 폰이 느린 망에 있어도 게임이 오래 기다리지 않게 합니다.</summary>
        public static int TimeoutSeconds = 12;

        static readonly List<(UnityWebRequest request, Action<HttpResponse> done)> Pending =
            new List<(UnityWebRequest, Action<HttpResponse>)>();

        /// <summary>아직 답을 기다리는 요청 수.</summary>
        public static int PendingCount => Pending.Count;

        /// <summary>JSON 을 보냅니다. <paramref name="body"/> 가 null 이면 본문 없이 보냅니다 (GET).</summary>
        public static void SendJson(string method, string url, string body, string bearer, Action<HttpResponse> done)
        {
            Send(method, url, body, "application/json", bearer, done);
        }

        public static void Send(string method, string url, string body, string contentType, string bearer,
                                Action<HttpResponse> done)
        {
            var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };

            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", contentType);
            }

            if (!string.IsNullOrEmpty(bearer))
                request.SetRequestHeader("Authorization", "Bearer " + bearer);

            try
            {
                request.SendWebRequest();
            }
            catch (Exception e)
            {
                request.Dispose();
                Debug.LogWarning("[Arcade] 요청을 보내지 못했습니다: " + e.Message);
                done?.Invoke(new HttpResponse { networkError = true, text = "" });
                return;
            }

            Pending.Add((request, done));
        }

        /// <summary>
        /// 끝난 요청을 찾아 답을 넘겨줍니다. **매 프레임 한 번씩 불러야 합니다.**
        /// 답을 받은 쪽이 곧바로 다음 요청을 보내도 괜찮도록, 끝난 것을 먼저 목록에서 뺀 뒤에 알립니다.
        /// </summary>
        public static void Pump()
        {
            if (Pending.Count == 0) return;

            var finished = new List<(UnityWebRequest, Action<HttpResponse>)>();
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                if (!Pending[i].request.isDone) continue;
                finished.Add(Pending[i]);
                Pending.RemoveAt(i);
            }

            for (int i = finished.Count - 1; i >= 0; i--)
            {
                var (request, done) = finished[i];

                var response = new HttpResponse
                {
                    code = request.responseCode,
                    text = request.downloadHandler != null ? request.downloadHandler.text : "",
                    networkError = request.result == UnityWebRequest.Result.ConnectionError
                                   || request.result == UnityWebRequest.Result.DataProcessingError
                                   || request.responseCode == 0,
                };
                request.Dispose();

                // 받는 쪽에서 예외가 나도 다른 답은 계속 넘겨줘야 합니다.
                try { done?.Invoke(response); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
