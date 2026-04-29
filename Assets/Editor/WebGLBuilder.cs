using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Linq;

public class WebGLBuilder
{
    [MenuItem("Build/Build WebGL (Production)")]
    public static void BuildWebGL()
    {
        string buildPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "WebGL");
        Build(buildPath, BuildOptions.None);
    }

    [MenuItem("Build/Build WebGL (Development)")]
    public static void BuildWebGLDev()
    {
        string buildPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "WebGL-Dev");
        Build(buildPath, BuildOptions.Development);
    }

    private static void Build(string path, BuildOptions options)
    {
        Directory.CreateDirectory(path);

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("No scenes in Build Settings!");
            EditorApplication.Exit(1);
            return;
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = path,
            target = BuildTarget.WebGL,
            options = options
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"WebGL build succeeded: {summary.totalSize / 1024 / 1024} MB");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"WebGL build failed: {summary.totalErrors} errors");
            EditorApplication.Exit(1);
        }
    }
}
