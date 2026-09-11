using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Arcade
{
    /// <summary>별명을 쓸 수 있는지 본 결과.</summary>
    public enum NicknameProblem
    {
        /// <summary>써도 됩니다.</summary>
        None,
        /// <summary>비어 있습니다.</summary>
        Empty,
        /// <summary>금지어가 들어 있습니다 (<c>badword.csv</c>).</summary>
        BadWord,
        /// <summary>서버에 이름으로 저장할 수 없는 모양입니다 (<c>.</c> 한 글자 같은 것).</summary>
        Invalid,
    }

    /// <summary>
    /// **별명에 금지어가 들어 있는지** 봅니다. 남들에게 보이는 이름이라 스토어 심사에서도 봅니다.
    ///
    /// 금지어 원본은 작업 폴더의 <c>badword.csv</c> 이고 (사용자가 직접 채우고 늘립니다),
    /// 게임은 <c>Assets/Shell/Resources/badword.csv</c> 사본을 읽습니다. 글자표와 같은 방식입니다.
    /// **표가 없으면 거르지 않을 뿐, 앱은 그대로 돌아갑니다.**
    ///
    /// 거르는 방법은 단순합니다 — 별명과 금지어를 둘 다 <see cref="Squash"/> 로 눌러 편 뒤,
    /// **금지어가 별명 안 어디에든 들어 있으면** 막습니다. 그래서 "시 발", "시.발", "SiBal" 도 걸립니다.
    /// 대신 짧은 금지어는 멀쩡한 이름까지 막을 수 있습니다 (예: <c>SM</c> 이 있으면 <c>SMILE</c> 도 막힘).
    /// 그런 줄은 표에서 지우시면 됩니다.
    /// </summary>
    public static class NicknameRules
    {
        /// <summary>Resources 안에서의 이름. 확장자는 빼고 적습니다.</summary>
        public const string ResourceName = "badword";

        /// <summary>첫 글자별로 나눠 둔 금지어. 별명 한 글자마다 그 글자로 시작하는 것만 대 봅니다.</summary>
        static Dictionary<char, List<string>> _byFirst;
        static int _count;

        /// <summary>표에서 읽어 온 금지어 수 (겹치는 줄은 하나로 셉니다). 확인용입니다.</summary>
        public static int WordCount
        {
            get { Load(); return _count; }
        }

        /// <summary>별명을 쓸 수 있는지 봅니다. 먼저 <see cref="PlayerIdentity.Sanitize"/> 로 다듬은 뒤 부르세요.</summary>
        public static NicknameProblem Check(string nickname)
        {
            return Check(nickname, out _);
        }

        /// <summary>걸린 금지어까지 돌려줍니다. 화면에는 보여 주지 않고 로그로만 남깁니다.</summary>
        public static NicknameProblem Check(string nickname, out string matched)
        {
            matched = null;
            if (string.IsNullOrEmpty(nickname) || nickname.Trim().Length == 0) return NicknameProblem.Empty;
            if (!PlayerIdentity.IsValidKey(PlayerIdentity.KeyOf(nickname))) return NicknameProblem.Invalid;

            Load();
            if (_count == 0) return NicknameProblem.None;

            string squashed = Squash(nickname);
            for (int i = 0; i < squashed.Length; i++)
            {
                if (!_byFirst.TryGetValue(squashed[i], out var words)) continue;

                foreach (var word in words)
                {
                    if (word.Length > squashed.Length - i) continue;
                    if (string.CompareOrdinal(squashed, i, word, 0, word.Length) != 0) continue;

                    matched = word;
                    return NicknameProblem.BadWord;
                }
            }

            return NicknameProblem.None;
        }

        /// <summary>
        /// 비교하기 좋게 눌러 폅니다 — 영문은 소문자로, 공백과 흔한 기호(<c>. _ - * ~</c> 등)는 뺍니다.
        /// 금지어 쪽도 똑같이 누르므로 표에 <c>COSEX.NET</c> 처럼 적혀 있어도 그대로 걸립니다.
        /// </summary>
        public static string Squash(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c)) continue;
                if (Separators.IndexOf(c) >= 0) continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>글자 사이에 끼워 금지어를 피해 가는 데 흔히 쓰는 기호들. 비교할 때 뺍니다.</summary>
        const string Separators = ".,_-~!@#$%^&*()+=[]{}|\\/:;\"'<>?`·•ㆍ…";

        /// <summary>표를 다시 읽습니다. 에디터에서 badword.csv 를 고친 뒤 부르면 바로 반영됩니다.</summary>
        public static void Reload()
        {
            _byFirst = null;
            Load();
        }

        static void Load()
        {
            if (_byFirst != null) return;

            _byFirst = new Dictionary<char, List<string>>();
            _count = 0;

            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) return;   // 표가 없으면 거르지 않습니다

            foreach (var word in Parse(asset.text))
            {
                if (!_byFirst.TryGetValue(word[0], out var list))
                    _byFirst[word[0]] = list = new List<string>();
                list.Add(word);
                _count++;
            }
        }

        /// <summary>
        /// 표에서 금지어를 꺼냅니다. 열 이름이 <c>Word</c>(또는 단어 / 금지어)인 열을 읽고,
        /// 그런 열이 없으면 첫 번째 열을 읽습니다. 겹치는 줄과 빈 줄, <c>#</c> 로 시작하는 줄은 건너뜁니다.
        /// </summary>
        public static List<string> Parse(string csv)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(csv)) return words;

            var rows = StringTable.SplitRows(csv);
            if (rows.Count == 0) return words;

            int column = StringTable.FindColumn(rows[0], "word", "words", "badword", "단어", "금지어");
            int start = 1;
            if (column < 0)
            {
                column = 0;
                start = 0;   // 머리줄이 없는 표로 봅니다
            }

            var seen = new HashSet<string>();
            for (int i = start; i < rows.Count; i++)
            {
                var row = rows[i];
                if (column >= row.Count) continue;

                string raw = row[column].Trim();
                if (raw.Length == 0 || raw.StartsWith("#")) continue;

                string word = Squash(raw);
                if (word.Length == 0 || !seen.Add(word)) continue;
                words.Add(word);
            }

            return words;
        }
    }
}
