using System.Collections.Generic;
using System.IO;
using Arcade.Online;
using UnityEditor;
using UnityEngine;

namespace Arcade.EditorTools
{
    /// <summary>
    /// 작업 폴더의 **`google-services.json`** 에서 랭킹 서버 주소(프로젝트 ID · API 키)를 꺼내
    /// <see cref="ArcadeConfig"/> 에 넣어 줍니다. Build All Scenes 때마다 자동으로 불립니다.
    ///
    /// Firebase SDK 를 쓰면 이 파일을 프로젝트에 넣고 SDK 가 읽게 하지만, 이 앱은 SDK 없이
    /// 서버 주소로 직접 요청하므로 **값 두 개만** 있으면 됩니다. 그래서 파일은 작업 폴더에 그대로 두고
    /// 값만 설정 에셋에 옮겨 적습니다. (난이도 CSV 를 가져오는 것과 같은 방식입니다)
    ///
    /// 두 값은 비밀번호가 아닙니다 — 앱 안에 그대로 들어가고, 누가 무엇을 할 수 있는지는
    /// 서버의 보안 규칙(<c>firestore.rules</c>)이 정합니다.
    /// </summary>
    public static class ShellFirebaseConfig
    {
        const string FileName = "google-services.json";

        [MenuItem("Tools/Arcade/Apply google-services.json", priority = 24)]
        public static void ApplyMenu()
        {
            if (Sync()) Selection.activeObject = ShellConfigAsset.LoadOrCreate();
        }

        /// <summary>값을 옮겨 적습니다. 파일이 있고 읽을 수 있었으면 true.</summary>
        public static bool Sync()
        {
            string path = ShellStringsCsv.OutsidePath(FileName);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.Log("[Arcade] 작업 폴더에 " + FileName + " 이(가) 없습니다. 랭킹은 폰 안에만 저장됩니다.");
                return false;
            }

            object json = MiniJson.Parse(File.ReadAllText(path));
            string projectId = MiniJson.DigString(json, "project_info", "project_id");
            string packageName = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            string apiKey = FindApiKey(json, packageName, out string matchedPackage);

            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[Arcade] " + FileName + " 에서 프로젝트 ID 나 API 키를 찾지 못했습니다.\n" +
                               "  Firebase 콘솔 > 프로젝트 설정 > 내 앱 에서 파일을 다시 받아 주세요.");
                return false;
            }

            if (!string.IsNullOrEmpty(packageName) && matchedPackage != packageName)
            {
                Debug.LogWarning("[Arcade] " + FileName + " 의 패키지 이름(" + matchedPackage + ")이 앱(" + packageName +
                                 ")과 다릅니다. 랭킹은 돌아가지만, Firebase 콘솔에 앱을 등록할 때 패키지 이름을 확인해 주세요.");
            }

            var config = ShellConfigAsset.LoadOrCreate();
            if (config.firebaseProjectId == projectId && config.firebaseApiKey == apiKey) return true;

            config.firebaseProjectId = projectId;
            config.firebaseApiKey = apiKey;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log("[Arcade] " + FileName + " 에서 랭킹 서버 주소를 가져왔습니다 -> " + ShellConfigAsset.Path +
                      "\n  프로젝트 : " + projectId);
            return true;
        }

        /// <summary>
        /// 이 앱(패키지 이름)에 해당하는 API 키. 한 프로젝트에 앱을 여럿 등록했으면 파일에 여러 개가 들어 있습니다.
        /// 맞는 앱이 없으면 첫 번째 것을 씁니다.
        /// </summary>
        static string FindApiKey(object json, string packageName, out string matchedPackage)
        {
            matchedPackage = null;
            if (!(MiniJson.Dig(json, "client") is List<object> clients)) return null;

            string firstKey = null, firstPackage = null;
            foreach (var client in clients)
            {
                string package = MiniJson.DigString(client, "client_info", "android_client_info", "package_name");
                string key = null;
                if (MiniJson.Dig(client, "api_key") is List<object> keys && keys.Count > 0)
                    key = MiniJson.DigString(keys[0], "current_key");
                if (string.IsNullOrEmpty(key)) continue;

                if (firstKey == null) { firstKey = key; firstPackage = package; }
                if (package == packageName)
                {
                    matchedPackage = package;
                    return key;
                }
            }

            matchedPackage = firstPackage;
            return firstKey;
        }
    }
}
