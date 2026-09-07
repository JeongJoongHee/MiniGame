using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Archery
{
    /// <summary>
    /// archery.csv 를 난이도 구간표(StageBand 목록)로 바꿔 줍니다.
    /// 점프점프의 DifficultyTable 과 같은 방식이라, 표를 다루는 규칙이 두 게임에서 똑같습니다.
    ///
    /// - 열은 "위치"가 아니라 "이름"으로 찾습니다. 열 순서를 바꾸거나
    ///   메모용 열을 하나 더 끼워 넣어도 게임은 그대로 동작합니다.
    /// - 이름은 대소문자 / 공백 / 밑줄 / 하이픈을 무시하고 비교하므로
    ///   Target_Speed, targetSpeed, "Target Speed" 가 전부 같은 열로 인식됩니다.
    /// - 값이 없는 열은 **바로 위 줄의 값을 이어받습니다.** 그래서 표를 만들 때
    ///   바뀌는 칸만 채워 넣어도 됩니다.
    /// - 엑셀이 저장하는 BOM / CRLF / 따옴표 묶인 칸을 모두 처리합니다.
    ///
    /// 꼭 있어야 하는 열은 Hits_Max 하나뿐입니다. 나머지는 없으면 기본값으로 채웁니다.
    /// 나중에 열을 늘릴 때는 (1) 별칭 배열에 이름을 추가하고 (2) StageBand 에 필드를 추가하고
    /// (3) 아래 읽는 부분에 한 줄 넣으면 됩니다.
    /// </summary>
    public static class StageTable
    {
        static readonly string[] HitsMaxAliases = { "hitsmax", "hitmax", "maxhits", "upto", "uptohits", "명중", "명중최대", "최대명중" };
        static readonly string[] SpeedAliases = { "targetspeed", "speed", "movespeed", "과녁속도", "이동속도" };
        static readonly string[] ShrinkAliases = { "shrinkpercent", "shrink", "shrinkperhit", "sizeshrink", "축소", "축소율" };
        static readonly string[] SizeMinAliases = { "sizeminpercent", "sizemin", "minsize", "최소크기", "크기하한" };
        static readonly string[] AmmoAliases = { "ammomax", "ammo", "maxammo", "arrows", "화살", "화살수", "최대화살" };
        static readonly string[] ArrowSpeedAliases = { "arrowspeed", "shotspeed", "화살속도" };

        // 열이 아예 없는 CSV 를 위한 첫 줄 기본값. 그 뒤 줄은 바로 위 줄 값을 이어받습니다.
        const float DefaultTargetSpeed = 1.6f;
        const float DefaultShrinkPercent = 1f;
        const float DefaultSizeMinPercent = 35f;
        const int DefaultAmmoMax = 5;
        const float DefaultArrowSpeed = 16f;

        /// <summary>
        /// CSV 본문을 구간표로 변환합니다. 실패하면 null 을 돌려주고 report 에 이유가 담깁니다.
        /// 성공했더라도 경고가 있으면 report 가 비어 있지 않을 수 있습니다.
        /// </summary>
        public static StageBand[] Parse(string csv, out string report)
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
            int cHits = FindColumn(header, HitsMaxAliases);
            int cSpeed = FindColumn(header, SpeedAliases);
            int cShrink = FindColumn(header, ShrinkAliases);
            int cSizeMin = FindColumn(header, SizeMinAliases);
            int cAmmo = FindColumn(header, AmmoAliases);
            int cArrow = FindColumn(header, ArrowSpeedAliases);

            if (cHits < 0)
            {
                report = "Hits_Max (구간의 누적 명중 수 상한) 열을 찾지 못했습니다."
                       + "\n  읽은 머리글: " + string.Join(" | ", header.ToArray());
                return null;
            }

            var warnings = new StringBuilder();
            if (cSpeed < 0) warnings.Append($"  Target_Speed 열이 없어 {DefaultTargetSpeed} 로 채웁니다.\n");
            if (cAmmo < 0) warnings.Append($"  Ammo_Max 열이 없어 {DefaultAmmoMax} 로 채웁니다.\n");

            var bands = new List<StageBand>();
            float speed = DefaultTargetSpeed;
            float shrink = DefaultShrinkPercent;
            float sizeMin = DefaultSizeMinPercent;
            int ammo = DefaultAmmoMax;
            float arrowSpeed = DefaultArrowSpeed;
            int prevHits = int.MinValue;

            for (int i = 1; i < lines.Count; i++)
            {
                var cells = SplitCells(lines[i]);

                // 상한을 못 읽는 줄(빈 줄, 메모 줄, 합계 줄)은 조용히 건너뜁니다.
                if (!TryInt(cells, cHits, out int upToHits)) continue;

                // 빈 칸은 바로 위 줄 값을 그대로 씁니다.
                if (TryFloat(cells, cSpeed, out float readSpeed)) speed = readSpeed;
                if (TryFloat(cells, cShrink, out float readShrink)) shrink = readShrink;
                if (TryFloat(cells, cSizeMin, out float readSizeMin)) sizeMin = readSizeMin;
                if (TryInt(cells, cAmmo, out int readAmmo)) ammo = readAmmo;
                if (TryFloat(cells, cArrow, out float readArrow)) arrowSpeed = readArrow;

                if (upToHits <= prevHits)
                    warnings.Append($"  {i + 1}번째 줄: 명중 수 상한 {upToHits} 이(가) 앞 줄({prevHits})보다 크지 않습니다. 오름차순으로 정렬해 주세요.\n");
                prevHits = upToHits;

                if (sizeMin > 100f)
                {
                    warnings.Append($"  {i + 1}번째 줄: 최소 크기 {sizeMin}% 가 100% 를 넘어 100 으로 낮췄습니다.\n");
                    sizeMin = 100f;
                }

                bands.Add(new StageBand
                {
                    upToHits = upToHits,
                    targetSpeed = Mathf.Max(0f, speed),
                    shrinkPercent = Mathf.Max(0f, shrink),
                    sizeMinPercent = Mathf.Clamp(sizeMin, 1f, 100f),
                    ammoMax = Mathf.Max(1, ammo),
                    arrowSpeed = Mathf.Max(1f, arrowSpeed),
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
        public static string Describe(StageBand[] bands)
        {
            var sb = new StringBuilder();
            sb.Append("  명중 수        과녁속도   축소율   최소크기   화살수   화살속도\n");

            int from = 0;
            for (int i = 0; i < bands.Length; i++)
            {
                var band = bands[i];
                bool last = i == bands.Length - 1;
                string range = last ? $"{from}회~" : $"{from}~{band.upToHits}회";

                sb.Append("  ").Append(range.PadRight(15))
                  .Append(band.targetSpeed.ToString("0.##").PadRight(11))
                  .Append((band.shrinkPercent.ToString("0.##") + "%").PadRight(9))
                  .Append((band.sizeMinPercent.ToString("0.##") + "%").PadRight(11))
                  .Append(band.ammoMax.ToString().PadRight(9))
                  .Append(band.arrowSpeed.ToString("0.##"))
                  .Append('\n');

                from = band.upToHits + 1;
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
