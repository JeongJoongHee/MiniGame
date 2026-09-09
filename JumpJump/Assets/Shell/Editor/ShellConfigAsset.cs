using System.IO;
using UnityEditor;
using UnityEngine;

namespace Arcade.EditorTools
{
    /// <summary>
    /// 껍데기 설정 에셋(<see cref="ArcadeConfig"/>)이 없으면 만들어 줍니다.
    /// 씬을 구울 때마다 확인하므로, 실수로 지워도 다음 빌드에서 기본값으로 되살아납니다.
    ///
    /// <b>Resources 폴더에 두는 것이 중요합니다</b> — 씬에 참조를 굽지 않고 실행 중에 찾아 쓰므로,
    /// 값만 고칠 때는 씬을 다시 굽지 않아도 됩니다. (글꼴 · 글자표와 같은 방식)
    /// </summary>
    public static class ShellConfigAsset
    {
        public const string Folder = "Assets/Shell/Resources";
        public const string Path = Folder + "/" + ArcadeConfig.ResourceName + ".asset";

        [MenuItem("Tools/Arcade/Select Arcade Config", priority = 22)]
        public static void SelectMenu()
        {
            Selection.activeObject = LoadOrCreate();
        }

        public static ArcadeConfig LoadOrCreate()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArcadeConfig>(Path);
            if (config != null) return config;

            Directory.CreateDirectory(Folder);
            config = ScriptableObject.CreateInstance<ArcadeConfig>();
            AssetDatabase.CreateAsset(config, Path);
            AssetDatabase.SaveAssets();

            Debug.Log("[Arcade] 설정 에셋을 새로 만들었습니다 -> " + Path +
                      "\n  광고 간격 · 랭킹 줄 수 · 별명 길이를 여기서 고칩니다.");
            return config;
        }
    }
}
