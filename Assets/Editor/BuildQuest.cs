using UnityEditor;
using UnityEngine;

// One-click Quest deploy: builds the Android APK and pushes it straight to
// the connected headset (AutoRunPlayer installs over the previous build and
// launches it).
public static class BuildQuest
{
    [MenuItem("Court Clash/Build And Run On Quest")]
    public static void BuildAndRun()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = "Builds/CourtClash.apk",
            target = BuildTarget.Android,
            options = BuildOptions.AutoRunPlayer,
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Court Clash build: {report.summary.result} in {report.summary.totalTime.TotalSeconds:0}s " +
            $"-> {report.summary.outputPath}");
    }
}
