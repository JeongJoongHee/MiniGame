using System;
using System.Collections.Generic;
using System.Text;
using Arcade.Online;
using UnityEditor;
using UnityEngine;

namespace Arcade.EditorTools
{
    /// <summary>
    /// **진짜 Firebase 서버에 붙어서** 랭킹이 도는지 확인합니다. (자체 점검은 서버 없이 규칙만 봅니다)
    ///
    /// 시험 계정 두 개(A · B)를 익명으로 만들어 이것들을 해 봅니다.
    ///  - 로그인 / 별명 잡기 / 점수 올리기 / 랭킹 가져오기 / 낮은 점수는 안 덮어쓰기
    ///  - 별명 바꾸기 (쓰던 별명이 풀리는가, 점수 줄의 별명도 바뀌는가)
    ///  - <b>보안 규칙</b> — B 가 A 의 별명을 잡거나, A 의 점수를 고치거나, A 의 이름으로 점수를 올리면 거절되는가
    /// 끝나면 **시험 기록과 시험 계정을 전부 지웁니다.** 기록은 <c>ranks/selftest</c> 라는 시험용 게임 칸에만 씁니다.
    ///
    /// 배치 모드에서는 <c>-quit</c> 없이 돌려야 합니다 (서버 답을 기다리는 동안 에디터가 살아 있어야 해서).
    /// 끝나면 스스로 에디터를 닫습니다.
    /// </summary>
    public static class ArcadeOnlineTest
    {
        const string Game = "selftest";

        /// <summary>시험이 건드리는 PlayerPrefs. 끝나면 전부 원래대로 되돌립니다.</summary>
        static readonly string[] Keys =
        {
            "Arcade.Auth.Refresh", "Arcade.ServerId", "Arcade.Nickname", "Arcade.NicknameRegistered",
            "Arcade.Rank.Games", "Arcade.Rank." + Game + ".Sent",
        };
        const string BestKey = "Arcade.Rank." + Game + ".Best";

        static readonly Queue<Action> Steps = new Queue<Action>();
        static readonly StringBuilder Log = new StringBuilder();
        static int _failed;
        static double _deadline;

        static Dictionary<string, string> _savedStrings;
        static bool _hadBest;
        static int _savedBest;

        static FirebaseRankingService _a, _b;
        static string _uidA, _uidB, _nickA, _nickA2, _nickB;

