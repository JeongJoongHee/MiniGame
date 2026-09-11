using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JumpJump.EditorTools
{
    /// <summary>
    /// 안드로이드 APK 를 굽습니다. 씬 생성 -> 플레이어 설정 -> 빌드까지 한 번에 처리하므로
    /// Build Settings 창을 손으로 만질 필요가 없습니다.
    ///
    /// 결과물은 작업 폴더(D:\00.JumpJump)에 JumpJump.apk 로 떨어집니다.
    /// </summary>
    public static class JumpJumpBuild
    {
        const string ScenePath = "Assets/JumpJump/Scenes/GameScene.unity";
        const string ApkName = "JumpJump.apk";

        /// <summary>APK 에 들어가는 화면 순서. 0번이 앱을 켤 때 처음 열리는 씬입니다.</summary>
        static readonly string[] Scenes =
        {
            Arcade.EditorTools.ShellSceneBuilder.TitleScenePath,
            Arcade.EditorTools.ShellSceneBuilder.LobbyScenePath,
            ScenePath,                                                    // 점프점프
            Arcade.EditorTools.ShellSceneBuilder.ArcheryScenePath,        // 활쏘기
        };

        [MenuItem("Tools/JumpJump/Build Android APK", priority = 40)]
        public static void BuildApk()
        {
            // 타이틀 / 로비 / 게임 씬을 먼저 굽습니다. config.csv 동기화도 이 안에서 일어납니다.
            Arcade.EditorTools.ShellSceneBuilder.BuildAll();

            foreach (var scene in Scenes)
            {
                if (File.Exists(scene)) continue;
                Debug.LogError($"[JumpJump] {scene} 이(가) 없어 APK 를 만들 수 없습니다.");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[JumpJump] 빌드 대상을 Android 로 전환합니다...");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[JumpJump] Android 로 전환하지 못했습니다. Unity Hub 에서 Android Build Support 를 확인하세요.");
                    return;
                }
            }

            ApplyPlayerSettings();

            string output = Path.Combine(WorkingDir(), ApkName);
            Debug.Log($"[JumpJump] APK 빌드 시작 -> {output}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            LogResult(report, output);
        }

        static void ApplyPlayerSettings()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.companyName = "zkfpf";

            // 폰 홈 화면에 뜨는 앱 이름. 이제 미니게임 모음이라 앱 제목을 씁니다.
            var catalog = AssetDatabase.LoadAssetAtPath<Arcade.GameCatalog>(
                Arcade.EditorTools.ShellSceneBuilder.CatalogPath);
            PlayerSettings.productName = catalog != null && !string.IsNullOrEmpty(catalog.appTitle)
                ? catalog.appTitle
                : "JumpJump";
            PlayerSettings.SetApplicationIdentifier(android, "com.zkfpf.jumpjump");

            PlayerSettings.bundleVersion = "0.1";
            PlayerSettings.Android.bundleVersionCode = 1;

            // 세로 전용 게임입니다.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // 요즘 기기는 전부 64비트입니다. ARM64 하나만 구우면 빌드도 빨라집니다.
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // AAB(스토어 업로드용)가 아니라 기기에 바로 설치할 수 있는 APK 로 굽습니다.
            EditorUserBuildSettings.buildAppBundle = false;

            // 랭킹 서버와 통신합니다. 인터넷 권한을 확실히 넣어 둡니다
            // (Unity 가 알아서 넣어 주기도 하지만, 빠지면 폰에서 랭킹만 조용히 안 되므로 명시합니다).
            PlayerSettings.Android.forceInternetPermission = true;

            // 서명용 keystore 를 따로 만들지 않았으므로 Unity 의 디버그 키로 서명됩니다.
            // 기기에 직접 설치(사이드로드)하는 데는 문제가 없고, 스토어 업로드는 불가합니다.
            PlayerSettings.Android.useCustomKeystore = false;
        }

        static void LogResult(BuildReport report, string output)
        {
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[JumpJump] APK 빌드 실패: {summary.result} (오류 {summary.totalErrors}건)");
                return;
            }

            // summary.totalSize 는 압축 전 크기라 실제 APK 보다 훨씬 큽니다. 파일을 직접 잽니다.
            double megabytes = File.Exists(output)
                ? new FileInfo(output).Length / 1024.0 / 1024.0
                : summary.totalSize / 1024.0 / 1024.0;
            Debug.Log($"[JumpJump] APK 빌드 완료\n"
                      + $"  파일   : {output}\n"
                      + $"  크기   : {megabytes:0.0} MB\n"
                      + $"  걸린시간: {summary.totalTime.TotalMinutes:0.0}분\n"
                      + $"  패키지 : com.zkfpf.jumpjump  (ARM64 / IL2CPP / 디버그 서명)");
        }

        /// <summary>Unity 프로젝트의 부모 폴더(D:\00.JumpJump).</summary>
        static string WorkingDir()
        {
            var projectDir = Directory.GetParent(Application.dataPath);
            var workingDir = projectDir != null ? projectDir.Parent : null;
            return workingDir != null ? workingDir.FullName : Directory.GetCurrentDirectory();
        }
    }
}
