using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BuildTools
{
    /// <summary>
    /// Command-line and editor entry point for the reusable one-click build script.
    /// </summary>
    public static class OneClickBuild
    {
        private const string LogPrefix = "ONE_CLICK_BUILD";
        private const string DefaultBuildName = "BPLE";
        private const string DefaultWindowsTarget = "Windows64";
        private const string DefaultConfiguration = "Development";

        [MenuItem("Tools/一键打包/Windows x64 Development", false, 1)]
        private static void BuildWindowsDevelopment()
        {
            BuildInteractive(DefaultWindowsTarget, "Development", true);
        }

        [MenuItem("Tools/一键打包/Windows x64 Release", false, 2)]
        private static void BuildWindowsRelease()
        {
            BuildInteractive(DefaultWindowsTarget, "Release", true);
        }

        [MenuItem("Tools/一键打包/Android Development", false, 3)]
        private static void BuildAndroidDevelopment()
        {
            BuildInteractive("Android", "Development", false);
        }

        [MenuItem("Tools/一键打包/Linux x64 Development", false, 4)]
        private static void BuildLinuxDevelopment()
        {
            BuildInteractive("Linux64", "Development", false);
        }

        [MenuItem("Tools/一键打包/WebGL Development", false, 5)]
        private static void BuildWebGLDevelopment()
        {
            BuildInteractive("WebGL", "Development", false);
        }

        [MenuItem("Tools/一键打包/打开构建目录", false, 20)]
        private static void OpenBuildFolder()
        {
            string outputRoot = Path.Combine(GetProjectRoot(), "Builds");
            Directory.CreateDirectory(outputRoot);
            EditorUtility.RevealInFinder(outputRoot);
        }

        /// <summary>
        /// Entry point used by Tools/one-click-build.ps1 in Unity batch mode.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            int exitCode = 1;
            try
            {
                string targetName = GetCommandLineValue("-oneClickTarget", DefaultWindowsTarget);
                string configuration = GetCommandLineValue("-oneClickConfiguration",
                    string.Equals(targetName, "Android", StringComparison.OrdinalIgnoreCase) ? "Release" : DefaultConfiguration);
                string outputDirectory = GetCommandLineValue("-oneClickOutput", null);
                string buildName = GetCommandLineValue("-oneClickName", DefaultBuildName);
                exitCode = Build(targetName, configuration, outputDirectory, buildName, false) ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "_EXCEPTION " + exception);
            }

            EditorApplication.Exit(exitCode);
        }

        public static void BuildStarSeaTarget(string targetName)
        {
            BuildInteractive(targetName, "Release", false);
        }

        private static void BuildInteractive(string targetName, string configuration, bool launchAfterBuild)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string outputDirectory = Path.Combine(GetProjectRoot(), "Builds", "Editor", timestamp);
                string buildName = DefaultBuildName + "_" + targetName + "_" + configuration;
                bool success = Build(targetName, configuration, outputDirectory, buildName, true);
                if (success && launchAfterBuild)
                {
                    LaunchExecutable(FindBuiltExecutable(outputDirectory, ResolveTarget(targetName), buildName));
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "_EXCEPTION " + exception);
                EditorUtility.DisplayDialog("一键打包失败", exception.ToString(), "确定");
            }
        }

        private static bool Build(string targetName, string configuration, string outputDirectory, string buildName, bool interactive)
        {
            AssetDatabase.SaveAssets();

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                return Fail(interactive, "Build Settings 中没有已启用的场景。");
            }

            BuildTarget target = ResolveTarget(targetName);
            BuildTargetGroup targetGroup = ResolveTargetGroup(target);
            bool development = string.Equals(configuration, "Development", StringComparison.OrdinalIgnoreCase);
            string safeBuildName = SanitizeFileName(string.IsNullOrEmpty(buildName) ? DefaultBuildName : buildName);

            if (string.IsNullOrEmpty(outputDirectory))
            {
                outputDirectory = Path.Combine(GetProjectRoot(), "Builds", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            }
            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);

            string locationPathName = ResolveOutputPath(outputDirectory, safeBuildName, target);
            BuildOptions options = development
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : (target == BuildTarget.Android ? BuildOptions.CompressWithLz4HC : BuildOptions.None);

            Debug.Log(string.Format(
                "{0}_START target={1} group={2} configuration={3} scenes={4} output={5}",
                LogPrefix, target, targetGroup, development ? "Development" : "Release", scenes.Length, locationPathName));

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = locationPathName,
                target = target,
                targetGroup = targetGroup,
                options = options
            };

            BuildReport report;
            if (target == BuildTarget.Android && !development)
            {
                // Release Android builds use the same modern ABI and IL2CPP profile as the dedicated
                // optimized entry point; the compatibility Minimal profile remains separate.
                using (var settings = new AndroidOptimizedBuild.SettingsScope())
                {
                    settings.Apply();
                    report = BuildPipeline.BuildPlayer(buildOptions);
                }
            }
            else
            {
                report = BuildPipeline.BuildPlayer(buildOptions);
            }
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                return Fail(interactive, string.Format(
                    "打包失败：{0}，错误 {1} 个，警告 {2} 个。详情见 Console 或 build.log。",
                    summary.result, summary.totalErrors, summary.totalWarnings));
            }

            string outputPath = FindBuiltExecutable(outputDirectory, target, safeBuildName);
            string info = string.Format(
                "时间: {0:yyyy-MM-dd HH:mm:ss}\r\n" +
                "Unity: {1}\r\n" +
                "目标: {2} ({3})\r\n" +
                "配置: {4}\r\n" +
                "场景数: {5}\r\n" +
                "输出: {6}\r\n" +
                "大小: {7:F1} MB\r\n" +
                "耗时: {8:F1} 秒\r\n",
                DateTime.Now,
                Application.unityVersion,
                target,
                targetGroup,
                development ? "Development" : "Release",
                scenes.Length,
                outputPath,
                summary.totalSize / 1048576f,
                summary.totalTime.TotalSeconds);

            File.WriteAllText(Path.Combine(outputDirectory, "build-info.txt"), info);
            Debug.Log(string.Format(
                "{0}_SUCCEEDED path={1} size={2:F1}MB time={3:F1}s warnings={4}",
                LogPrefix, outputPath, summary.totalSize / 1048576f, summary.totalTime.TotalSeconds, summary.totalWarnings));

            if (interactive)
            {
                EditorUtility.DisplayDialog("一键打包完成", info, "确定");
            }
            return true;
        }

        private static string ResolveOutputPath(string outputDirectory, string buildName, BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return Path.Combine(outputDirectory, buildName + ".apk");
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(outputDirectory, buildName + ".exe");
                case BuildTarget.StandaloneLinux64:
                    return Path.Combine(outputDirectory, buildName + ".x86_64");
                case BuildTarget.WebGL:
                    return outputDirectory;
                default:
                    return Path.Combine(outputDirectory, buildName);
            }
        }

        private static string FindBuiltExecutable(string outputDirectory, BuildTarget target, string buildName)
        {
            string expected;
            switch (target)
            {
                case BuildTarget.Android:
                    expected = Path.Combine(outputDirectory, buildName + ".apk");
                    if (File.Exists(expected))
                    {
                        return expected;
                    }
                    return Directory.GetFiles(outputDirectory, "*.apk", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    expected = Path.Combine(outputDirectory, buildName + ".exe");
                    if (File.Exists(expected))
                    {
                        return expected;
                    }
                    return Directory.GetFiles(outputDirectory, "*.exe", SearchOption.TopDirectoryOnly)
                        .Where(path => !Path.GetFileName(path).StartsWith("UnityCrashHandler", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();
                case BuildTarget.StandaloneLinux64:
                    expected = Path.Combine(outputDirectory, buildName + ".x86_64");
                    if (File.Exists(expected))
                    {
                        return expected;
                    }
                    return Directory.GetFiles(outputDirectory, "*.x86_64", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();
                case BuildTarget.WebGL:
                    return Path.Combine(outputDirectory, "index.html");
                default:
                    return outputDirectory;
            }
        }

        private static BuildTarget ResolveTarget(string targetName)
        {
            switch ((targetName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "windows64":
                case "win64":
                    return BuildTarget.StandaloneWindows64;
                case "windows":
                case "windows32":
                case "win32":
                    return BuildTarget.StandaloneWindows;
                case "android":
                    return BuildTarget.Android;
                case "linux64":
                case "linux":
                    return BuildTarget.StandaloneLinux64;
                case "webgl":
                    return BuildTarget.WebGL;
                default:
                    throw new ArgumentException("不支持的构建目标: " + targetName);
            }
        }

        private static BuildTargetGroup ResolveTargetGroup(BuildTarget target)
        {
            if (target == BuildTarget.Android)
            {
                return BuildTargetGroup.Android;
            }
            if (target == BuildTarget.WebGL)
            {
                return BuildTargetGroup.WebGL;
            }
            return BuildTargetGroup.Standalone;
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string GetCommandLineValue(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return fallback;
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        }

        private static bool Fail(bool interactive, string message)
        {
            Debug.LogError(LogPrefix + "_FAILED " + message);
            if (interactive)
            {
                EditorUtility.DisplayDialog("一键打包失败", message, "确定");
            }
            return false;
        }

        private static void LaunchExecutable(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            {
                Debug.LogWarning("构建完成，但没有找到可启动文件。");
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = true
            });
        }
    }
}

