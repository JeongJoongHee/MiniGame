using System.IO;
using UnityEditor;
using UnityEngine;

namespace Enchant.EditorTools
{
    /// <summary>
    /// 작업 폴더(D:\00.JumpJump)의 강화 표 `@Enchant_Sword.csv` 를 게임이 읽는 위치로 옮기고,
    /// 읽은 결과를 EnchantConfig 인스펙터에 구워서 눈으로 확인할 수 있게 합니다.
    /// 점프점프의 config.csv · 활쏘기의 archery.csv 와 같은 방식입니다.
    ///
    /// 작업 폴더에 `@Enchant_Sword.csv` 와 `Enchant_Sword.csv` 가 둘 다 있으면 **더 최근에 고친 쪽**을 씁니다.
    /// </summary>
    public static class EnchantTableCsv
    {
        public const string AssetPath = "Assets/Enchant/Enchant_Sword.csv";
        public const string ConfigPath = "Assets/Enchant/EnchantConfig.asset";
        static readonly string[] FileNames = { "@Enchant_Sword.csv", "Enchant_Sword.csv" };

        [MenuItem("Tools/Enchant/Apply Enchant_Sword.csv", priority = 10)]
        public static void Apply()
        {
            Sync();

            var config = AssetDatabase.LoadAssetAtPath<EnchantConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("[Enchant] EnchantConfig.asset 이 없습니다. 먼저 Tools > Enchant > Build Enchant Scene 을 실행하세요.");
                return;
            }

            Bake(config);
            AssetDatabase.SaveAssets();
        }

        /// <summary>CSV 를 읽어 config 에 연결하고 인스펙터 목록에 구워 둡니다. 읽은 표를 로그에 찍습니다.</summary>
        public static EnchantLevel[] Bake(EnchantConfig config)
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetPath);
            if (csv == null)
            {
                Debug.LogError($"[Enchant] {AssetPath} 을(를) 찾지 못했습니다. 작업 폴더에 @Enchant_Sword.csv 를 두세요.");
                config.InvalidateLevels();
                return config.Levels;
            }

            if (config.levelCsv != csv)
            {
                config.levelCsv = csv;
                EditorUtility.SetDirty(config);
            }
            config.InvalidateLevels();

            var levels = EnchantTable.Parse(csv.text, out string report);
            if (levels == null)
            {
                Debug.LogError("[Enchant] 강화 표를 읽지 못했습니다.\n  " + report);
                return config.Levels;
            }

            config.levels = levels;   // 인스펙터에서 눈으로 보고, CSV 가 깨졌을 때의 예비값
            EditorUtility.SetDirty(config);

            if (!string.IsNullOrEmpty(report))
                Debug.LogWarning("[Enchant] 강화 표를 읽었지만 손본 부분이 있습니다.\n" + report);

            Debug.Log($"[Enchant] 강화 표 {levels.Length}줄 (출처: {csv.name}.csv)\n" + EnchantTable.Describe(levels));
            return levels;
        }

        /// <summary>
        /// 작업 폴더의 표가 Assets 안의 것보다 새로우면 복사해 옵니다.
        /// 씬 빌드 / 플레이테스트 / 미리보기 앞에서 자동으로 불리므로, 사용자는 바깥 파일만 고치면 됩니다.
        /// </summary>
        public static void Sync()
        {
            string outside = OutsidePath();
            string inside = Path.GetFullPath(AssetPath);
            if (string.IsNullOrEmpty(outside)) return;

            bool needCopy = !File.Exists(inside)
                            || File.GetLastWriteTimeUtc(outside) > File.GetLastWriteTimeUtc(inside);
            if (!needCopy) return;

            Directory.CreateDirectory(Path.GetDirectoryName(inside));
            File.Copy(outside, inside, overwrite: true);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[Enchant] 작업 폴더의 강화 표가 더 새로워서 가져왔습니다.\n  {outside}\n  -> {AssetPath}");
        }

        /// <summary>작업 폴더에 있는 강화 표. 두 이름이 다 있으면 더 최근에 고친 쪽입니다. 없으면 null.</summary>
        static string OutsidePath()
        {
            string dir = Arcade.EditorTools.ShellArt.WorkingDir();
            string best = null;

            foreach (var name in FileNames)
            {
                string path = Path.Combine(dir, name);
                if (!File.Exists(path)) continue;
                if (best == null || File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(best)) best = path;
            }
            return best;
        }
    }
}
