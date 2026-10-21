using SectorLab.Battle;
using SectorLab.Objectives;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SectorLab.Editor
{
    // One-time, idempotent migration of the existing Main scene, never called by the player.
    public static class RelaySceneSetup
    {
        [MenuItem("SectorLab/Attach match scope to Main")]
        public static void AttachMatchScope()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
            var battle = Object.FindAnyObjectByType<BattleController>();
            if (battle == null || battle.gameObject.scene.path != "Assets/_Project/Scenes/Main.unity")
                throw new System.InvalidOperationException("Open Main first.");
            var scope = battle.GetComponent<GameLifetimeScope>() ?? battle.gameObject.AddComponent<GameLifetimeScope>();
            var serialized = new SerializedObject(scope);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("enemyTactics").objectReferenceValue = battle.GetComponent<SectorLab.AI.EnemyTacticsController>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(battle.gameObject.scene);
            EditorSceneManager.SaveScene(battle.gameObject.scene);
        }

        [MenuItem("SectorLab/Attach enemy tactics to Main")]
        public static void AttachEnemyAI()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
            var battle = Object.FindAnyObjectByType<BattleController>();
            if (battle == null || battle.gameObject.scene.path != "Assets/_Project/Scenes/Main.unity")
                throw new System.InvalidOperationException("Open Main first.");
            var ai = battle.GetComponent<SectorLab.AI.EnemyTacticsController>() ?? battle.gameObject.AddComponent<SectorLab.AI.EnemyTacticsController>();
            EditorSceneManager.MarkSceneDirty(battle.gameObject.scene);
            EditorSceneManager.SaveScene(battle.gameObject.scene);
        }

        [MenuItem("SectorLab/Setup relay points in Main")]
        public static void ConfigureMain()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before editing Main.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/_Project/Scenes/Main.unity") throw new System.InvalidOperationException("Open Main first.");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Relays"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Relays");
            var parent = GameObject.Find("Relays") ?? new GameObject("Relays");
            string[] labels = { "A", "B", "C" };
            Vector3[] positions = { new Vector3(-2.5f, 0, -1.8f), new Vector3(2.5f, 0, 2.2f), Vector3.zero };
            var points = new RelayPoint[3];
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_Ground.mat");
            for (int i = 0; i < labels.Length; i++)
            {
                string path = $"Assets/_Project/Data/Relays/{labels[i]}.asset";
                var config = AssetDatabase.LoadAssetAtPath<RelayConfig>(path);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<RelayConfig>();
                    config.label = labels[i];
                    config.kind = i < 2 ? RelayKind.Score : RelayKind.Repair;
                    AssetDatabase.CreateAsset(config, path);
                }
                Transform existing = parent.transform.Find(labels[i]);
                if (existing != null)
                {
                    points[i] = existing.GetComponent<RelayPoint>();
                    var existingLabel = existing.Find("Status").GetComponent<TextMesh>();
                    existingLabel.characterSize = 0.07f;
                    existingLabel.transform.localPosition = new Vector3(0, 0.25f, -0.95f);
                    EditorUtility.SetDirty(existingLabel);
                    continue;
                }
                var node = new GameObject(labels[i]);
                node.transform.SetParent(parent.transform);
                node.transform.position = positions[i];
                var area = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                area.name = "CaptureArea";
                area.transform.SetParent(node.transform, false);
                area.transform.localPosition = Vector3.up * 0.04f;
                area.transform.localScale = new Vector3(config.radius * 2, 0.025f, config.radius * 2);
                Object.DestroyImmediate(area.GetComponent<Collider>());
                var renderer = area.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                area.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                var collider = node.AddComponent<SphereCollider>();
                collider.radius = config.radius;
                collider.isTrigger = true;
                var textObject = new GameObject("Status");
                textObject.transform.SetParent(node.transform, false);
                textObject.transform.localPosition = new Vector3(0, 0.25f, -0.95f);
                var label = textObject.AddComponent<TextMesh>();
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.fontSize = 48;
                label.characterSize = 0.07f;
                label.text = config.label;
                label.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                points[i] = node.AddComponent<RelayPoint>();
                points[i].Configure(config, renderer, label);
            }
            var battle = Object.FindAnyObjectByType<BattleController>();
            var serialized = new SerializedObject(battle);
            var relays = serialized.FindProperty("relays");
            relays.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) relays.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("W2: A/B/C added to Main; original arena, prefabs and NavMesh retained.");
        }
    }
}
