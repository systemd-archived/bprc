using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BuildTools
{
    /// <summary>
    /// Release Android build profile for current devices. The legacy AndroidMinimalBuild profile stays
    /// available for script-engine compatibility and ARMv7-only recovery builds.
    /// </summary>
    public static class AndroidOptimizedBuild
    {
        private const string LogPrefix = "ANDROID_OPTIMIZED_BUILD";
        private const string DefaultName = "BPLE-Android";

        [MenuItem("Tools/Build/Build Optimized Android APK")]
        public static void BuildInteractive()
        {
            try
            {
                string output = Path.Combine(GetProjectRoot(), "Builds", "AndroidOptimized",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"), DefaultName + ".apk");
                Build(output);
                EditorUtility.RevealInFinder(output);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "_FAILED " + exception);
                EditorUtility.DisplayDialog("Optimized Android build failed", exception.Message, "OK");
            }
        }

        public static void BuildFromCommandLine()
        {
            int exitCode = 1;
            try
            {
                string directory = GetArgument("-oneClickOutput",
                    Path.Combine(GetProjectRoot(), "Builds", "AndroidOptimized"));
                string name = GetArgument("-oneClickName", DefaultName);
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

            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            AssetDatabase.SaveAssets();
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
                Debug.Log(string.Format("{0}_SUCCEEDED path={1} size={2:F1}MB time={3:F1}s backend=IL2CPP architectures=ARM64|ARMv7",
                    LogPrefix, outputPath, report.summary.totalSize / 1048576f, report.summary.totalTime.TotalSeconds));
            }
        }

        private static string GetProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string GetArgument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return fallback;
        }

        public sealed class SettingsScope : IDisposable
        {
            private readonly ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            private readonly ManagedStrippingLevel stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android);
            private readonly AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;
            private readonly AndroidSdkVersions minSdk = PlayerSettings.Android.minSdkVersion;
            private readonly bool stripEngine = PlayerSettings.stripEngineCode;
            private readonly bool expansion = PlayerSettings.Android.useAPKExpansionFiles;
            private readonly bool perCpu = PlayerSettings.Android.buildApkPerCpuArchitecture;
            private readonly bool appBundle = EditorUserBuildSettings.buildAppBundle;
            private readonly bool exportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            private readonly bool development = EditorUserBuildSettings.development;
            private readonly bool debugging = EditorUserBuildSettings.allowDebugging;
            private readonly bool profiler = EditorUserBuildSettings.connectProfiler;
            private readonly AndroidCreateSymbols symbols = EditorUserBuildSettings.androidCreateSymbols;

            public void Apply()
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                // ARMv7 remains enabled for older Android hardware; ARM64 is the primary modern ABI.
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
                PlayerSettings.stripEngineCode = true;
                PlayerSettings.Android.useAPKExpansionFiles = false;
                PlayerSettings.Android.buildApkPerCpuArchitecture = false;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                EditorUserBuildSettings.development = false;
                EditorUserBuildSettings.allowDebugging = false;
                EditorUserBuildSettings.connectProfiler = false;
                EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
            }

            public void Dispose()
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, backend);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, stripping);
                PlayerSettings.Android.targetArchitectures = architectures;
                PlayerSettings.Android.minSdkVersion = minSdk;
                PlayerSettings.stripEngineCode = stripEngine;
                PlayerSettings.Android.useAPKExpansionFiles = expansion;
                PlayerSettings.Android.buildApkPerCpuArchitecture = perCpu;
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
}
