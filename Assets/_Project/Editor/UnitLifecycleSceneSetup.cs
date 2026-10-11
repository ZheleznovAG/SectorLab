using System;
using SectorLab.Battle;
using SectorLab.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SectorLab.Editor
{
    public static class UnitLifecycleSceneSetup
    {
        [MenuItem("SectorLab/Use async unit lifecycle in Main")]
        public static void ConfigureMain()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/Scenes/Main.unity") throw new InvalidOperationException("Open Main first.");
            var battle = UnityEngine.Object.FindAnyObjectByType<BattleController>();
            var unitsRoot = GameObject.Find("Units");
            var oldUnits = unitsRoot.GetComponentsInChildren<Unit>(true);
            if (oldUnits.Length != 0 && oldUnits.Length != 6) throw new InvalidOperationException("Unexpected Main unit placeholders.");
            foreach (Unit unit in oldUnits)
                if (!PrefabUtility.IsPartOfPrefabInstance(unit)) throw new InvalidOperationException("Main unit placeholder is not a prefab instance.");
            const string rosterPath = "Assets/_Project/Data/UnitRoster.asset";
            var roster = AssetDatabase.LoadAssetAtPath<UnitRoster>(rosterPath);
            if (roster == null)
            {
                roster = ScriptableObject.CreateInstance<UnitRoster>();
                roster.slots = new UnitSpawnSlot[6];
                Vector3[] positions = { new Vector3(-3,0,-3.4f), new Vector3(0,0,-3), new Vector3(3,0,-3.4f) };
                for (int i = 0; i < 6; i++) roster.slots[i] = new UnitSpawnSlot {
                    team = i < 3 ? Team.Player : Team.Enemy, role = (UnitRole)(i % 3),
                    prefabKey = "units/" + (i < 3 ? "" : "Enemy") + ((UnitRole)(i % 3)),
                    position = i < 3 ? positions[i] : -positions[i % 3]
                };
                AssetDatabase.CreateAsset(roster, rosterPath);
            }
            roster.Validate();
            var lifecycle = battle.GetComponent<UnitLifecycle>() ?? battle.gameObject.AddComponent<UnitLifecycle>();
            var lifecycleData = new SerializedObject(lifecycle);
            lifecycleData.FindProperty("unitsRoot").objectReferenceValue = unitsRoot.transform;
            lifecycleData.FindProperty("resultView").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<BattleResultView>();
            lifecycleData.ApplyModifiedPropertiesWithoutUndo();
            var scopeData = new SerializedObject(battle.GetComponent<GameLifetimeScope>());
            scopeData.FindProperty("roster").objectReferenceValue = roster;
            scopeData.FindProperty("unitLifecycle").objectReferenceValue = lifecycle;
            scopeData.ApplyModifiedPropertiesWithoutUndo();
            // Only the six scene placeholders are removed. Their existing role assets/prefabs remain authoritative.
            foreach (Unit unit in oldUnits) UnityEngine.Object.DestroyImmediate(unit.gameObject);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Main now starts all six roles through one scoped async factory; original prefabs and NavMesh preserved.");
        }
    }
}
