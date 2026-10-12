using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SectorLab.Editor
{
    public static class ArenaBalanceSetup
    {
        [MenuItem("SectorLab/Balance symmetric Main arena")]
        public static void ConfigureMain()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/_Project/Scenes/Main.unity")
                throw new System.InvalidOperationException("Open Main outside Play Mode.");
            var battle = Object.FindFirstObjectByType<BattleController>();
            battle.Relays[0].transform.position = new Vector3(-2.5f, 0, -2);
            battle.Relays[1].transform.position = new Vector3(2.5f, 0, 2);
            battle.Relays[2].transform.position = Vector3.zero;
            var first = GameObject.Find("Block1");
            var second = GameObject.Find("Block2");
            if (first == null || second == null) throw new System.InvalidOperationException("Original side obstacles are missing.");
            second.transform.position = new Vector3(-first.transform.position.x, first.transform.position.y, -first.transform.position.z);
            second.transform.rotation = first.transform.rotation;
            second.transform.localScale = first.transform.localScale;
            var third = GameObject.Find("Block3");
            if (third == null) throw new System.InvalidOperationException("Original Block3 is missing.");
            third.transform.position = new Vector3(0, 0.5f, 2.7f);
            var fourth = GameObject.Find("Block4");
            if (fourth == null) { fourth = Object.Instantiate(third, third.transform.parent); fourth.name = "Block4"; }
            fourth.transform.position = new Vector3(0, 0.5f, -2.7f);
            var roster = AssetDatabase.LoadAssetAtPath<UnitRoster>("Assets/_Project/Data/UnitRoster.asset");
            Vector3[] bases = { new Vector3(-3, 0, -3.8f), new Vector3(0, 0, -3.8f), new Vector3(3, 0, -3.8f) };
            for (int i = 0; i < roster.slots.Length; i++)
            {
                var slot = roster.slots[i];
                slot.position = bases[(int)slot.role] * (slot.team == Team.Player ? 1 : -1);
            }
            EditorUtility.SetDirty(roster);
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            string path = AssetDatabase.GetAssetPath(surface.navMeshData);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(path)) throw new System.InvalidOperationException("Original baked NavMesh asset is missing.");
            surface.BuildNavMesh();
            surface.RemoveData();
            AssetDatabase.CreateAsset(surface.navMeshData, path); // Replace content at the existing path; preserve its .meta.
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(path);
            surface.AddData();
            if (AssetDatabase.AssetPathToGUID(path) != guid) throw new System.InvalidOperationException("NavMesh GUID changed.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Main arena now has mirrored relays, bases and four obstacles; original NavMesh GUID retained.");
        }
    }
}
