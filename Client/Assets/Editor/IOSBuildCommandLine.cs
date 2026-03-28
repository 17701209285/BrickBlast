using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BrickBlast.Editor
{
    public static class IOSBuildCommandLine
    {
        public static void BuildFromCommandLine()
        {
            string outputPath = GetRequiredArgument("--ios-output-path");
            string bundleIdentifier = GetOptionalArgument("--ios-bundle-id");
            string bundleVersion = GetOptionalArgument("--ios-bundle-version");
            string buildNumber = GetOptionalArgument("--ios-build-number");
            bool developmentBuild = HasFlag("--ios-development");
            bool allowDebugging = HasFlag("--ios-allow-debugging");
            bool cleanOutput = HasFlag("--ios-clean-output");

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");
            }

            string fullOutputPath = Path.GetFullPath(outputPath);
            if (cleanOutput && Directory.Exists(fullOutputPath))
            {
                Directory.Delete(fullOutputPath, true);
            }

            Directory.CreateDirectory(fullOutputPath);

            string originalBundleIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
            string originalBundleVersion = PlayerSettings.bundleVersion;
            string originalBuildNumber = PlayerSettings.iOS.buildNumber;

            try
            {
                if (!string.IsNullOrWhiteSpace(bundleIdentifier))
                {
                    PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, bundleIdentifier);
                }

                if (!string.IsNullOrWhiteSpace(bundleVersion))
                {
                    PlayerSettings.bundleVersion = bundleVersion;
                }

                if (!string.IsNullOrWhiteSpace(buildNumber))
                {
                    PlayerSettings.iOS.buildNumber = buildNumber;
                }

                if (EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS) == false)
                {
                    throw new InvalidOperationException("Failed to switch active build target to iOS.");
                }

                BuildOptions buildOptions = BuildOptions.None;
                if (developmentBuild)
                {
                    buildOptions |= BuildOptions.Development;
                }

                if (allowDebugging)
                {
                    buildOptions |= BuildOptions.AllowDebugging;
                }

                BuildPlayerOptions playerOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = fullOutputPath,
                    targetGroup = BuildTargetGroup.iOS,
                    target = BuildTarget.iOS,
                    options = buildOptions
                };

                Debug.LogFormat(
                    "[IOSBuild] Export Xcode project. Output={0} BundleId={1} Version={2} Build={3} Development={4} AllowDebugging={5}",
                    fullOutputPath,
                    PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS),
                    PlayerSettings.bundleVersion,
                    PlayerSettings.iOS.buildNumber,
                    developmentBuild,
                    allowDebugging);

                BuildReport report = BuildPipeline.BuildPlayer(playerOptions);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Unity iOS export failed. Result={0} Errors={1} Warnings={2}",
                            report.summary.result,
                            report.summary.totalErrors,
                            report.summary.totalWarnings));
                }

                Debug.LogFormat("[IOSBuild] Xcode project exported to: {0}", fullOutputPath);
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, originalBundleIdentifier);
                PlayerSettings.bundleVersion = originalBundleVersion;
                PlayerSettings.iOS.buildNumber = originalBuildNumber;
                AssetDatabase.SaveAssets();
            }
        }

        private static string GetRequiredArgument(string name)
        {
            string value = GetOptionalArgument(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Missing required command line argument: " + name);
            }

            return value;
        }

        private static string GetOptionalArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private static bool HasFlag(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
