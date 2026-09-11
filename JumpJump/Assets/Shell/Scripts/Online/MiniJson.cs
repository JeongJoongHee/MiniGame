using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arcade.Online
{
    /// <summary>
    /// **서버와 주고받는 JSON 을 읽고 쓰는 작은 도구**입니다.
    ///
    /// Unity 에 들어 있는 <c>JsonUtility</c> 는 미리 정해 둔 모양(클래스)만 읽을 수 있는데,
    /// Firestore 는 칸마다 <c>{"stringValue": ...}</c> / <c>{"integerValue": ...}</c> 처럼
    /// 모양이 바뀌는 JSON 을 돌려줍니다. 그래서 무엇이든 읽을 수 있는 이 도구를 따로 둡니다.
    ///
    /// 읽으면 이렇게 바뀝니다 — 객체 = <c>Dictionary&lt;string, object&gt;</c>, 배열 = <c>List&lt;object&gt;</c>,
    /// 글자 = <c>string</c>, 숫자 = <c>long</c>(정수) 또는 <c>double</c>, 참/거짓 = <c>bool</c>, null = <c>null</c>.
    /// </summary>
    public static class MiniJson
    {
        // ------------------------------------------------------------------ 읽기

        /// <summary>JSON 글자를 읽습니다. 모양이 틀리면 null 을 돌려줍니다 (예외를 던지지 않습니다).</summary>
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                var parser = new Parser(json);
                object value = parser.ReadValue();
                parser.SkipWhitespace();
                return parser.AtEnd ? value : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        sealed class Parser
        {
            readonly string _s;
            int _i;

            public Parser(string s) { _s = s; }

            public bool AtEnd => _i >= _s.Length;

            public void SkipWhitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd) throw new FormatException();

                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw new FormatException();
                }
            }

            Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>();
                _i++;   // {
                SkipWhitespace();
                if (Peek('}')) { _i++; return result; }

                while (true)
                {
                    SkipWhitespace();
                    if (!Peek('"')) throw new FormatException();
                    string key = ReadString();

                    SkipWhitespace();
                    if (!Peek(':')) throw new FormatException();
                    _i++;

                    result[key] = ReadValue();

                    SkipWhitespace();
                    if (Peek(',')) { _i++; continue; }
                    if (Peek('}')) { _i++; return result; }
                    throw new FormatException();
                }
            }

            List<object> ReadArray()
            {
                var result = new List<object>();
                _i++;   // [
                SkipWhitespace();
                if (Peek(']')) { _i++; return result; }

                while (true)
                {
                    result.Add(ReadValue());

                    SkipWhitespace();
                    if (Peek(',')) { _i++; continue; }
                    if (Peek(']')) { _i++; return result; }
                    throw new FormatException();
                }
            }

            string ReadString()
            {
                _i++;   // "
                var sb = new StringBuilder();

                while (_i < _s.Length)
                {
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }

                    if (_i >= _s.Length) break;
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw new FormatException();
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw new FormatException();
                    }
                }

                throw new FormatException();
            }

            object ReadNumber()
            {
                int start = _i;
                bool isFloat = false;

                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+') { _i++; continue; }
                    if (c == '.' || c == 'e' || c == 'E') { isFloat = true; _i++; continue; }
                    break;
                }

                string text = _s.Substring(start, _i - start);
                if (!isFloat && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    return l;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    return d;
                throw new FormatException();
            }

            bool Peek(char c) => _i < _s.Length && _s[_i] == c;

            void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw new FormatException();
                _i += word.Length;
            }
        }

        // ------------------------------------------------------------------ 쓰기

        /// <summary>사전 · 목록 · 글자 · 숫자 · 참/거짓 · null 을 JSON 글자로 씁니다.</summary>
        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;

                case IDictionary<string, object> dict:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var pair in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, pair.Key);
                        sb.Append(':');
                        Write(sb, pair.Value);
                    }
                    sb.Append('}');
                    break;
                }

                case IEnumerable list:
                {
                    sb.Append('[');
                    bool first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Write(sb, item);
                    }
                    sb.Append(']');
                    break;
                }

                default: WriteString(sb, value.ToString()); break;
            }
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------------ 꺼내기 도우미

        /// <summary><c>obj["a"]["b"]</c> 처럼 안쪽으로 따라 들어갑니다. 중간에 없으면 null.</summary>
        public static object Dig(object node, params string[] path)
        {
            foreach (var key in path)
            {
                if (!(node is Dictionary<string, object> dict) || !dict.TryGetValue(key, out node)) return null;
            }
            return node;
        }

        public static string DigString(object node, params string[] path)
        {
            return Dig(node, path) as string;
        }
    }
}
