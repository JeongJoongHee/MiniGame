using System.IO;
using UnityEditor;
using UnityEngine;

namespace JumpJump.EditorTools
{
    /// <summary>
    /// 프로젝트 폴더(D:\00.JumpJump)의 config.csv 를 게임이 읽는 위치로 옮겨 주고,
    /// 읽은 결과를 GameConfig 인스펙터에 구워서 눈으로 확인할 수 있게 합니다.
    ///
    /// 게임 자체는 Assets/JumpJump/config.csv 를 직접 읽으므로, 엑셀에서 저장만 하면
    /// 다음 Play 부터 반영됩니다. 이 메뉴는 "지금 값이 뭔지 확인"과
    /// "바깥 폴더의 파일을 안으로 복사"를 담당합니다.
    /// </summary>
    public static class JumpJumpConfigCsv
    {
        public const string AssetPath = "Assets/JumpJump/config.csv";
        const string ConfigPath = "Assets/JumpJump/GameConfig.asset";
        const string FileName = "config.csv";

        [MenuItem("Tools/JumpJump/Apply config.csv", priority = 10)]
        public static void Apply()
        {
            Sync();

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("[JumpJump] GameConfig.asset 이 없습니다. 먼저 Tools > JumpJump > Build Game Scene 을 실행하세요.");
                return;
            }

            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
            if (csv == null)
            {
                Debug.LogError($"[JumpJump] {AssetPath} 을(를) 찾지 못했습니다. 프로젝트 폴더에 {FileName} 을 두거나 이 경로에 직접 넣어 주세요.");
                return;
            }

            var bands = DifficultyTable.Parse(csv.text, out string report);
            if (bands == null)
            {
                Debug.LogError($"[JumpJump] {FileName} 을(를) 읽지 못했습니다.\n  {report}");
                return;
            }

            config.difficultyCsv = csv;
            config.difficultyBands = bands;   // 인스펙터에서 눈으로 볼 수 있게 구워 둡니다
            config.InvalidateBands();

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(report))
                Debug.LogWarning($"[JumpJump] {FileName} 을(를) 읽었지만 손본 부분이 있습니다.\n{report}");

            Debug.Log($"[JumpJump] {FileName} 적용 완료 ({bands.Length}개 구간)\n"
                      + DifficultyTable.Describe(bands, config.rowSpacing * config.metersPerUnit));
        }

        /// <summary>
        /// 프로젝트 폴더의 config.csv 가 Assets 안의 것보다 새로우면 복사해 옵니다.
        /// 씬 빌드 / 플레이테스트 / 미리보기 앞에서 자동으로 호출되므로,
        /// 사용자는 바깥 폴더의 파일만 고쳐도 됩니다.
        /// </summary>
        public static void Sync()
        {
            string outside = OutsidePath();
            string inside = Path.GetFullPath(AssetPath);

            if (string.IsNullOrEmpty(outside) || !File.Exists(outside)) return;

            // 같은 파일을 가리키는 경우(프로젝트 배치가 바뀐 경우) 아무것도 하지 않습니다.
            if (string.Equals(Path.GetFullPath(outside), inside, System.StringComparison.OrdinalIgnoreCase)) return;

            bool needCopy = !File.Exists(inside)
                            || File.GetLastWriteTimeUtc(outside) > File.GetLastWriteTimeUtc(inside);
            if (!needCopy) return;

            Directory.CreateDirectory(Path.GetDirectoryName(inside));
            File.Copy(outside, inside, overwrite: true);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[JumpJump] 바깥 폴더의 {FileName} 이(가) 더 새로워서 가져왔습니다.\n  {outside}\n  -> {AssetPath}");
        }

        /// <summary>Unity 프로젝트의 부모 폴더(D:\00.JumpJump)에 있는 config.csv 경로.</summary>
        static string OutsidePath()
        {
            // Application.dataPath = <프로젝트>/Assets  ->  부모의 부모가 작업 폴더입니다.
            var projectDir = Directory.GetParent(Application.dataPath);
            var workingDir = projectDir != null ? projectDir.Parent : null;
            return workingDir != null ? Path.Combine(workingDir.FullName, FileName) : null;
        }
    }
}
