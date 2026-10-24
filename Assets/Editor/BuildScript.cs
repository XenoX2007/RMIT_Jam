using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// WebGL build for itch.io.
// Batch mode:  Unity.exe -batchmode -quit -projectPath <project> -executeMethod BuildScript.BuildWebGL -logFile build.log
// In the Editor: menu Build > WebGL (itch.io)
// Output: Builds/WebGL (index.html at the root), Gzip compression with decompression fallback,
// using the scenes that are enabled in Build Settings.
public static class BuildScript
{
    private const string OutputPath = "Builds/WebGL";

    [MenuItem("Build/WebGL (itch.io)")]
    public static void BuildWebGL()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) Fail("No scenes are enabled in Build Settings.");

        // itch.io serves the files without Content-Encoding headers, so the loader must decompress itself
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        // canvas size of the page; set the same Viewport dimensions on itch.io (Embed options)
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = true;

        if (Directory.Exists(OutputPath)) Directory.Delete(OutputPath, true);
        Directory.CreateDirectory(OutputPath);

        Debug.Log("[BuildScript] Building WebGL with " + scenes.Length + " scenes:\n  " + string.Join("\n  ", scenes));
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log("[BuildScript] Result: " + summary.result + ", size " + (summary.totalSize / (1024f * 1024f)).ToString("F1") +
                  " MB, " + summary.totalErrors + " errors, " + summary.totalWarnings + " warnings, time " + summary.totalTime);

        if (summary.result != BuildResult.Succeeded)
        {
            foreach (BuildStep step in report.steps)
                foreach (BuildStepMessage m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        Debug.LogError("[BuildScript] " + step.name + ": " + m.content);
            Fail("WebGL build " + summary.result);
        }

        string index = Path.Combine(OutputPath, "index.html");
        if (!File.Exists(index)) Fail("index.html is missing from " + OutputPath);

        // Render at 1 pixel per CSS pixel: on high-DPI screens the browser would otherwise render 1.5-2x the pixels
        string html = File.ReadAllText(index);
        const string create = "createUnityInstance(canvas, config,";
        if (html.Contains(create) && !html.Contains("config.devicePixelRatio = 1; createUnityInstance"))
            File.WriteAllText(index, html.Replace(create, "config.devicePixelRatio = 1; " + create));

        // Unity's Burst debug symbols must not be uploaded with the game
        foreach (string dir in Directory.GetDirectories(OutputPath, "*_DoNotShip"))
        {
            string dest = Path.Combine("Builds", Path.GetFileName(dir));
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            Directory.Move(dir, dest);
        }
        Debug.Log("[BuildScript] WebGL build OK: " + Path.GetFullPath(OutputPath));
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    private static void Fail(string message)
    {
        Debug.LogError("[BuildScript] " + message);
        if (Application.isBatchMode) EditorApplication.Exit(1);
        throw new Exception(message);
    }
}