        [MenuItem("Tools/Arcade/Run Online Ranking Test (서버 사용)", priority = 25)]
        public static void Run()
        {
            ShellFirebaseConfig.Sync();
            ShellStringsCsv.Sync();
            var config = ShellConfigAsset.LoadOrCreate();
            if (!config.HasFirebase)
            {
                Debug.LogError("[Arcade 서버 점검] Firebase 설정이 비어 있습니다. google-services.json 을 확인하세요.");
                Quit(1);
                return;
            }

            Save();
            Log.Clear();
            _failed = 0;
            Steps.Clear();

            string tag = UnityEngine.Random.Range(1000, 9999).ToString();
            _nickA = "점검A" + tag;
            _nickA2 = "Test" + tag;   // 영문 — 대소문자만 다른 별명이 막히는지 보려고
            _nickB = "점검B" + tag;

            _a = new FirebaseRankingService(config.firebaseProjectId, config.firebaseApiKey);
            _b = new FirebaseRankingService(config.firebaseProjectId, config.firebaseApiKey);
            Log.Append("프로젝트: ").Append(config.firebaseProjectId).Append('\n');

            // ---- A : 로그인 · 별명 · 점수 --------------------------------------------------
            Step(() =>
            {
                ForgetLogin();
                _a.Auth.EnsureSignedIn(ok =>
                {
                    _uidA = _a.Auth.Uid;
                    Check("A 익명 로그인  -> " + Short(_uidA) + (ok ? "" : "  (" + _a.Auth.LastError + ")"), ok);
                    if (!ok) Abort();
                    Next();
                });
            });

            Step(() => _a.ReserveNickname(_nickA, r => { Check("A 별명 잡기 \"" + _nickA + "\"  -> " + r, r == NicknameResult.Ok); Next(); }));

            Step(() =>
            {
                PlayerPrefs.DeleteKey(BestKey);
                _a.Submit(Game, PlayerIdentity.Nickname, 1234, r => { Check("A 점수 1234 올리기  -> " + r, r == SubmitResult.Ok); Next(); });
            });

            Step(() => _a.Fetch(Game, 8, page =>
            {
                Check("랭킹을 가져오면 A 가 1234 로 보인다  -> 내 순위 " + page.myRank + " / " + page.myScore,
                      page.ok && page.myScore == 1234 && page.myRank >= 1 && HasRow(page, _nickA, 1234));
                Next();
            }));

            Step(() => _a.Submit(Game, PlayerIdentity.Nickname, 1000, r => { Check("더 낮은 점수 1000 은 기록을 안 바꾼다  -> " + r, r == SubmitResult.Ok); Next(); }));

            Step(() => Read(_a, new[] { "ranks", Game, "scores", _uidA }, doc =>
            {
                Check("서버의 A 점수는 여전히 1234", Firestore.GetInt(doc, "score") == 1234);
                Next();
            }));

            // ---- A : 별명 바꾸기 --------------------------------------------------------
            Step(() => _a.ReserveNickname(_nickA2, r => { Check("A 별명 바꾸기 -> \"" + _nickA2 + "\"  -> " + r, r == NicknameResult.Ok); Next(); }));

            Step(() => Wait(2.0, Next));   // 바꾼 뒤 점수 줄의 별명을 고치는 요청이 끝날 때까지

            Step(() => Read(_a, new[] { "nicknames", PlayerIdentity.KeyOf(_nickA) }, doc =>
            {
                Check("바꾸기 전 별명 \"" + _nickA + "\" 은 풀렸다", doc == null);
                Next();
            }));

            Step(() => Read(_a, new[] { "ranks", Game, "scores", _uidA }, doc =>
            {
                Check("점수 줄의 별명도 새 별명으로 바뀌었다  -> \"" + Firestore.GetString(doc, "nick") + "\"",
                      Firestore.GetString(doc, "nick") == _nickA2);
                Next();
            }));

            // ---- B : 겹치는 별명 · 보안 규칙 ---------------------------------------------
            Step(() =>
            {
                ForgetLogin();   // A 의 로그인은 _a 가 메모리에 들고 있습니다. 폰 저장만 비워 B 를 새로 만듭니다
                _b.Auth.EnsureSignedIn(ok =>
                {
                    _uidB = _b.Auth.Uid;
                    Check("B 익명 로그인 (A 와 다른 번호)  -> " + Short(_uidB), ok && _uidB != _uidA);
                    if (!ok) Abort();
                    Next();
                });
            });

            Step(() => _b.ReserveNickname(_nickA2.ToUpperInvariant(), r =>
            {
                Check("B 가 A 의 별명(대소문자만 다르게)을 잡으면 \"이미 있는 별명\"  -> " + r, r == NicknameResult.Taken);
                Next();
            }));

            Step(() => _b.ReserveNickname(_nickA, r => { Check("B 는 A 가 놓은 별명 \"" + _nickA + "\" 을 잡을 수 있다  -> " + r, r == NicknameResult.Ok); _nickB = _nickA; Next(); }));

            Step(() => Commit(_b, _b.Db.SetWrite(new[] { "ranks", Game, "scores", _uidA },
                                                  Fields(_nickB, 999999)), ok =>
            {
                Check("[보안] B 가 A 의 점수 줄을 고치면 거절된다", !ok);
                Next();
            }));

            Step(() => Commit(_b, _b.Db.SetWrite(new[] { "ranks", Game, "scores", _uidB },
                                                  Fields(_nickA2, 5000)), ok =>
            {
                Check("[보안] B 가 A 의 별명으로 점수를 올리면 거절된다", !ok);
                Next();
            }));

            Step(() => Commit(_b, _b.Db.DeleteWrite(new[] { "nicknames", PlayerIdentity.KeyOf(_nickA2) }), ok =>
            {
                Check("[보안] B 가 A 의 별명 자리를 지우면 거절된다", !ok);
                Next();
            }));

            // ---- 치우기 ---------------------------------------------------------------
            // (B 도 별명을 잡는 순간 폰에 남은 시험 기록이 B 이름으로 올라가므로 점수 줄까지 지웁니다)
            Step(() => CleanUp(_b, _uidB, _nickB, () => CleanUp(_a, _uidA, _nickA2, Next)));

            Step(Finish);

            _deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Next();
        }

        // ------------------------------------------------------------------ 단계 진행

        static void Step(Action action) => Steps.Enqueue(action);

        static void Next()
        {
            if (Steps.Count == 0) return;
            Steps.Dequeue()();
        }

        static void Abort()
        {
            // 로그인이 안 되면 뒤의 단계는 의미가 없습니다. 치우기와 마무리만 남깁니다.
            Steps.Clear();
            Step(Finish);
        }

