using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Arcade.EditorTools
{
    /// <summary>
    /// 작업 폴더(D:\00.JumpJump)의 `strings.csv` 를 게임이 읽는 자리로 옮겨 줍니다.
    /// 난이도의 `config.csv` / `archery.csv` 와 똑같은 방식입니다.
    ///
    /// **게임은 `Assets/Shell/Resources/strings.csv` 를 읽습니다.**
    /// Resources 폴더라서 씬에 참조를 굽지 않습니다 — 그래서 **글자만 고칠 때는
    /// 씬을 다시 굽지 않아도 되고**, 엑셀에서 저장한 뒤 Play 만 눌러도 반영됩니다.
    /// (다만 바깥 파일을 안으로 가져오는 일은 에디터가 해야 하므로,
    ///  Build All Scenes 나 아래 메뉴를 한 번 거쳐야 합니다)
    /// </summary>
    public static class ShellStringsCsv
    {
        public const string AssetPath = "Assets/Shell/Resources/strings.csv";
        const string FileName = "strings.csv";

        [MenuItem("Tools/Arcade/Apply strings.csv", priority = 22)]
        public static void Apply()
        {
            Sync();

            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
            if (csv == null)
            {
                Debug.LogError($"[Arcade] {AssetPath} 을(를) 찾지 못했습니다.\n" +
                               $"  작업 폴더에 {FileName} 을 두고 다시 실행해 주세요.");
                return;
            }

            var table = new Dictionary<string, string>();
            StringTable.Parse(csv.text, table);

            if (table.Count == 0)
            {
                Debug.LogError($"[Arcade] {FileName} 에서 읽어 온 줄이 없습니다.\n" +
                               "  첫 줄에 Key 와 Text 열이 있는지 확인해 주세요.");
                return;
            }

            StringTable.Reload();
            Debug.Log($"[Arcade] {FileName} 적용 완료 ({table.Count}줄)\n" + Describe(table));
        }

        /// <summary>
        /// 바깥 폴더의 strings.csv 가 더 새로우면 가져옵니다. 없으면 지금 코드에 적힌
        /// 기본 문구로 새로 만들어 줍니다. 씬을 구울 때마다 자동으로 불립니다.
        /// </summary>
        public static void Sync()
        {
            string outside = OutsidePath();
            string inside = Path.GetFullPath(AssetPath);

            if (string.IsNullOrEmpty(outside)) return;
            if (string.Equals(Path.GetFullPath(outside), inside, System.StringComparison.OrdinalIgnoreCase)) return;

            if (!File.Exists(outside))
            {
                // 작업 폴더에 표가 없으면 안쪽 사본을 그대로 내보내 줍니다.
                // 사용자가 "고칠 파일이 어디 있지?" 하고 헤매지 않도록 하는 것입니다.
                if (File.Exists(inside)) File.Copy(inside, outside, overwrite: false);
                return;
            }

            // 엑셀이 옛 한글 인코딩으로 저장했으면 여기서 UTF-8 로 되돌려 놓습니다.
            // 파일을 고치는 것이라 복사 여부와 상관없이 먼저 합니다.
            RepairEncoding(outside);

            bool needCopy = !File.Exists(inside)
                            || File.GetLastWriteTimeUtc(outside) > File.GetLastWriteTimeUtc(inside);
            if (!needCopy) return;

            Directory.CreateDirectory(Path.GetDirectoryName(inside));
            File.Copy(outside, inside, overwrite: true);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            StringTable.Reload();

            Debug.Log($"[Arcade] 바깥 폴더의 {FileName} 이(가) 더 새로워서 가져왔습니다.\n" +
                      $"  {outside}\n  -> {AssetPath}");
        }

        /// <summary>
        /// CSV 를 **UTF-8 + BOM** 으로 맞춰 둡니다. 한글이 깨지지 않게 하는 장치입니다.
        ///
        /// 한글 윈도우의 엑셀은 CSV 를 열 때, 파일 앞에 BOM 이라는 표시가 없으면
        /// **옛 한글 인코딩(CP949)이라고 넘겨짚습니다.** 그래서 UTF-8 로 저장된 한글이
        /// `?먰봽?먰봽` 처럼 깨져 보입니다. BOM 을 붙여 두면 엑셀이 UTF-8 로 알아봅니다.
        ///
        /// 반대로 엑셀에서 그냥 "CSV" 로 저장하면 CP949 로 쓰이는데, 그때는 Unity 가
        /// 한글을 못 읽습니다. 그 경우를 여기서 알아채고 UTF-8 로 되돌려 놓습니다.
        /// **덕분에 엑셀에서 어느 쪽으로 저장해도 한글이 깨지지 않습니다.**
        /// </summary>
        static void RepairEncoding(string path)
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch { return; }

            if (bytes.Length == 0) return;

            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            if (hasBom) return;   // 이미 제대로 되어 있습니다

            string text = null;
            bool wasLegacy = false;

            // BOM 이 없으면 먼저 UTF-8 로 읽어 봅니다. 깨진 바이트가 있으면 예외가 납니다.
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                // UTF-8 이 아니면 엑셀이 CP949 로 저장한 것입니다.
                try
                {
                    text = Encoding.GetEncoding(949).GetString(bytes);
                    wasLegacy = true;
                }
                catch
                {
                    Debug.LogWarning($"[Arcade] {FileName} 의 한글을 읽지 못했습니다.\n" +
                                     "  엑셀에서 [다른 이름으로 저장] > 파일 형식을 " +
                                     "**\"CSV UTF-8(쉼표로 분리)\"** 로 골라 다시 저장해 주세요.");
                    return;
                }
            }

            File.WriteAllText(path, text, new UTF8Encoding(true));   // BOM 을 붙여 다시 씁니다

            Debug.Log($"[Arcade] {FileName} 을(를) UTF-8 로 맞춰 두었습니다. " +
                      (wasLegacy
                          ? "엑셀이 옛 한글 인코딩으로 저장해서 되돌렸습니다."
                          : "엑셀에서 한글이 깨져 보이지 않도록 표시(BOM)를 붙였습니다."));
        }

        /// <summary>읽은 결과를 로그에 보기 좋게 늘어놓습니다. 줄바꿈은 기호로 바꿔 한 줄로 만듭니다.</summary>
        static string Describe(Dictionary<string, string> table)
        {
            var keys = new List<string>(table.Keys);
            keys.Sort(string.CompareOrdinal);

            var sb = new StringBuilder();
            foreach (var key in keys)
            {
                string text = table[key].Replace("\n", " / ");
                if (text.Length > 60) text = text.Substring(0, 57) + "...";
                sb.Append("  ").Append(key.PadRight(24)).Append(text).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Unity 프로젝트의 부모 폴더(D:\00.JumpJump)에 있는 strings.csv 경로.</summary>
        static string OutsidePath()
        {
            var projectDir = Directory.GetParent(Application.dataPath);
            var workingDir = projectDir != null ? projectDir.Parent : null;
            return workingDir != null ? Path.Combine(workingDir.FullName, FileName) : null;
        }
    }
}
