using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace JumpJump
{
    /// <summary>
    /// config.csv 를 난이도 구간표(DifficultyBand 목록)로 바꿔 줍니다.
    ///
    /// - 열은 "위치"가 아니라 "이름"으로 찾습니다. 열 순서를 바꾸거나
    ///   메모용 열을 하나 더 끼워 넣어도 게임은 그대로 동작합니다.
    /// - 이름은 대소문자 / 공백 / 밑줄 / 하이픈을 무시하고 비교하므로
    ///   Height_Max, heightMax, "Heigh Max" 가 전부 같은 열로 인식됩니다.
    ///   원본 파일의 오타(Heght_Min, Heigh_Max)도 별칭에 넣어 두었습니다.
    /// - 엑셀이 저장하는 BOM / CRLF / 따옴표 묶인 칸을 모두 처리합니다.
    /// </summary>
    public static class DifficultyTable
    {
        static readonly string[] HeightMaxAliases = { "heightmax", "heighmax", "heghtmax", "hightmax", "maxheight", "높이", "최대높이", "높이최대" };
        static readonly string[] TimerAliases = { "timer", "time", "타이머" };
        static readonly string[] SpeedAliases = { "basespeed", "speed", "movespeed", "이동속도", "발판이동속도", "발판속도" };
        static readonly string[] BlocksMinAliases = { "blocksmin", "blockmin", "minblocks", "platformmin", "발판최소", "최소발판" };
        static readonly string[] BlocksMaxAliases = { "blocksmax", "blockmax", "maxblocks", "platformmax", "발판최대", "최대발판" };

        // 발판 최소/최대 열이 아예 없는 CSV 를 위한 첫 줄 기본값.
        // (그 뒤 줄은 바로 위 줄의 값을 이어받습니다)
        const int DefaultBlocksMin = 4;
        const int DefaultBlocksMax = 6;

        /// <summary>
        /// CSV 본문을 구간표로 변환합니다. 실패하면 null 을 돌려주고 report 에 이유가 담깁니다.
        /// 성공했더라도 경고가 있으면 report 가 비어 있지 않을 수 있습니다.
        /// </summary>
        public static DifficultyBand[] Parse(string csv, out string report)
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
            int cUpTo = FindColumn(header, HeightMaxAliases);
            int cTimer = FindColumn(header, TimerAliases);
            int cSpeed = FindColumn(header, SpeedAliases);
            int cBlocksMin = FindColumn(header, BlocksMinAliases);
            int cBlocksMax = FindColumn(header, BlocksMaxAliases);

            var missing = new List<string>();
            if (cUpTo < 0) missing.Add("Height_Max (구간의 높이 상한)");
            if (cTimer < 0) missing.Add("Timer (타이머 초)");
            if (cSpeed < 0) missing.Add("Base_Speed (발판 이동 속도)");
            if (missing.Count > 0)
            {
                report = "다음 열을 찾지 못했습니다: " + string.Join(", ", missing.ToArray())
                       + "\n  읽은 머리글: " + string.Join(" | ", header.ToArray());
                return null;
            }

            var warnings = new StringBuilder();
            if (cBlocksMin < 0 || cBlocksMax < 0)
                warnings.Append($"  발판 최소/최대 열이 없어 위 줄 값을 이어서 씁니다. (첫 줄 기본값 {DefaultBlocksMin}~{DefaultBlocksMax})\n");

            var bands = new List<DifficultyBand>();
            int blocksMin = DefaultBlocksMin;
            int blocksMax = DefaultBlocksMax;
            float prevUpTo = float.NegativeInfinity;

            for (int i = 1; i < lines.Count; i++)
            {
                var cells = SplitCells(lines[i]);

                // 높이 상한을 못 읽는 줄(빈 줄, 메모 줄, 합계 줄)은 조용히 건너뜁니다.
                if (!TryFloat(cells, cUpTo, out float upTo)) continue;

                if (!TryFloat(cells, cTimer, out float timer))
                {
                    warnings.Append($"  {i + 1}번째 줄: 타이머를 읽지 못해 건너뜁니다.\n");
                    continue;
                }
                if (!TryFloat(cells, cSpeed, out float speed))
                {
                    warnings.Append($"  {i + 1}번째 줄: 이동 속도를 읽지 못해 건너뜁니다.\n");
                    continue;
                }

                if (TryInt(cells, cBlocksMin, out int readMin)) blocksMin = readMin;
                if (TryInt(cells, cBlocksMax, out int readMax)) blocksMax = readMax;

                if (blocksMin > blocksMax)
                {
                    warnings.Append($"  {i + 1}번째 줄: 발판 최소({blocksMin})가 최대({blocksMax})보다 커서 서로 바꿨습니다.\n");
                    int swap = blocksMin; blocksMin = blocksMax; blocksMax = swap;
                }

                if (upTo <= prevUpTo)
                    warnings.Append($"  {i + 1}번째 줄: 높이 상한 {upTo} 이(가) 앞 줄({prevUpTo})보다 크지 않습니다. 오름차순으로 정렬해 주세요.\n");
                prevUpTo = upTo;

                bands.Add(new DifficultyBand
                {
                    upToMeters = upTo,
                    minBlocks = Mathf.Max(1, blocksMin),
                    maxBlocks = Mathf.Max(1, blocksMax),
                    timer = Mathf.Max(0.1f, timer),
                    moveSpeed = Mathf.Max(0f, speed),
                });
            }

            if (bands.Count == 0)
            {
                report = "읽을 수 있는 데이터 줄이 없습니다.";
                return null;
            }

            report = warnings.ToString();
            return bands.ToArray();
        }

        /// <summary>구간표를 사람이 읽기 좋은 표로. 로그에 찍어 눈으로 확인할 때 씁니다.</summary>
        public static string Describe(DifficultyBand[] bands, float metersPerRow)
        {
            var sb = new StringBuilder();
            sb.Append("  높이(m)        칸(row)        발판     타이머   이동속도\n");

            float from = 0f;
            for (int i = 0; i < bands.Length; i++)
            {
                var band = bands[i];
                bool last = i == bands.Length - 1;

                int firstRow = i == 0 ? 1 : Mathf.FloorToInt(from / metersPerRow) + 1;
                int lastRow = Mathf.FloorToInt(band.upToMeters / metersPerRow);

                string range = last ? $"{from:0}m~" : $"~{band.upToMeters:0}m";
                string rows = last ? $"{firstRow}~" : $"{firstRow}~{lastRow}";

                sb.Append("  ").Append(range.PadRight(15))
                  .Append(rows.PadRight(15))
                  .Append($"{band.minBlocks}~{band.maxBlocks}".PadRight(9))
                  .Append($"{band.timer:0.##}초".PadRight(9))
                  .Append(band.moveSpeed.ToString("0.##"))
                  .Append('\n');

                from = band.upToMeters;
            }

            return sb.ToString();
        }

        static List<string> SplitLines(string csv)
        {
            var lines = new List<string>();
            foreach (var raw in csv.TrimStart('﻿').Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
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

        /// <summary>대소문자 / 공백 / 밑줄 / 하이픈을 지워 열 이름 표기 흔들림을 흡수합니다.</summary>
        static string Normalize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        static bool TryFloat(List<string> cells, int index, out float value)
        {
            value = 0f;
            if (index < 0 || index >= cells.Count) return false;
            return float.TryParse(cells[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        static bool TryInt(List<string> cells, int index, out int value)
        {
            value = 0;
            if (!TryFloat(cells, index, out float f)) return false;
            value = Mathf.RoundToInt(f);
            return true;
        }
    }
}
