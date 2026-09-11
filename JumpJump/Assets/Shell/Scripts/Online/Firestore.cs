using System;
using System.Collections.Generic;
using System.Globalization;

namespace Arcade.Online
{
    /// <summary>
    /// **Firestore(점수를 담아 두는 서버 저장소)에 읽고 쓰는 도구**입니다.
    /// Firebase SDK 대신 Firestore 의 웹 주소(REST)로 직접 요청합니다.
    ///
    /// 저장소는 폴더와 문서로 되어 있습니다 — <c>ranks/jumpjump/scores/{PID}</c> 처럼
    /// "폴더/문서/폴더/문서" 가 번갈아 옵니다. 여기서는 그 경로를 글자 배열로 받습니다.
    ///
    /// 쓰기는 전부 <see cref="Commit"/> 한 가지로 합니다. 여러 문서를 **한 번에** 고치고,
    /// 하나라도 안 되면 전부 없던 일이 됩니다. 별명을 잡는 일(빈자리 확인 + 내 것으로 표시 +
    /// 쓰던 별명 놓기)이 반드시 한 번에 일어나야 해서 이 방식을 씁니다.
    /// </summary>
    public class Firestore
    {
        readonly string _projectId;
        readonly string _apiKey;
        readonly FirebaseAuth _auth;

        public Firestore(string projectId, string apiKey, FirebaseAuth auth)
        {
            _projectId = projectId;
            _apiKey = apiKey;
            _auth = auth;
        }

        /// <summary>저장소의 뿌리 이름. 문서의 "정식 이름"은 전부 이것으로 시작합니다.</summary>
        string Root => "projects/" + _projectId + "/databases/(default)/documents";

        string BaseUrl => "https://firestore.googleapis.com/v1/" + Root;

        /// <summary>문서의 정식 이름 (쓰기 요청 안에 적는 값).</summary>
        public string Name(params string[] path)
        {
            return Root + "/" + string.Join("/", path);
        }

        /// <summary>문서의 웹 주소. 한글 별명도 들어가므로 칸마다 주소용 글자로 바꿉니다.</summary>
        string Url(string[] path, string suffix = "")
        {
            var parts = new string[path.Length];
            for (int i = 0; i < path.Length; i++) parts[i] = Uri.EscapeDataString(path[i]);
            return BaseUrl + (parts.Length > 0 ? "/" + string.Join("/", parts) : "") + suffix + "?key=" + _apiKey;
        }

        // ------------------------------------------------------------ 요청

        /// <summary>문서 하나를 읽습니다. 없으면 404 가 돌아옵니다.</summary>
        public void Get(string[] path, Action<HttpResponse> done)
        {
            Send("GET", Url(path), null, done);
        }

        /// <summary>여러 쓰기를 **한 번에** 합니다. 하나라도 거절되면 아무것도 바뀌지 않습니다.</summary>
        public void Commit(List<object> writes, Action<HttpResponse> done)
        {
            var body = new Dictionary<string, object> { { "writes", writes } };
            Send("POST", BaseUrl + ":commit?key=" + _apiKey, MiniJson.Serialize(body), done);
        }

