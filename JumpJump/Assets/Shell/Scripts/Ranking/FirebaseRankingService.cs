using System;
using System.Collections.Generic;
using Arcade.Online;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **랭킹을 Firebase 서버에 올리고 가져오는 구현**입니다 (2단계).
    /// 앱이 켜질 때 <see cref="OnlineBoot"/> 가 <see cref="Services.Use(IRankingService)"/> 로 끼워 넣습니다.
    /// 게임 코드와 화면 코드는 이것이 서버인지 폰 안인지 모릅니다.
    ///
    /// 서버에는 폴더 세 개가 있습니다.
    /// <code>
    ///   ranks/{게임}/scores/{PID}  -> { nick: "홍길동", score: 47900, at: 시각 }   한 사람당 한 줄 = 최고 점수
    ///   users/{PID}                -> { nick: "홍길동", at: 시각 }                 지금 쓰는 별명
    ///   nicknames/{별명(소문자)}    -> { uid: PID }                                 이 별명의 주인
    /// </code>
    /// <b>별명은 앱 전체에서 하나뿐</b>이라 <c>nicknames</c> 폴더를 "자리표"로 씁니다.
    /// 자리를 잡는 일은 "없어야만 만든다"는 조건을 달아 한 번에 처리하므로, 두 사람이 같은 순간에
    /// 같은 별명을 눌러도 한 사람만 잡힙니다. 남의 자리를 지우거나 남의 점수를 고치는 일은
    /// 서버의 **보안 규칙**(작업 폴더의 <c>firestore.rules</c>)이 막습니다.
    ///
    /// <b>인터넷이 끊겨도 기록은 사라지지 않습니다.</b> 점수는 먼저 폰에 적어 두고(<see cref="LocalRankingService.RecordBest"/>),
    /// 서버에 올라간 값을 따로 기억해 둡니다. 둘이 다르면 다음에 연결될 때(앱을 켤 때 · 다음 판 · 랭킹 창을 열 때) 올립니다.
    /// </summary>
    public class FirebaseRankingService : IRankingService
    {
        const string ScoresFolder = "scores";
        const string GamesKey = "Arcade.Rank.Games";

        /// <summary>
        /// 같은 게임의 랭킹을 이 시간 안에 다시 열면 서버에 다시 묻지 않습니다 (초).
        /// 탭을 왔다 갔다 할 때마다 읽기 횟수가 쌓이지 않게 하려는 것입니다 (Firestore 무료 한도는 하루 5만 번).
        /// </summary>
        const float CacheSeconds = 20f;

        readonly FirebaseAuth _auth;
        readonly Firestore _db;
        readonly Dictionary<string, (float time, int count, RankingPage page)> _cache =
            new Dictionary<string, (float, int, RankingPage)>();

        public FirebaseRankingService(string projectId, string apiKey)
        {
            _auth = new FirebaseAuth(apiKey);
            _db = new Firestore(projectId, apiKey, _auth);
        }

        /// <summary>로그인 담당. 배치 점검이 시험 계정을 치울 때 씁니다.</summary>
        public FirebaseAuth Auth => _auth;

        /// <summary>저장소. 배치 점검이 보안 규칙을 시험하고 시험 기록을 치울 때 씁니다.</summary>
        public Firestore Db => _db;

        public bool IsReady => _auth.SignedIn;

        /// <summary>
        /// 앱이 켜질 때 한 번. 미리 로그인해 두고, 지난번에 못 올린 기록이 있으면 올립니다.
        /// 실패해도 아무 일 없습니다 — 다음에 필요할 때 다시 시도합니다.
        /// </summary>
        public void Warmup()
        {
            _auth.EnsureSignedIn(ok =>
            {
                if (ok) FlushAll();
            });
        }

        // ------------------------------------------------------------------ 점수 올리기

        public void Submit(string gameId, string nickname, int score, Action<SubmitResult> done = null)
        {
            if (!LocalRankingService.RecordBest(gameId, score))
            {
                done?.Invoke(SubmitResult.Failed);
                return;
            }

            RememberGame(gameId);
            _cache.Remove(gameId);

            EnsureRegistered(state =>
            {
                if (state != SubmitResult.Ok) { done?.Invoke(state); return; }
                Flush(gameId, done);
            });
        }

        /// <summary>
        /// 점수를 올리기 전에 별명이 서버에 등록되어 있게 합니다.
        /// 1단계 때 폰 안에서만 정한 별명은 여기서 처음 등록되는데, 그새 남이 가져갔으면 NeedNickname 입니다.
        /// </summary>
        void EnsureRegistered(Action<SubmitResult> done)
        {
            _auth.EnsureSignedIn(ok =>
            {
                if (!ok) { done(SubmitResult.Failed); return; }
                if (!PlayerIdentity.HasNickname) { done(SubmitResult.NeedNickname); return; }
                if (PlayerIdentity.IsNicknameRegistered) { done(SubmitResult.Ok); return; }

                // 금지어 표가 나중에 늘어나서 예전 별명이 걸리게 됐을 수도 있습니다.
                if (NicknameRules.Check(PlayerIdentity.Nickname) != NicknameProblem.None)
                {
                    done(SubmitResult.NeedNickname);
                    return;
                }

                ReserveNickname(PlayerIdentity.Nickname, result =>
                {
                    switch (result)
                    {
                        case NicknameResult.Ok: done(SubmitResult.Ok); break;
                        case NicknameResult.Taken: done(SubmitResult.NeedNickname); break;
                        default: done(SubmitResult.Failed); break;
                    }
                });
            });
        }

        /// <summary>폰에 적힌 최고 기록이 서버와 다르면 올립니다. 같으면 아무것도 하지 않습니다.</summary>
        void Flush(string gameId, Action<SubmitResult> done)
        {
            int best = LocalRankingService.BestOf(gameId);
            string nick = PlayerIdentity.Nickname;
            string stamp = SentStamp(best, nick);

            if (best <= 0 || PlayerPrefs.GetString(SentKey(gameId), "") == stamp)
            {
                done?.Invoke(SubmitResult.Ok);
                return;
            }

            if (!PlayerIdentity.IsNicknameRegistered || string.IsNullOrEmpty(_auth.Uid))
            {
                done?.Invoke(SubmitResult.Failed);
                return;
            }

            var fields = new Dictionary<string, object>
            {
                { "nick", Firestore.Str(nick) },
                { "score", Firestore.Int(best) },
            };
            var write = _db.SetWrite(new[] { "ranks", gameId, ScoresFolder, _auth.Uid }, fields);

            _db.Commit(new List<object> { write }, response =>
            {
                if (response.Ok)
                {
                    PlayerPrefs.SetString(SentKey(gameId), stamp);
                    PlayerPrefs.Save();
                    _cache.Remove(gameId);
                    done?.Invoke(SubmitResult.Ok);
                    return;
                }

                Debug.LogWarning("[Arcade] 점수를 서버에 올리지 못했습니다 (" + gameId + " " + best + "점) — " + response);

                // 규칙에 걸렸다면 대개 "서버가 아는 내 별명"과 폰이 아는 별명이 어긋난 것입니다.
                // 등록 표시를 지워 두면 다음 판에 다시 등록하고 올립니다.
                if (response.Error == "PERMISSION_DENIED") PlayerIdentity.ForgetNicknameRegistration();

                done?.Invoke(SubmitResult.Failed);
            });
        }

        /// <summary>한 번이라도 점수를 올린 게임 전부를 차례로 Flush 합니다. (별명을 바꾼 뒤에도 부릅니다)</summary>
        void FlushAll()
        {
            if (!PlayerIdentity.IsNicknameRegistered) return;

            var games = KnownGames();
            void Next(int i)
            {
                if (i >= games.Count) return;
                Flush(games[i], _ => Next(i + 1));
            }
            Next(0);
        }

        // ------------------------------------------------------------------ 랭킹 가져오기

        public void Fetch(string gameId, int count, Action<RankingPage> done)
        {
            if (_cache.TryGetValue(gameId, out var hit) && hit.count == count &&
                Time.realtimeSinceStartup - hit.time < CacheSeconds)
            {
                done?.Invoke(hit.page);
                return;
            }

            // 아직 못 올린 내 기록이 있으면 먼저 올립니다. 그래야 방금 낸 점수가 순위에 바로 보입니다.
            if (PlayerIdentity.IsNicknameRegistered && NeedsFlush(gameId))
                Flush(gameId, _ => Query(gameId, count, done));
            else
                Query(gameId, count, done);
        }

        void Query(string gameId, int count, Action<RankingPage> done)
        {
            var parent = new[] { "ranks", gameId };

            _db.TopByField(parent, ScoresFolder, "score", count, response =>
            {
                if (!response.Ok)
                {
                    Debug.LogWarning("[Arcade] 랭킹을 불러오지 못했습니다 (" + gameId + ") — " + response);
                    done?.Invoke(new RankingPage { ok = false });
                    return;
                }

                string me = _auth.Uid;
                var page = new RankingPage { ok = true };

                int place = 0;
                long previous = long.MinValue;

                if (response.Json is List<object> results)
                {
                    foreach (var item in results)
                    {
                        object doc = MiniJson.Dig(item, "document");
                        if (doc == null) continue;   // 결과가 하나도 없으면 시각만 적힌 줄이 옵니다

                        long score = Firestore.GetInt(doc, "score") ?? 0;
                        int index = page.rows.Count + 1;
                        if (score != previous) place = index;   // 같은 점수는 같은 순위 (1, 2, 2, 4 ...)
                        previous = score;

                        bool isMe = !string.IsNullOrEmpty(me) && Firestore.LastSegment(doc) == me;
                        page.rows.Add(new RankingRow(place, Firestore.GetString(doc, "nick") ?? "?", (int)score, isMe));

                        if (isMe)
                        {
                            page.myRank = place;
                            page.myScore = (int)score;
                        }
                    }
                }

                if (page.myRank > 0 || string.IsNullOrEmpty(me))
                {
                    Finish(gameId, count, page, done);
                    return;
                }

                // 윗줄에 내가 없으면 내 줄을 따로 읽고, 나보다 높은 사람이 몇 명인지 세서 순위를 냅니다.
                FindMyRank(parent, me, page, () => Finish(gameId, count, page, done));
            });
        }

        void FindMyRank(string[] parent, string me, RankingPage page, Action then)
        {
            _db.Get(new[] { parent[0], parent[1], ScoresFolder, me }, mine =>
            {
                long myScore = mine.Ok ? Firestore.GetInt(mine.Json, "score") ?? 0 : 0;
                if (myScore <= 0) { then(); return; }   // 아직 올린 기록이 없습니다

                _db.CountGreater(parent, ScoresFolder, "score", myScore, counted =>
                {
                    if (counted.Ok && counted.Json is List<object> list && list.Count > 0)
                    {
                        long higher = Firestore.IntValue(MiniJson.Dig(list[0], "result", "aggregateFields", "higher")) ?? -1;
                        if (higher >= 0)
                        {
                            page.myRank = (int)higher + 1;
                            page.myScore = (int)myScore;
                        }
                    }
                    then();
                });
            });
        }

        void Finish(string gameId, int count, RankingPage page, Action<RankingPage> done)
        {
            _cache[gameId] = (Time.realtimeSinceStartup, count, page);
            done?.Invoke(page);
        }

        // ------------------------------------------------------------------ 별명

        public void ReserveNickname(string nickname, Action<NicknameResult> done)
        {
            string clean = PlayerIdentity.Sanitize(nickname);
            string key = PlayerIdentity.KeyOf(clean);
            if (!PlayerIdentity.IsValidKey(key))
            {
                done?.Invoke(NicknameResult.Failed);
                return;
            }

            _auth.EnsureSignedIn(ok =>
            {
                if (!ok) { done?.Invoke(NicknameResult.Failed); return; }

                string uid = _auth.Uid;

                // 서버가 아는 "지금 내 별명"부터 봅니다. 바꾸는 중이라면 그 자리를 놓아 줘야 하니까요.
                _db.Get(new[] { "users", uid }, current =>
                {
                    if (!current.Ok && current.code != 404)
                    {
                        Debug.LogWarning("[Arcade] 별명 확인 실패 — " + current);
                        done?.Invoke(NicknameResult.Failed);
                        return;
                    }

                    string oldNick = current.Ok ? Firestore.GetString(current.Json, "nick") : null;
                    string oldKey = string.IsNullOrEmpty(oldNick) ? null : PlayerIdentity.KeyOf(oldNick);

                    Claim(uid, clean, key, oldKey, claimSeat: oldKey != key, done);
                });
            });
        }

        /// <summary>
        /// 별명 자리를 잡습니다 — 아래 세 가지를 **한 번에**. 하나라도 거절되면 아무것도 바뀌지 않습니다.
        ///   1) nicknames/{새 별명} 을 만든다 (<b>이미 있으면 실패</b> — 남이 쓰는 별명)
        ///   2) users/{PID} 의 별명을 바꾼다
        ///   3) nicknames/{쓰던 별명} 을 지운다 (바꾸는 경우만)
        /// </summary>
        void Claim(string uid, string clean, string key, string oldKey, bool claimSeat, Action<NicknameResult> done)
        {
            var writes = new List<object>();

            if (claimSeat)
                writes.Add(_db.SetWrite(new[] { "nicknames", key },
                                        new Dictionary<string, object> { { "uid", Firestore.Str(uid) } },
                                        mustExist: false, stampTime: false));

            writes.Add(_db.SetWrite(new[] { "users", uid },
                                    new Dictionary<string, object> { { "nick", Firestore.Str(clean) } }));

            if (oldKey != null && oldKey != key && PlayerIdentity.IsValidKey(oldKey))
                writes.Add(_db.DeleteWrite(new[] { "nicknames", oldKey }));

            _db.Commit(writes, committed =>
            {
                if (committed.Ok)
                {
                    Succeed(uid, clean, done);
                    return;
                }

                // 왜 안 됐는지 봅니다 — 남이 쓰는 별명인지, 인터넷·규칙 문제인지.
                _db.Get(new[] { "nicknames", key }, seat =>
                {
                    string owner = seat.Ok ? Firestore.GetString(seat.Json, "uid") : null;

                    if (owner != null && owner != uid)
                    {
                        done?.Invoke(NicknameResult.Taken);
                    }
                    else if (owner == uid && claimSeat)
                    {
                        // 자리는 이미 내 것인데 users 쪽만 어긋나 있습니다. 자리 잡기는 빼고 다시 합니다.
                        Claim(uid, clean, key, oldKey, claimSeat: false, done);
                    }
                    else
                    {
                        Debug.LogWarning("[Arcade] 별명을 등록하지 못했습니다 — " + committed +
                                         (committed.Error == "PERMISSION_DENIED"
                                             ? "\n  Firestore 보안 규칙이 아직 안 들어갔을 수 있습니다 (작업 폴더의 firestore.rules)."
                                             : ""));
                        done?.Invoke(NicknameResult.Failed);
                    }
                });
            });
        }

        void Succeed(string uid, string clean, Action<NicknameResult> done)
        {
            PlayerIdentity.Nickname = clean;
            PlayerIdentity.MarkNicknameRegistered(uid, clean);
            _cache.Clear();

            done?.Invoke(NicknameResult.Ok);

            // 이미 올라가 있는 점수 줄들의 별명도 새 것으로 바꿔 둡니다.
            FlushAll();
        }

        // ------------------------------------------------------------------ 내 기록 전부 지우기 (치트)

        /// <summary>
        /// 서버의 내 기록을 **한 번에** 지우고(점수 줄 · 별명 자리 · users), 익명 계정도 지웁니다.
        /// 보안 규칙이 "내 것만, 별명 자리와 users 는 같이" 지우도록 되어 있어서 한 요청에 담습니다.
        /// 끝나면 성공이든 실패든 이 폰의 로그인 정보를 잊습니다 — 다음 판부터는 새 사람입니다.
        /// </summary>
        public void DeleteMyData(IList<string> gameIds, Action<bool> done)
        {
            _auth.EnsureSignedIn(ok =>
            {
                if (!ok) { Forget(); done?.Invoke(false); return; }

                string uid = _auth.Uid;
                _db.Get(new[] { "users", uid }, current =>
                {
                    if (!current.Ok && current.code != 404) { Forget(); done?.Invoke(false); return; }

                    var writes = new List<object> { _db.DeleteWrite(new[] { "users", uid }) };

                    string nick = current.Ok ? Firestore.GetString(current.Json, "nick") : null;
                    string key = string.IsNullOrEmpty(nick) ? null : PlayerIdentity.KeyOf(nick);
                    if (PlayerIdentity.IsValidKey(key)) writes.Add(_db.DeleteWrite(new[] { "nicknames", key }));

                    var games = KnownGames();
                    if (gameIds != null)
                        foreach (var id in gameIds)
                            if (!string.IsNullOrEmpty(id) && !games.Contains(id)) games.Add(id);
                    foreach (var id in games)
                        writes.Add(_db.DeleteWrite(new[] { "ranks", id, ScoresFolder, uid }));

                    _db.Commit(writes, deleted =>
                    {
                        if (!deleted.Ok)
                        {
                            Debug.LogWarning("[Arcade] 서버의 내 기록을 지우지 못했습니다 — " + deleted);
                            Forget();
                            done?.Invoke(false);
                            return;
                        }

                        _auth.DeleteAccount(accountGone =>
                        {
                            if (!accountGone) Debug.LogWarning("[Arcade] 기록은 지웠지만 익명 계정은 못 지웠습니다 (남아도 문제는 없습니다).");
                            Forget();
                            done?.Invoke(true);
                        });
                    });
                });
            });
        }

        /// <summary>이 폰의 로그인 정보와 랭킹 캐시를 잊습니다. 다음 로그인은 새 번호입니다.</summary>
        void Forget()
        {
            _auth.Forget();
            _cache.Clear();
        }

        // ------------------------------------------------------------------ 폰에 적어 두는 것들

        static string SentKey(string gameId) => "Arcade.Rank." + gameId + ".Sent";

        /// <summary>서버에 올린 값의 표시. 점수나 별명 중 하나라도 바뀌면 다시 올립니다.</summary>
        static string SentStamp(int best, string nick) => best + "|" + PlayerIdentity.KeyOf(nick) + "|" + nick;

        static bool NeedsFlush(string gameId)
        {
            int best = LocalRankingService.BestOf(gameId);
            return best > 0 && PlayerPrefs.GetString(SentKey(gameId), "") != SentStamp(best, PlayerIdentity.Nickname);
        }

        static void RememberGame(string gameId)
        {
            var games = KnownGames();
            if (games.Contains(gameId)) return;

            games.Add(gameId);
            PlayerPrefs.SetString(GamesKey, string.Join(",", games));
            PlayerPrefs.Save();
        }

        static List<string> KnownGames()
        {
            var games = new List<string>();
            foreach (var id in PlayerPrefs.GetString(GamesKey, "").Split(','))
                if (id.Length > 0 && !games.Contains(id)) games.Add(id);
            return games;
        }
    }
}
