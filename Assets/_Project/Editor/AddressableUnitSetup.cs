using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

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
            AddressableAssetSettings.BuildPlayerContent(out var result);
            string reportPath = Path.Combine(Application.dataPath, "../docs/agent_execution/evidence/" + task + "-local-content.json");
            var report = new ContentBuildRecord {
                checkedAtUtc = DateTime.UtcNow.ToString("o"), target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                error = result.Error, outputPath = result.OutputPath, durationSeconds = result.Duration,
                succeeded = string.IsNullOrEmpty(result.Error)
            };
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            if (!report.succeeded) throw new InvalidOperationException("Addressables content build failed: " + result.Error);
            Debug.Log("Local unit content built: " + result.OutputPath);
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
        }
    }
}