        /// <summary>
        /// <paramref name="parent"/> 아래 <paramref name="collection"/> 폴더를 점수 높은 순으로 <paramref name="limit"/> 개.
        /// </summary>
        public void TopByField(string[] parent, string collection, string field, int limit, Action<HttpResponse> done)
        {
            var query = new Dictionary<string, object>
            {
                { "from", new List<object> { new Dictionary<string, object> { { "collectionId", collection } } } },
                { "orderBy", new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "field", new Dictionary<string, object> { { "fieldPath", field } } },
                            { "direction", "DESCENDING" },
                        },
                    }
                },
                { "limit", limit },
            };

            var body = new Dictionary<string, object> { { "structuredQuery", query } };
            Send("POST", Url(parent, ":runQuery"), MiniJson.Serialize(body), done);
        }

        /// <summary>
        /// <paramref name="field"/> 가 <paramref name="value"/> 보다 큰 문서가 몇 개인지 셉니다 — 내 순위 = 이 수 + 1.
        /// 문서를 하나하나 받아 오지 않고 서버가 세어 주므로 사람이 많아도 싸게 끝납니다.
        /// </summary>
        public void CountGreater(string[] parent, string collection, string field, long value, Action<HttpResponse> done)
        {
            var query = new Dictionary<string, object>
            {
                { "from", new List<object> { new Dictionary<string, object> { { "collectionId", collection } } } },
                { "where", new Dictionary<string, object>
                    {
                        { "fieldFilter", new Dictionary<string, object>
                            {
                                { "field", new Dictionary<string, object> { { "fieldPath", field } } },
                                { "op", "GREATER_THAN" },
                                { "value", Int(value) },
                            }
                        },
                    }
                },
            };

            var body = new Dictionary<string, object>
            {
                { "structuredAggregationQuery", new Dictionary<string, object>
                    {
                        { "structuredQuery", query },
                        { "aggregations", new List<object>
                            {
                                new Dictionary<string, object> { { "alias", "higher" }, { "count", new Dictionary<string, object>() } },
                            }
                        },
                    }
                },
            };

            Send("POST", Url(parent, ":runAggregationQuery"), MiniJson.Serialize(body), done);
        }

        /// <summary>로그인 통행증을 붙여서 보냅니다. 로그인이 안 돼도 읽기는 되므로 통행증 없이라도 보냅니다.</summary>
        void Send(string method, string url, string body, Action<HttpResponse> done)
        {
            _auth.EnsureSignedIn(ok => Http.SendJson(method, url, body, ok ? _auth.IdToken : null, done));
        }

        // ------------------------------------------------------------ 쓰기 한 건 만들기

        /// <summary>
        /// 문서를 이 칸들로 **통째로** 씁니다 (없으면 만들고, 있으면 덮어씁니다).
        /// </summary>
        /// <param name="mustExist">true = 이미 있어야만 / false = 없어야만 / null = 상관없음</param>
        /// <param name="stampTime">true 면 <c>at</c> 칸에 서버 시각을 찍습니다 (폰 시계가 틀려도 정확합니다)</param>
        public Dictionary<string, object> SetWrite(string[] path, Dictionary<string, object> fields,
                                                   bool? mustExist = null, bool stampTime = true)
        {
            var write = new Dictionary<string, object>
            {
                { "update", new Dictionary<string, object> { { "name", Name(path) }, { "fields", fields } } },
            };

            if (mustExist.HasValue)
                write["currentDocument"] = new Dictionary<string, object> { { "exists", mustExist.Value } };

            if (stampTime)
                write["updateTransforms"] = new List<object>
                {
                    new Dictionary<string, object> { { "fieldPath", "at" }, { "setToServerValue", "REQUEST_TIME" } },
                };

            return write;
        }

        /// <summary>문서를 지웁니다. 없는 문서를 지워도 실패하지 않습니다.</summary>
        public Dictionary<string, object> DeleteWrite(string[] path)
        {
            return new Dictionary<string, object> { { "delete", Name(path) } };
        }

        // ------------------------------------------------------------ 값 변환

        /// <summary>Firestore 는 칸마다 값의 종류를 적어야 합니다. 글자 칸.</summary>
        public static Dictionary<string, object> Str(string value)
        {
            return new Dictionary<string, object> { { "stringValue", value ?? "" } };
        }

        /// <summary>정수 칸. 큰 수가 깨지지 않도록 Firestore 는 정수를 **글자로** 주고받습니다.</summary>
        public static Dictionary<string, object> Int(long value)
        {
            return new Dictionary<string, object> { { "integerValue", value.ToString(CultureInfo.InvariantCulture) } };
        }

        /// <summary>문서(JSON)에서 글자 칸 하나를 꺼냅니다. 없으면 null.</summary>
        public static string GetString(object document, string field)
        {
            return MiniJson.DigString(document, "fields", field, "stringValue");
        }

        /// <summary>문서(JSON)에서 정수 칸 하나를 꺼냅니다. 없으면 null.</summary>
        public static long? GetInt(object document, string field)
        {
            return IntValue(MiniJson.Dig(document, "fields", field));
        }

        /// <summary><c>{"integerValue": "123"}</c> 모양 하나를 숫자로. 세기(count) 결과를 읽을 때도 씁니다.</summary>
        public static long? IntValue(object value)
        {
            if (!(value is Dictionary<string, object> typed)) return null;

            if (typed.TryGetValue("integerValue", out object raw))
            {
                if (raw is string s && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
                if (raw is long ll) return ll;
            }
            if (typed.TryGetValue("doubleValue", out object d) && d is double dd) return (long)dd;
            return null;
        }

        /// <summary>문서 정식 이름의 마지막 칸 (= 문서 이름. 점수 문서라면 PID).</summary>
        public static string LastSegment(object document)
        {
            string name = MiniJson.DigString(document, "name");
            if (string.IsNullOrEmpty(name)) return null;

            int slash = name.LastIndexOf('/');
            return slash >= 0 ? name.Substring(slash + 1) : name;
        }
    }
}
