using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CaptainPinkTurd.Story.Editor
{
    /// <summary>
    /// Builds the Android APK from the command line (close the editor first, it holds a project lock):
    ///   Unity.exe -batchmode -quit -projectPath D:/biformis_android -buildTarget Android
    ///     -executeMethod CaptainPinkTurd.Story.Editor.AndroidBuild.BuildApk -logFile D:/biformis_android/Logs/build.log
    /// It builds the enabled Build Settings scenes with the player settings as they are (IL2CPP, ARM64, debug
    /// keystore) into Builds/Biformis.apk, and exits non-zero if the build fails.
    /// </summary>
    public static class AndroidBuild
    {
        private const string OutputPath = "Builds/Biformis.apk";

        public static void BuildApk()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            //summary.totalSize counts the content before compression; the APK's own size is what gets installed
            long apkBytes = File.Exists(OutputPath) ? new FileInfo(OutputPath).Length : 0;
            Debug.Log($"[AndroidBuild] {summary.result}: {summary.outputPath}, {apkBytes / (1024f * 1024f):0.0} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalTime:mm\\:ss}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