        static void Tick()
        {
            Http.Pump();
            if (_waitUntil > 0 && EditorApplication.timeSinceStartup >= _waitUntil)
            {
                _waitUntil = 0;
                var then = _afterWait;
                _afterWait = null;
                then?.Invoke();
            }

            if (EditorApplication.timeSinceStartup > _deadline)
            {
                Check("시간 안에 끝났다 (120초)", false);
                Steps.Clear();
                Finish();
            }
        }

        static double _waitUntil;
        static Action _afterWait;

        static void Wait(double seconds, Action then)
        {
            _afterWait = then;
            _waitUntil = EditorApplication.timeSinceStartup + seconds;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Restore();

            if (_failed == 0) Debug.Log("[Arcade 서버 점검] 전부 통과\n" + Log);
            else Debug.LogError("[Arcade 서버 점검] " + _failed + "건 실패\n" + Log);

            Quit(_failed == 0 ? 0 : 1);
        }

        static void Quit(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ 도우미

        static void Check(string what, bool ok)
        {
            Log.Append(ok ? "  통과  " : "  실패  ").Append(what).Append('\n');
            if (!ok) _failed++;
        }

        static void Read(FirebaseRankingService service, string[] path, Action<object> done)
        {
            service.Db.Get(path, r => done(r.Ok ? r.Json : null));
        }

        /// <summary>
        /// 쓰기 한 건을 해 보고 **성공했으면 true.** 거절된 경우는 서버가 규칙 때문에 막았을 때
        /// (<c>PERMISSION_DENIED</c>)만 false 로 칩니다 — 인터넷이 잠깐 끊겨서 실패한 것을
        /// "규칙이 막았다"로 잘못 세지 않으려는 것입니다. 그 밖의 실패는 성공(true)으로 돌려 검사가 실패하게 합니다.
        /// </summary>
        static void Commit(FirebaseRankingService service, object write, Action<bool> done)
        {
            service.Db.Commit(new List<object> { write }, r =>
            {
                if (!r.Ok && r.Error != "PERMISSION_DENIED")
                    Log.Append("        (규칙이 아닌 다른 이유로 실패: ").Append(r).Append(")\n");
                done(r.Ok || r.Error != "PERMISSION_DENIED");
            });
        }

        static Dictionary<string, object> Fields(string nick, long score)
        {
            return new Dictionary<string, object> { { "nick", Firestore.Str(nick) }, { "score", Firestore.Int(score) } };
        }

        static bool HasRow(RankingPage page, string nick, int score)
        {
            foreach (var row in page.rows) if (row.nickname == nick && row.score == score && row.isMe) return true;
            return false;
        }

        /// <summary>시험 계정의 기록(점수 · 별명 · 사람)을 한 번에 지우고, 계정도 지웁니다.</summary>
        static void CleanUp(FirebaseRankingService service, string uid, string nick, Action then)
        {
            if (string.IsNullOrEmpty(uid) || !service.Auth.SignedIn) { then(); return; }

            var writes = new List<object>
            {
                service.Db.DeleteWrite(new[] { "users", uid }),
                service.Db.DeleteWrite(new[] { "nicknames", PlayerIdentity.KeyOf(nick) }),
                service.Db.DeleteWrite(new[] { "ranks", Game, "scores", uid }),
            };

            service.Db.Commit(writes, deleted =>
            {
                Check("시험 기록 지우기 (" + Short(uid) + ")", deleted.Ok);
                service.Auth.DeleteAccount(ok =>
                {
                    Check("시험 계정 지우기 (" + Short(uid) + ")", ok);
                    then();
                });
            });
        }

        static void ForgetLogin()
        {
            PlayerPrefs.DeleteKey("Arcade.Auth.Refresh");
            PlayerPrefs.Save();
        }

        static string Short(string id)
        {
            return string.IsNullOrEmpty(id) ? "(없음)" : id.Length <= 8 ? id : id.Substring(0, 8) + "...";
        }

        static void Save()
        {
            _savedStrings = new Dictionary<string, string>();
            foreach (var key in Keys)
                if (PlayerPrefs.HasKey(key)) _savedStrings[key] = PlayerPrefs.GetString(key, "");

            _hadBest = PlayerPrefs.HasKey(BestKey);
            _savedBest = PlayerPrefs.GetInt(BestKey, 0);
        }

        static void Restore()
        {
            foreach (var key in Keys)
            {
                if (_savedStrings != null && _savedStrings.TryGetValue(key, out string value)) PlayerPrefs.SetString(key, value);
                else PlayerPrefs.DeleteKey(key);
            }

            if (_hadBest) PlayerPrefs.SetInt(BestKey, _savedBest);
            else PlayerPrefs.DeleteKey(BestKey);

            PlayerPrefs.Save();
            PlayerIdentity.Reload();
        }
    }
}
