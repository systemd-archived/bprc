using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BuildTools
{
    public static class AndroidMinimalBuild
    {
        private const string LogPrefix = "ANDROID_MINIMAL_BUILD";
        private static readonly string[] ExcludedDirectories =
        {
            "AssetBundles/Windows", "AssetBundles/Linux", "AssetBundles/macOS",
            "AssetBundles/iOS", "easytier/win-x64"
        };

        internal static List<string> RemovedFiles;
        internal static long RemovedBytes;
        internal static bool FilterApplied;

        [MenuItem("Tools/Build/Build Minimal Android APK")]
        public static void BuildInteractive()
        {
            try
            {
                string output = Path.Combine(GetProjectRoot(), "Builds", "AndroidMinimal",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"), "BPLE-Minimal.apk");
                Build(output);
                EditorUtility.RevealInFinder(output);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "_FAILED " + exception);
                EditorUtility.DisplayDialog("Minimal Android build failed", exception.Message, "OK");
            }
        }

        public static void BuildFromCommandLine()
        {
            int exitCode = 1;
            try
            {
                string directory = GetArgument("-oneClickOutput",
                    Path.Combine(GetProjectRoot(), "Builds", "AndroidMinimal"));
                string name = GetArgument("-oneClickName", "BPLE-Minimal");
                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new ArgumentException("Invalid APK file name: " + name);
                Build(Path.Combine(directory, name + ".apk"));
                exitCode = 0;
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "_FAILED " + exception);
            }
            EditorApplication.Exit(exitCode);
        }

        public static void Build(string outputPath)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Select Android in Build Settings first; batch mode must use -buildTarget Android.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Install Android Build Support, SDK, NDK and OpenJDK for this Unity editor.");

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new BuildFailedException("No enabled scenes in Build Settings.");
            if (!Directory.Exists(Path.Combine(Application.streamingAssetsPath, "AssetBundles", "Android")))
                throw new BuildFailedException("Android AssetBundles are missing. Run Tools/AssetBundles/Rebuild Android first.");

            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            AssetDatabase.SaveAssets();
            RemovedFiles = new List<string>();
            RemovedBytes = 0;
            FilterApplied = false;
            try
            {
                using (var settings = new SettingsScope())
                {
                    settings.Apply();
                    BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = outputPath,
                        target = BuildTarget.Android,
                        targetGroup = BuildTargetGroup.Android,
                        options = BuildOptions.CompressWithLz4HC | BuildOptions.DetailedBuildReport
                    });
                    if (report.summary.result != BuildResult.Succeeded)
                        throw new BuildFailedException("Android build " + report.summary.result
                            + "; errors=" + report.summary.totalErrors + ". See build.log / Console.");
                    if (!FilterApplied)
                        throw new BuildFailedException("The Android staging filter did not run; APK was not verified as minimal.");
                    WritePackageReport(outputPath, report.summary);
                }
                Debug.Log(LogPrefix + "_SUCCEEDED " + outputPath);
            }
            finally
            {
                RemovedFiles = null;
                RemovedBytes = 0;
                FilterApplied = false;
            }
        }

        // Only copied Gradle assets are filtered. Source assets and importer metadata stay in place.
        internal static void FilterGradleProject(string unityLibraryPath)
        {
            if (RemovedFiles == null) return;
            string assetsRoot = Path.GetFullPath(Path.Combine(unityLibraryPath, "src", "main", "assets"));
            string sourceRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (assetsRoot.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Refusing to filter source Assets: " + assetsRoot);
            if (!Directory.Exists(Path.Combine(assetsRoot, "AssetBundles", "Android")))
                throw new BuildFailedException("Android bundles not found in generated Gradle assets: " + assetsRoot);

            foreach (string relative in ExcludedDirectories)
            {
                string directory = Path.Combine(assetsRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(directory)) continue;
                foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                    RecordRemoval(assetsRoot, file);
                Directory.Delete(directory, true);
            }

            // Legacy root-level bundles are duplicated by the platform-specific Android folder.
            string bundleRoot = Path.Combine(assetsRoot, "AssetBundles");
            var androidBundleNames = new HashSet<string>(Directory.GetFiles(Path.Combine(bundleRoot, "Android"))
                .Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.GetFiles(bundleRoot, "*.unity3d"))
            {
                if (!androidBundleNames.Contains(Path.GetFileName(file))) continue;
                RecordRemoval(assetsRoot, file);
                File.Delete(file);
            }
            FilterApplied = true;
            Debug.Log(LogPrefix + "_FILTER removed=" + RemovedFiles.Count
                + " bytes=" + RemovedBytes + " stagedAssets=" + assetsRoot);
        }

        private static void RecordRemoval(string assetsRoot, string file)
        {
            RemovedBytes += new FileInfo(file).Length;
            RemovedFiles.Add(file.Substring(assetsRoot.Length + 1).Replace('\\', '/'));
        }

        private static void WritePackageReport(string apkPath, BuildSummary summary)
        {
            var text = new StringBuilder();
            text.AppendLine("Minimal Android APK report");
            text.AppendLine("Unity: " + Application.unityVersion);
            text.AppendLine("APK: " + apkPath);
            text.AppendLine("Profile: Release / Mono / ARMv7 / LZ4HC");
            text.AppendLine("Managed stripping: Disabled (runtime C# and reflection compatibility)");
            text.AppendLine("APK bytes: " + new FileInfo(apkPath).Length);
            text.AppendLine("Build seconds: " + summary.totalTime.TotalSeconds.ToString("F1"));
            text.AppendLine("Excluded staged bytes: " + RemovedBytes);
            text.AppendLine();
            using (ZipArchive archive = ZipFile.OpenRead(apkPath))
            {
                if (!archive.Entries.Any(entry => entry.FullName.StartsWith("assets/AssetBundles/Android/", StringComparison.Ordinal)))
                    throw new BuildFailedException("APK has no Android AssetBundles.");
                foreach (string relative in ExcludedDirectories)
                    if (archive.Entries.Any(entry => entry.FullName.StartsWith("assets/" + relative + "/", StringComparison.OrdinalIgnoreCase)))
                        throw new BuildFailedException("Excluded platform files remain in APK: " + relative);

                text.AppendLine("APK groups (compressed MiB / uncompressed MiB / files)");
                foreach (var group in archive.Entries.Where(entry => !entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    .GroupBy(entry => GetPackageGroup(entry.FullName))
                    .OrderByDescending(group => group.Sum(entry => entry.CompressedLength)))
                {
                    text.AppendLine(string.Format("{0:F2} / {1:F2} / {2}  {3}",
                        group.Sum(entry => entry.CompressedLength) / 1048576d,
                        group.Sum(entry => entry.Length) / 1048576d, group.Count(), group.Key));
                }
                text.AppendLine();
                text.AppendLine("Largest APK entries (compressed MiB / uncompressed MiB)");
                foreach (ZipArchiveEntry entry in archive.Entries.OrderByDescending(entry => entry.CompressedLength).Take(20))
                    text.AppendLine(string.Format("{0:F2} / {1:F2}  {2}",
                        entry.CompressedLength / 1048576d, entry.Length / 1048576d, entry.FullName));
            }
            text.AppendLine();
            text.AppendLine("Excluded copied files");
            foreach (string file in RemovedFiles) text.AppendLine(file);
            string reportPath = Path.Combine(Path.GetDirectoryName(apkPath), "android-size-report.txt");
            File.WriteAllText(reportPath, text.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(apkPath), "build-info.txt"),
                "Profile: Android Minimal Release (Mono ARMv7)\r\nOutput: " + apkPath
                + "\r\nSize: " + (new FileInfo(apkPath).Length / 1048576d).ToString("F2")
                + " MiB\r\nSize report: " + reportPath + "\r\n", new UTF8Encoding(false));
            Debug.Log(LogPrefix + "_REPORT " + reportPath);
        }

        private static string GetPackageGroup(string path)
        {
            if (path.StartsWith("assets/bin/Data/Managed/", StringComparison.Ordinal)) return "Managed code";
            if (path.StartsWith("assets/AssetBundles/", StringComparison.Ordinal)) return "Android AssetBundles";
            if (path.StartsWith("assets/bin/Data/", StringComparison.Ordinal)) return "Unity scenes / Resources / shared data";
            if (path.StartsWith("lib/", StringComparison.Ordinal)) return "Native libraries: " + path.Split('/')[1];
            if (path.StartsWith("assets/", StringComparison.Ordinal)) return "Other StreamingAssets";
            if (path.EndsWith(".dex", StringComparison.Ordinal)) return "Java / DEX";
            return "Android resources / manifest / signatures";
        }

        private static string GetProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string GetArgument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return fallback;
        }

        private sealed class SettingsScope : IDisposable
        {
            private readonly ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            private readonly ManagedStrippingLevel stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android);
            private readonly AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;
            private readonly bool stripEngine = PlayerSettings.stripEngineCode;
            private readonly bool expansion = PlayerSettings.Android.useAPKExpansionFiles;
            private readonly bool perCpu = PlayerSettings.Android.buildApkPerCpuArchitecture;
            private readonly float maxAspectRatio = PlayerSettings.Android.maxAspectRatio;
            private readonly bool resizableWindow = PlayerSettings.Android.resizableWindow;
            private readonly bool outsideSafeArea = PlayerSettings.Android.renderOutsideSafeArea;
            private readonly bool fullscreen = PlayerSettings.Android.startInFullscreen;
            private readonly bool framePacing = PlayerSettings.Android.optimizedFramePacing;
            private readonly bool appBundle = EditorUserBuildSettings.buildAppBundle;
            private readonly bool exportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            private readonly bool development = EditorUserBuildSettings.development;
            private readonly bool debugging = EditorUserBuildSettings.allowDebugging;
            private readonly bool profiler = EditorUserBuildSettings.connectProfiler;
            private readonly AndroidCreateSymbols symbols = EditorUserBuildSettings.androidCreateSymbols;

            public void Apply()
            {
                // Mono is necessary for INCommandManager's ScriptEngine and external C# addons.
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.Mono2x);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Disabled);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
                PlayerSettings.stripEngineCode = false;
                PlayerSettings.Android.useAPKExpansionFiles = false;
                PlayerSettings.Android.buildApkPerCpuArchitecture = false;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                EditorUserBuildSettings.development = false;
                EditorUserBuildSettings.allowDebugging = false;
                EditorUserBuildSettings.connectProfiler = false;
                EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Disabled;
            }

            public void Dispose()
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, backend);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, stripping);
                PlayerSettings.Android.targetArchitectures = architectures;
                PlayerSettings.stripEngineCode = stripEngine;
                PlayerSettings.Android.useAPKExpansionFiles = expansion;
                PlayerSettings.Android.buildApkPerCpuArchitecture = perCpu;
                PlayerSettings.Android.maxAspectRatio = maxAspectRatio;
                PlayerSettings.Android.resizableWindow = resizableWindow;
                PlayerSettings.Android.renderOutsideSafeArea = outsideSafeArea;
                PlayerSettings.Android.startInFullscreen = fullscreen;
                PlayerSettings.Android.optimizedFramePacing = framePacing;
                EditorUserBuildSettings.buildAppBundle = appBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = exportProject;
                EditorUserBuildSettings.development = development;
                EditorUserBuildSettings.allowDebugging = debugging;
                EditorUserBuildSettings.connectProfiler = profiler;
                EditorUserBuildSettings.androidCreateSymbols = symbols;
                AssetDatabase.SaveAssets();
            }
        }
    }

    public sealed class AndroidMinimalBuildFilter : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => int.MaxValue;
        public void OnPostGenerateGradleAndroidProject(string path) => AndroidMinimalBuild.FilterGradleProject(path);
    }
}
