using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// 화면에 나오는 **글자를 전부 모아 둔 표**입니다. 원본은 `D:\00.JumpJump\strings.csv` 이고,
    /// 게임은 `Assets/Shell/Resources/strings.csv` 사본을 읽습니다.
    /// (난이도의 `config.csv` / `archery.csv` 와 똑같은 방식입니다)
    ///
    /// 표는 `Key,Text` 두 열입니다. Key 는 코드가 찾는 이름이고, Text 가 화면에 나올 글자입니다.
    /// **Key 는 고치지 마세요. Text 만 고치면 됩니다.**
    ///
    /// - `\n` 을 적으면 줄바꿈이 됩니다. (엑셀 안에서 줄을 나눠도 됩니다)
    /// - `{score}` 처럼 중괄호로 감싼 것은 **숫자가 들어갈 자리**입니다.
    ///   자리를 옮기거나 지워도 되지만, 이름을 잘못 적으면 그 자리에 글자가 그대로 남습니다.
    /// - `Note` 처럼 모르는 열은 무시하므로, 메모용 열을 마음껏 추가해도 됩니다.
    ///
    /// **표가 없거나 어떤 줄이 비어 있어도 앱은 돌아갑니다** — 그 자리는 코드에 적힌
    /// 기본 문구(전부 영어)로 대신합니다. 그래서 표를 지워도 화면이 비지 않습니다.
    /// </summary>
    public static class StringTable
    {
        /// <summary>Resources 안에서의 이름. 확장자는 빼고 적습니다.</summary>
        public const string ResourceName = "strings";

        static Dictionary<string, string> _table;

        /// <summary>
        /// 키에 해당하는 글자. 표에 없으면 fallback 을 그대로 돌려줍니다.
        /// **fallback 은 코드에 적어 둔 기본 문구입니다** — 표가 깨져도 화면이 비지 않게 하는 안전장치입니다.
        /// </summary>
        public static string Get(string key, string fallback)
        {
            Load();

            if (_table != null && _table.TryGetValue(Normalize(key), out string value) && value.Length > 0)
                return value;

            return fallback;
        }

        /// <summary>
        /// 키에 해당하는 글자를 가져와 `{이름}` 자리를 채웁니다.
        ///
        /// <code>Format("jump.hud.score", "Score : {score}", ("score", 1200))</code>
        ///
        /// 표에 없는 이름이 적혀 있으면 **그 자리는 손대지 않고 그대로 둡니다.**
        /// 오타가 나도 앱이 멈추지 않고, 화면을 보면 어디가 틀렸는지 바로 보입니다.
        /// </summary>
        public static string Format(string key, string fallback, params (string name, object value)[] values)
        {
            return Fill(Get(key, fallback), values);
        }

        /// <summary>`{이름}` 자리를 채웁니다. 표를 거치지 않고 문자열만 다룰 때 씁니다.</summary>
        public static string Fill(string text, params (string name, object value)[] values)
        {
            if (string.IsNullOrEmpty(text) || values == null) return text;
            if (text.IndexOf('{') < 0) return text;   // 채울 자리가 없으면 그대로

            var sb = new StringBuilder(text);
            foreach (var (name, value) in values)
                sb.Replace("{" + name + "}", value != null ? value.ToString() : "");

            return sb.ToString();
        }

        /// <summary>
        /// 표를 다시 읽습니다. 에디터에서 CSV 를 고친 뒤 부르면 바로 반영됩니다.
        /// (플레이 모드에 다시 들어가면 어차피 다시 읽습니다)
        /// </summary>
        public static void Reload()
        {
            _table = null;
            Load();
        }

        /// <summary>표에 실제로 들어온 줄 수. 확인용입니다.</summary>
        public static int Count
        {
            get { Load(); return _table != null ? _table.Count : 0; }
        }

        static void Load()
        {
            if (_table != null) return;

            _table = new Dictionary<string, string>();

            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) return;   // 표가 없으면 전부 기본 문구로 갑니다

            Parse(asset.text, _table);
        }

        // ------------------------------------------------------------------ CSV 읽기

        /// <summary>
        /// `Key,Text` 두 열을 읽어 사전에 담습니다. 열은 **위치가 아니라 이름**으로 찾으므로
        /// 열 순서를 바꾸거나 메모 열을 끼워 넣어도 됩니다.
        /// 엑셀이 저장하는 BOM / CRLF / 따옴표 묶인 칸 / 칸 안의 줄바꿈을 전부 처리합니다.
        /// </summary>
        public static void Parse(string csv, Dictionary<string, string> into)
        {
            if (string.IsNullOrEmpty(csv) || into == null) return;

            var rows = SplitRows(csv);
            if (rows.Count < 2) return;

            int cKey = FindColumn(rows[0], "key", "이름", "키");
            int cText = FindColumn(rows[0], "text", "글자", "문구", "내용");
            if (cKey < 0 || cText < 0) return;

            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                if (cKey >= row.Count) continue;

                string key = Normalize(row[cKey]);
                if (key.Length == 0 || key.StartsWith("#")) continue;   // 빈 줄과 주석 줄은 건너뜁니다

                string text = cText < row.Count ? row[cText] : "";
                into[key] = text.Replace("\\n", "\n");   // 표에 적은 \n 을 진짜 줄바꿈으로
            }
        }

        /// <summary>키 비교는 대소문자와 앞뒤 공백을 무시합니다.</summary>
        static string Normalize(string key)
        {
            return key == null ? "" : key.Trim().ToLowerInvariant();
        }

        static int FindColumn(List<string> header, params string[] aliases)
        {
            for (int i = 0; i < header.Count; i++)
            {
                string name = Normalize(header[i]).Replace(" ", "").Replace("_", "").Replace("-", "");
                foreach (var alias in aliases)
                    if (name == alias) return i;
            }
            return -1;
        }

        /// <summary>
        /// CSV 를 줄 -> 칸으로 쪼갭니다. 따옴표로 묶인 칸 안의 쉼표와 줄바꿈은 그대로 둡니다.
        /// (한글 문장에는 쉼표가 흔해서 이 처리가 꼭 필요합니다. 엑셀이 알아서 따옴표를 붙여 줍니다)
        /// 칸 안의 `""` 는 따옴표 한 개로 읽습니다.
        /// </summary>
        static List<List<string>> SplitRows(string csv)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;

            if (csv.Length > 0 && csv[0] == '\uFEFF') csv = csv.Substring(1);   // 엑셀 BOM

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];

                if (quoted)
                {
                    if (c != '"') { cell.Append(c); continue; }

                    // "" 는 따옴표 한 개, 그 밖의 " 는 묶음 끝
                    if (i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;

                    case ',':
                        row.Add(cell.ToString()); cell.Clear();
                        break;

                    case '\r':
                        break;   // CRLF 의 앞쪽은 버립니다

                    case '\n':
                        row.Add(cell.ToString()); cell.Clear();
                        rows.Add(row); row = new List<string>();
                        break;

                    default:
                        cell.Append(c);
                        break;
                }
            }

            row.Add(cell.ToString());
            if (row.Count > 1 || row[0].Length > 0) rows.Add(row);

            return rows;
        }
    }
}
