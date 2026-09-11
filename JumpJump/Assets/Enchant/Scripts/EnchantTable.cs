using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Enchant
{
    /// <summary>
    /// @Enchant_Sword.csv 를 강화 표(EnchantLevel 목록)로 바꿔 줍니다.
    /// 점프점프의 DifficultyTable · 활쏘기의 StageTable 과 같은 규칙입니다.
    ///
    /// - 열은 "위치"가 아니라 "이름"으로 찾습니다. 순서를 바꾸거나 메모 열을 끼워 넣어도 됩니다.
    /// - 이름은 대소문자 / 공백 / 밑줄을 무시하고 비교합니다. 기획서의 `Enchat_Level`(오타)도
    ///   CSV 의 `Enchant_level` 도 같은 열로 봅니다.
    /// - 값이 없는 칸은 **바로 위 줄의 값을 이어받습니다.** 바뀌는 칸만 채워도 됩니다.
    /// - 확률은 만분율(10000 = 100%)입니다. `90%` 처럼 % 를 붙여 적으면 9000 으로 읽습니다.
    /// - 엑셀이 저장하는 BOM / CRLF / 따옴표 묶인 칸을 처리합니다.
    ///
    /// | 열 | 뜻 | 없으면 |
    /// | --- | --- | --- |
    /// | Enchant_Level | 강화 수치 | **꼭 있어야 합니다** |
    /// | Enchant_Prob  | 이 수치에서 강화 성공 확률 (만분율) | 10000 |
    /// | Melt          | 이 수치에서 분해하면 받는 안전 강화 주문서 수. 0 = 분해 불가 | 0 |
    /// | Melt_Prob     | (선택) 분해했을 때 주문서가 나올 확률 (만분율) | 10000 |
    /// | Image         | 이 수치의 검 그림 이름 | 빈 칸 |
    /// </summary>
    public static class EnchantTable
    {
        static readonly string[] LevelAliases = { "enchantlevel", "enchatlevel", "level", "강화", "강화수치", "레벨" };
        static readonly string[] ProbAliases = { "enchantprob", "enchatprob", "prob", "successprob", "확률", "성공확률", "강화확률" };
        static readonly string[] MeltAliases = { "melt", "meltscroll", "meltscrolls", "분해", "분해보상" };
        static readonly string[] MeltProbAliases = { "meltprob", "분해확률" };
        static readonly string[] ImageAliases = { "image", "sprite", "icon", "이미지", "그림" };

        public static EnchantLevel[] Parse(string csv, out string report)
        {
            report = "";

            if (string.IsNullOrEmpty(csv))
            {
                report = "CSV 내용이 비어 있습니다.";
                return null;
            }

            var lines = SplitLines(csv);
            if (lines.Count < 2)
            {
                report = "머리글 줄과 데이터 줄이 모두 필요합니다.";
                return null;
            }

            var header = SplitCells(lines[0]);
            int cLevel = FindColumn(header, LevelAliases);
            int cProb = FindColumn(header, ProbAliases);
            int cMelt = FindColumn(header, MeltAliases);
            int cMeltProb = FindColumn(header, MeltProbAliases);
            int cImage = FindColumn(header, ImageAliases);

            if (cLevel < 0)
            {
                report = "Enchant_Level (강화 수치) 열을 찾지 못했습니다."
                       + "\n  읽은 머리글: " + string.Join(" | ", header.ToArray());
                return null;
            }

            var warnings = new StringBuilder();
            if (cProb < 0) warnings.Append("  Enchant_Prob 열이 없어 전부 100% 로 채웁니다.\n");
            if (cImage < 0) warnings.Append("  Image 열이 없어 검 그림이 바뀌지 않습니다.\n");

            var byLevel = new SortedDictionary<int, EnchantLevel>();
            int prob = 10000, melt = 0, meltProb = 10000;
            string image = "";
            int prevLevel = int.MinValue;

            for (int i = 1; i < lines.Count; i++)
            {
                var cells = SplitCells(lines[i]);
                if (!TryInt(cells, cLevel, out int level)) continue;   // 빈 줄 · 메모 줄은 건너뜁니다

                if (TryChance(cells, cProb, out int readProb)) prob = readProb;
                if (TryInt(cells, cMelt, out int readMelt)) melt = readMelt;
                if (TryChance(cells, cMeltProb, out int readMeltProb)) meltProb = readMeltProb;
                if (cImage >= 0 && cImage < cells.Count && cells[cImage].Length > 0) image = cells[cImage];

                if (level < 0)
                {
                    warnings.Append($"  {i + 1}번째 줄: 강화 수치 {level} 은(는) 쓸 수 없어 건너뜁니다.\n");
                    continue;
                }
                if (level <= prevLevel)
                    warnings.Append($"  {i + 1}번째 줄: 강화 수치 {level} 이(가) 앞 줄({prevLevel})보다 크지 않습니다. 순서대로 다시 정렬했습니다.\n");
                prevLevel = Mathf.Max(prevLevel, level);

                if (prob < 0 || prob > 10000)
                {
                    warnings.Append($"  {i + 1}번째 줄: 확률 {prob} 이(가) 0~10000 밖이라 맞췄습니다 (만분율입니다. 10000 = 100%).\n");
                    prob = Mathf.Clamp(prob, 0, 10000);
                }
                if (melt < 0) melt = 0;

                if (byLevel.ContainsKey(level))
                    warnings.Append($"  {i + 1}번째 줄: 강화 수치 {level} 이(가) 두 번 나와서 아래 줄 값을 씁니다.\n");

                byLevel[level] = new EnchantLevel
                {
                    level = level,
                    successPer10000 = prob,
                    meltScrolls = melt,
                    meltPer10000 = Mathf.Clamp(meltProb, 0, 10000),
                    image = image,
                };
            }

            if (byLevel.Count == 0)
            {
                report = "읽을 수 있는 데이터 줄이 없습니다.";
                return null;
            }

            var result = new List<EnchantLevel>(byLevel.Values);
            if (result[0].level != 0)
                warnings.Append($"  표가 +0 이 아니라 +{result[0].level} 부터 시작합니다. 그 아래 수치도 첫 줄 값을 씁니다.\n");

            report = warnings.ToString();
            return result.ToArray();
        }

        /// <summary>
        /// 강화 표를 사람이 읽기 좋게. 값이 같은 줄은 한 줄로 묶습니다.
        /// 끝에 **"성공만 이어서 올라갈 확률"** 을 곁들입니다 — 밸런스를 볼 때 가장 먼저 궁금한 숫자라서.
        /// </summary>
        public static string Describe(EnchantLevel[] rows)
        {
            var sb = new StringBuilder();
            sb.Append("  강화 수치      성공 확률   분해 보상        그림\n");

            int start = 0;
            for (int i = 1; i <= rows.Length; i++)
            {
                bool same = i < rows.Length &&
                            rows[i].successPer10000 == rows[start].successPer10000 &&
                            rows[i].meltScrolls == rows[start].meltScrolls &&
                            rows[i].meltPer10000 == rows[start].meltPer10000 &&
                            rows[i].image == rows[start].image &&
                            rows[i].level == rows[i - 1].level + 1;
                if (same) continue;

                var row = rows[start];
                int last = rows[i - 1].level;
                string range = last == row.level ? $"+{row.level}" : $"+{row.level}~+{last}";
                string melt = row.meltScrolls <= 0 ? "-" :
                              "주문서 " + row.meltScrolls + (row.meltPer10000 < 10000 ? $" ({Percent(row.meltPer10000)})" : "");

                sb.Append("  ").Append(range.PadRight(14))
                  .Append(Percent(row.successPer10000).PadRight(12))
                  .Append(melt.PadRight(16))
                  .Append(row.image).Append('\n');

                start = i;
            }

            // 일반 강화만으로(한 번도 안 깨지고) 이 수치까지 갈 확률
            double chain = 1.0;
            var milestones = new StringBuilder();
            foreach (var row in rows)
            {
                int next = row.level + 1;
                chain *= row.successPer10000 / 10000.0;
                if (next % 10 == 0 || next == 7)
                    milestones.Append($"+{next} {FormatChance(chain)}   ");
            }
            sb.Append("  일반 강화만으로 한 번에 올라갈 확률 : ").Append(milestones).Append('\n');
            return sb.ToString();
        }

        /// <summary>만분율을 "90%" / "0.5%" 로.</summary>
        public static string Percent(int per10000)
        {
            return (per10000 / 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        static string FormatChance(double p)
        {
            if (p >= 0.01) return (p * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            if (p <= 0) return "0%";
            return "1/" + (1 / p).ToString("N0", CultureInfo.InvariantCulture);
        }

        static List<string> SplitLines(string csv)
        {
            var lines = new List<string>();
            foreach (var raw in csv.TrimStart('﻿').Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Trim().Length == 0) continue;
                if (line.TrimStart().StartsWith("#")) continue;   // 주석 줄
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>따옴표로 묶인 칸(엑셀이 쉼표 포함 값을 저장하는 방식)까지 처리하는 분해기.</summary>
        static List<string> SplitCells(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (quoted)
                {
                    if (c != '"') { sb.Append(c); continue; }
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = false;
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { cells.Add(sb.ToString().Trim()); sb.Length = 0; }
                else sb.Append(c);
            }

            cells.Add(sb.ToString().Trim());
            return cells;
        }

        static int FindColumn(List<string> header, string[] aliases)
        {
            for (int i = 0; i < header.Count; i++)
            {
                string name = Normalize(header[i]);
                if (name.Length == 0) continue;

                for (int a = 0; a < aliases.Length; a++)
                    if (name == aliases[a]) return i;
            }
            return -1;
        }

        static string Normalize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        static bool TryInt(List<string> cells, int index, out int value)
        {
            value = 0;
            if (index < 0 || index >= cells.Count) return false;
            if (!float.TryParse(cells[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                return false;
            value = Mathf.RoundToInt(f);
            return true;
        }

        /// <summary>만분율 칸. "9000" 은 그대로, "90%" 는 9000 으로 읽습니다.</summary>
        static bool TryChance(List<string> cells, int index, out int value)
        {
            value = 0;
            if (index < 0 || index >= cells.Count) return false;

            string cell = cells[index].Trim();
            if (cell.EndsWith("%"))
            {
                if (!float.TryParse(cell.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out float percent))
                    return false;
                value = Mathf.RoundToInt(percent * 100f);
                return true;
            }

            return TryInt(cells, index, out value);
        }
    }
}
