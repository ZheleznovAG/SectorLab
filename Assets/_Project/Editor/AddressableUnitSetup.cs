using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;

namespace SectorLab.Editor
{
    public static class AddressableUnitSetup
    {
        [MenuItem("SectorLab/Configure local unit Addressables")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before configuring content.");
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildRemoteCatalog = false;
            settings.DisableCatalogUpdateOnStartup = true;
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            var group = settings.FindGroup("Local Units") ?? settings.CreateGroup("Local Units", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            string[] roles = { "Tank", "Scout", "Ranger", "EnemyTank", "EnemyScout", "EnemyRanger" };
            foreach (string role in roles)
            {
                string path = "Assets/_Project/Prefabs/" + role + ".prefab";
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Missing prefab: " + path);
                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = "units/" + role;
                entry.SetLabel("unit", true, true);
            }
            EditorUtility.SetDirty(bundle);
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Configured six existing variants in one local unit bundle; no remote catalog.");
        }

        [MenuItem("SectorLab/Build local unit content")]
        public static void BuildContent()
            => BuildContent("manual-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));

        public static void BuildContent(string task)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(task, "^[A-Za-z0-9._-]+$"))
                throw new ArgumentException("Invalid evidence task name.", nameof(task));
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building content.");
            string reportPath = Path.Combine(Application.dataPath, "../docs/evidence/" + task + "-local-content.json");
            if (File.Exists(reportPath)) throw new InvalidOperationException("Content evidence already exists; use a new task name.");
            // Public, project-local preference: create the diagnostic report instead of a first-build modal.
            ProjectConfigData.GenerateBuildLayout = true;
            AddressablesRuntimeProperties.ClearCachedPropertyValues();
            AddressableAssetSettings.BuildPlayerContent(out var result);
            var report = new ContentBuildRecord {
                checkedAtUtc = DateTime.UtcNow.ToString("o"), target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                error = result.Error, outputPath = result.OutputPath, durationSeconds = result.Duration,
                succeeded = string.IsNullOrEmpty(result.Error)
            };
            if (report.succeeded)
            {
                string root = Path.GetFullPath(Addressables.BuildPath);
                var bundles = result.FileRegistry.GetFilePaths()
                    .Where(path => path.EndsWith(".bundle", StringComparison.Ordinal)).ToArray();
                if (bundles.Length == 0) throw new InvalidOperationException("Local unit content produced no AssetBundles.");
                report.files = bundles.Concat(new[] { "catalog.bin", "catalog.hash", "settings.json" }
                        .Select(name => Path.Combine(root, name)))
                    .Distinct().OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => Fingerprint(root, path)).ToArray();
                if (report.files.Any(file => file.path.EndsWith(".bundle", StringComparison.Ordinal)
                    && !file.path.StartsWith("aa/" + report.target + "/", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Content bundle path does not match the active build target.");
            }
            using (var stream = new StreamWriter(new FileStream(reportPath, FileMode.CreateNew), new UTF8Encoding(false)))
                stream.Write(JsonUtility.ToJson(report, true));
            if (!report.succeeded) throw new InvalidOperationException("Addressables content build failed: " + result.Error);
            Debug.Log("Local unit content built: " + result.OutputPath);
        }

        private static ContentFile Fingerprint(string root, string path)
        {
            string absolute = Path.GetFullPath(path);
            string relative = Path.GetRelativePath(root, absolute).Replace('\\', '/');
            if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                throw new InvalidOperationException("A bundle is outside the local Addressables build directory.");
            using var hash = SHA256.Create();
            using var stream = File.OpenRead(absolute);
            return new ContentFile {
                path = "aa/" + relative, bytes = stream.Length,
                sha256 = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "")
            };
        }

        [Serializable]
        private sealed class ContentFile
        {
            public string path, sha256;
            public long bytes;
        }

        [Serializable]
        private sealed class ContentBuildRecord
        {
            public string checkedAtUtc;
            public string target;
            public string error;
            public string outputPath;
            public double durationSeconds;
            public bool succeeded;
            public ContentFile[] files;
        }
    }
}
