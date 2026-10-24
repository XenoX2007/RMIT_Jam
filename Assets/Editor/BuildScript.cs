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
        // Full HD: the Itch1080 template always renders 1920x1080 and scales it to fit the page / fullscreen
        PlayerSettings.defaultWebScreenWidth = 1920;
        PlayerSettings.defaultWebScreenHeight = 1080;
        PlayerSettings.WebGL.template = "PROJECT:Itch1080";
        PlayerSettings.runInBackground = true;

        // Same look as the Editor: WebGL uses the PC quality level / PC URP asset (shadows, soft particles, full render scale)
        UseQualityLevelForWebGL("PC");

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

        // WebGLPerformance turns on GPU instancing at runtime for the trees/bushes (their materials live
        // inside the FBX files), so the instancing shader variants must not be stripped from this build
        int instancingStripping = SetInstancingStripping(2);   // 2 = Keep All
        BuildReport report;
        try { report = BuildPipeline.BuildPlayer(options); }
        finally { SetInstancingStripping(instancingStripping); }
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

    private const string WindowsFolder = "Builds/Windows/Crocodiles Comes Ashore";

    // Downloadable PC build: Builds/Windows/Crocodiles Comes Ashore/Crocodiles Comes Ashore.exe
    // Batch mode:  Unity.exe -batchmode -quit -projectPath <project> -buildTarget Win64 -executeMethod BuildScript.BuildWindows -logFile build.log
    [MenuItem("Build/Windows (PC download)")]
    public static void BuildWindows()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) Fail("No scenes are enabled in Build Settings.");

        if (Directory.Exists(WindowsFolder)) Directory.Delete(WindowsFolder, true);
        Directory.CreateDirectory(WindowsFolder);

        string exe = Path.Combine(WindowsFolder, "Crocodiles Comes Ashore.exe");
        Debug.Log("[BuildScript] Building Windows with " + scenes.Length + " scenes:\n  " + string.Join("\n  ", scenes));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        BuildSummary summary = report.summary;
        Debug.Log("[BuildScript] Result: " + summary.result + ", size " + (summary.totalSize / (1024f * 1024f)).ToString("F1") +
                  " MB, " + summary.totalErrors + " errors, " + summary.totalWarnings + " warnings, time " + summary.totalTime);
        if (summary.result != BuildResult.Succeeded || !File.Exists(exe)) Fail("Windows build " + summary.result);

        // Burst debug symbols must not be shipped with the game
        foreach (string dir in Directory.GetDirectories(WindowsFolder, "*_DoNotShip"))
        {
            string dest = Path.Combine("Builds", "Windows_" + Path.GetFileName(dir));
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            Directory.Move(dir, dest);
        }
        Debug.Log("[BuildScript] Windows build OK: " + Path.GetFullPath(exe));
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // Set the default quality level used by WebGL builds (Project Settings > Quality, the WebGL column)
    private static void UseQualityLevelForWebGL(string levelName)
    {
        int level = Array.IndexOf(QualitySettings.names, levelName);
        if (level < 0) Fail("No quality level named " + levelName);
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
        var so = new SerializedObject(asset);
        SerializedProperty list = so.FindProperty("m_PerPlatformDefaultQuality");
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == "WebGL")
                entry.FindPropertyRelative("second").intValue = level;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildScript] WebGL quality level: " + levelName);
    }

    // Project Settings > Graphics > Instancing Variants; returns the previous value
    private static int SetInstancingStripping(int value)
    {
        var so = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
        SerializedProperty prop = so.FindProperty("m_InstancingStripping");
        int old = prop.intValue;
        prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return old;
    }

    private static void Fail(string message)
    {
        Debug.LogError("[BuildScript] " + message);
        if (Application.isBatchMode) EditorApplication.Exit(1);
        throw new Exception(message);
    }
}
