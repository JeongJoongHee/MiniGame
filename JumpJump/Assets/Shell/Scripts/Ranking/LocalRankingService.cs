using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// **서버 없이 이 폰 안에만 저장하는 랭킹**입니다. 1단계에서 화면과 흐름을 먼저 만들기 위한 것으로,
    /// 2단계에서 Firebase 구현으로 갈아 끼웁니다.
    ///
    /// 내 최고 점수는 진짜로 저장되므로, 갈아 끼운 뒤에도 "내 기록"은 그대로 남습니다.
    ///
    /// <b>에디터에서만</b> 표본 줄 몇 개를 섞어 줍니다. 랭킹 화면이 한 줄짜리면 자리·글자 폭을
    /// 확인할 수 없기 때문입니다. **폰에 넣은 앱에서는 표본이 나오지 않습니다** —
    /// 없는 사람을 있는 것처럼 보여 주면 안 되니까요.
    /// </summary>
    public class LocalRankingService : IRankingService
    {
        /// <summary>에디터에서 화면을 확인할 때만 섞이는 표본입니다. 실제 앱에는 나오지 않습니다.</summary>
        static readonly (string nick, int score)[] SampleRows =
        {
            ("표본플레이어일", 52400),
            ("표본둘",         41300),
            ("표본셋",         33800),
            ("표본넷",         27100),
            ("표본다섯",       19600),
            ("표본여섯",       12200),
            ("표본일곱",        6400),
        };

        readonly bool _useSamples;

        public LocalRankingService(bool? useSamples = null)
        {
            _useSamples = useSamples ?? Application.isEditor;
        }

        public bool IsReady => true;

        public void Submit(string gameId, string nickname, int score, Action<bool> done = null)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                done?.Invoke(false);
                return;
            }

            if (score > BestOf(gameId))
            {
                PlayerPrefs.SetInt(BestKey(gameId), score);
                PlayerPrefs.Save();
            }

            done?.Invoke(true);
        }

        public void Fetch(string gameId, int count, Action<RankingPage> done)
        {
            var page = new RankingPage { ok = true };

            int myScore = BestOf(gameId);
            page.myScore = myScore;

            var all = new List<RankingRow>();

            if (_useSamples)
                foreach (var (nick, score) in SampleRows)
                    all.Add(new RankingRow(0, nick, score));

            if (myScore > 0)
            {
                string me = PlayerIdentity.HasNickname ? PlayerIdentity.Nickname : "나";
                all.Add(new RankingRow(0, me, myScore, true));
            }

            all.Sort((a, b) => b.score.CompareTo(a.score));

            for (int i = 0; i < all.Count && i < count; i++)
            {
                var row = all[i];
                row.rank = i + 1;
                page.rows.Add(row);
                if (row.isMe) page.myRank = row.rank;
            }

            done?.Invoke(page);
        }

        /// <summary>
        /// 별명을 잡습니다. 여기서는 서버가 없으니 <b>표본 별명과 겹치는지만</b> 봅니다.
        /// 그래도 "이미 있는 별명입니다" 흐름을 지금 화면에서 그대로 확인할 수 있습니다.
        /// (에디터에서 "표본둘" 을 넣어 보시면 됩니다)
        /// </summary>
        public void ReserveNickname(string nickname, Action<NicknameResult> done)
        {
            string clean = PlayerIdentity.Sanitize(nickname);
            if (clean.Length == 0)
            {
                done?.Invoke(NicknameResult.Failed);
                return;
            }

            foreach (var (nick, _) in SampleRows)
            {
                if (string.Equals(nick, clean, StringComparison.OrdinalIgnoreCase))
                {
                    done?.Invoke(NicknameResult.Taken);
                    return;
                }
            }

            done?.Invoke(NicknameResult.Ok);
        }

        /// <summary>이 게임에서의 내 최고 점수.</summary>
        public static int BestOf(string gameId)
        {
            return PlayerPrefs.GetInt(BestKey(gameId), 0);
        }

        static string BestKey(string gameId)
        {
            return "Arcade.Rank." + gameId + ".Best";
        }
    }
}
