using System.IO;
using UnityEditor;
using UnityEngine;

namespace Archery.EditorTools
{
    /// <summary>
    /// 작업 폴더(D:\00.JumpJump)의 archery.csv 를 게임이 읽는 위치로 옮겨 주고,
    /// 읽은 결과를 ArcheryConfig 인스펙터에 구워서 눈으로 확인할 수 있게 합니다.
    /// 점프점프의 JumpJumpConfigCsv 와 같은 방식입니다.
    ///
    /// 게임 자체는 Assets/Archery/archery.csv 를 직접 읽으므로, 엑셀에서 저장만 하면
    /// 다음 Play 부터 반영됩니다. 이 메뉴는 "지금 값이 뭔지 확인"과
    /// "바깥 폴더의 파일을 안으로 복사"를 담당합니다.
    /// </summary>
    public static class ArcheryStageCsv
    {
        public const string AssetPath = "Assets/Archery/archery.csv";
        public const string ConfigPath = "Assets/Archery/ArcheryConfig.asset";
        const string FileName = "archery.csv";

        [MenuItem("Tools/Archery/Apply archery.csv", priority = 10)]
        public static void Apply()
        {
            Sync();

            var config = AssetDatabase.LoadAssetAtPath<ArcheryConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("[Archery] ArcheryConfig.asset 이 없습니다. 먼저 Tools > Archery > Build Archery Scene 을 실행하세요.");
                return;
            }

            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
            if (csv == null)
            {
                Debug.LogError($"[Archery] {AssetPath} 을(를) 찾지 못했습니다. 작업 폴더에 {FileName} 을 두거나 이 경로에 직접 넣어 주세요.");
                return;
            }

            var bands = StageTable.Parse(csv.text, out string report);
            if (bands == null)
            {
                Debug.LogError($"[Archery] {FileName} 을(를) 읽지 못했습니다.\n  {report}");
                return;
            }

            config.stageCsv = csv;
            config.stageBands = bands;   // 인스펙터에서 눈으로 볼 수 있게 구워 둡니다
            config.InvalidateBands();

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(report))
                Debug.LogWarning($"[Archery] {FileName} 을(를) 읽었지만 손본 부분이 있습니다.\n{report}");

            Debug.Log($"[Archery] {FileName} 적용 완료 ({bands.Length}개 구간)\n" + StageTable.Describe(bands));
        }

        /// <summary>
        /// 작업 폴더의 archery.csv 가 Assets 안의 것보다 새로우면 복사해 옵니다.
        /// 씬 빌드 / 플레이테스트 / 미리보기 앞에서 자동으로 호출되므로,
        /// 사용자는 바깥 폴더의 파일만 고쳐도 됩니다.
        ///
        /// 작업 폴더에 파일이 아예 없으면 Assets 안의 사본을 그대로 내보내 줍니다.
        /// (엑셀로 열어 고칠 수 있도록)
        /// </summary>
        public static void Sync()
        {
            string outside = OutsidePath();
            string inside = Path.GetFullPath(AssetPath);

            if (string.IsNullOrEmpty(outside)) return;
            if (string.Equals(Path.GetFullPath(outside), inside, System.StringComparison.OrdinalIgnoreCase)) return;

            if (!File.Exists(outside))
            {
                if (!File.Exists(inside)) return;
                File.Copy(inside, outside, overwrite: false);
                Debug.Log($"[Archery] 작업 폴더에 {FileName} 을(를) 내보냈습니다. 엑셀로 여기를 고치면 됩니다.\n  {outside}");
                return;
            }

            bool needCopy = !File.Exists(inside)
                            || File.GetLastWriteTimeUtc(outside) > File.GetLastWriteTimeUtc(inside);
            if (!needCopy) return;

            Directory.CreateDirectory(Path.GetDirectoryName(inside));
            File.Copy(outside, inside, overwrite: true);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[Archery] 작업 폴더의 {FileName} 이(가) 더 새로워서 가져왔습니다.\n  {outside}\n  -> {AssetPath}");
        }

        /// <summary>Unity 프로젝트의 부모 폴더(D:\00.JumpJump)에 있는 archery.csv 경로.</summary>
        static string OutsidePath()
        {
            // Application.dataPath = <프로젝트>/Assets  ->  부모의 부모가 작업 폴더입니다.
            var projectDir = Directory.GetParent(Application.dataPath);
            var workingDir = projectDir != null ? projectDir.Parent : null;
            return workingDir != null ? Path.Combine(workingDir.FullName, FileName) : null;
        }
    }
}
