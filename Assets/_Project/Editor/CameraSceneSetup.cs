using SectorLab.Player;
using SectorLab.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SectorLab.Editor
{
    public static class CameraSceneSetup
    {
        [MenuItem("SectorLab/Configure Main tactical camera")]
        public static void ConfigureMain()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/_Project/Scenes/Main.unity")
                throw new System.InvalidOperationException("Open Main outside Play Mode.");
            var commander = Object.FindFirstObjectByType<UnitCommander>();
            var camera = Camera.main;
            if (camera == null || commander == null) throw new System.InvalidOperationException("Main camera/commander not found.");
            var tactical = camera.GetComponent<TacticalCamera>() ?? camera.gameObject.AddComponent<TacticalCamera>();
            var data = new SerializedObject(tactical);
            data.FindProperty("commander").objectReferenceValue = commander;
            data.ApplyModifiedPropertiesWithoutUndo();
            var scene = camera.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Main camera consumes commander pan/pinch/wheel with bounded focus and distance.");
        }
    }
}
