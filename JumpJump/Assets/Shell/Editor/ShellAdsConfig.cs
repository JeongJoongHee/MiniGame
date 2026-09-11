using System;
using UnityEditor;
using UnityEngine;

namespace Arcade.EditorTools
{
    /// <summary>
    /// **AdMob 앱 ID 를 광고 플러그인 설정에 옮겨 적습니다.** Build All Scenes 때마다 자동으로 불립니다.
    ///
    /// 앱 ID 의 원본은 <see cref="ArcadeConfig"/> 의 <c>admobAppId</c> 하나입니다. 그런데 광고 플러그인은
    /// 자기 설정 파일(<c>Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset</c>)에 적힌 값을
    /// 안드로이드 매니페스트에 넣고, **비어 있으면 APK 빌드를 멈춥니다.** 두 곳을 손으로 맞추다 어긋나지 않도록
    /// 여기서 한쪽 방향으로만 옮겨 적습니다.
    ///
    /// 플러그인의 설정 클래스는 바깥에서 부를 수 없게 잠겨 있어서(internal), 파일의 칸 이름으로 직접 씁니다.
    /// </summary>
    public static class ShellAdsConfig
    {
        const string SettingsTypeName = "GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor";
        const string AndroidAppIdField = "adMobAndroidAppId";

        [MenuItem("Tools/Arcade/Apply AdMob App ID", priority = 26)]
        public static void ApplyMenu()
        {
            Sync();
            Selection.activeObject = ShellConfigAsset.LoadOrCreate();
        }

        /// <summary>옮겨 적습니다. 플러그인 설정의 앱 ID 가 ArcadeConfig 와 같아졌으면 true.</summary>
        public static bool Sync()
        {
            var config = ShellConfigAsset.LoadOrCreate();
            string appId = (config.admobAppId ?? "").Trim();

            var settings = LoadPluginSettings();
            if (settings == null)
            {
                Debug.LogWarning("[Arcade] 광고 플러그인(Google Mobile Ads) 설정을 찾지 못했습니다. 패키지가 들어 있는지 확인하세요.");
                return false;
            }

            var so = new SerializedObject(settings);
            var prop = so.FindProperty(AndroidAppIdField);
            if (prop == null)
            {
                Debug.LogError("[Arcade] 광고 플러그인 설정에서 '" + AndroidAppIdField + "' 칸을 찾지 못했습니다. " +
                               "플러그인 버전이 바뀌었으면 ShellAdsConfig 의 칸 이름을 확인하세요.");
                return false;
            }

            if (prop.stringValue != appId)
            {
                prop.stringValue = appId;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Debug.Log("[Arcade] AdMob 앱 ID 를 광고 플러그인 설정에 옮겨 적었습니다 -> " + appId);
            }

            if (string.IsNullOrEmpty(appId))
                Debug.LogWarning("[Arcade] AdMob 앱 ID 가 비어 있습니다 (ArcadeConfig.admobAppId). 이대로는 APK 빌드가 멈춥니다.");
            else if (!config.useTestAds)
                Debug.LogWarning("[Arcade] ★ 진짜 광고로 설정되어 있습니다 (useTestAds 꺼짐). " +
                                 "스토어에 올릴 빌드가 아니면 켜 두세요 — 내 광고를 내가 누르면 계정이 정지될 수 있습니다.");

            return true;
        }

        /// <summary>지금 플러그인 설정에 적힌 안드로이드 앱 ID (점검용).</summary>
        public static string PluginAndroidAppId()
        {
            var settings = LoadPluginSettings();
            if (settings == null) return null;
            return new SerializedObject(settings).FindProperty(AndroidAppIdField)?.stringValue;
        }

        /// <summary>플러그인 설정 파일. 없으면 플러그인이 스스로 만들게 합니다.</summary>
        static ScriptableObject LoadPluginSettings()
        {
            var type = Type.GetType(SettingsTypeName);
            if (type == null) return null;

            var load = type.GetMethod("LoadInstance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            return load != null ? load.Invoke(null, null) as ScriptableObject : null;
        }
    }
}
