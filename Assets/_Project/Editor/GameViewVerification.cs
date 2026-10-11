using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SectorLab.Editor
{
    // Editor-only: capture_game_view resizes PNGs, so choose the actual render size first.
    public static class GameViewVerification
    {
        [MenuItem("SectorLab/Verification/Game View 16x9")]
        public static void Landscape16x9() => SetResolution(1280, 720);
        [MenuItem("SectorLab/Verification/Game View 20x9")]
        public static void Landscape20x9() => SetResolution(1600, 720);
        [MenuItem("SectorLab/Verification/Game View 4x3")]
        public static void Landscape4x3() => SetResolution(1024, 768);

        public static void SetResolution(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Assembly assembly = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            object group = sizesType.GetProperty("currentGroup", flags).GetValue(sizes);
            Type groupType = group.GetType();
            int builtIn = (int)groupType.GetMethod("GetBuiltinCount", flags).Invoke(group, null);
            int custom = (int)groupType.GetMethod("GetCustomCount", flags).Invoke(group, null);
            string label = $"SectorLab verification {width}x{height}";
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            int index = -1;
            for (int i = builtIn; i < builtIn + custom; i++)
            {
                object size = groupType.GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { i });
                if ((string)sizeType.GetProperty("baseText", flags).GetValue(size) == label) { index = i; break; }
            }
            if (index < 0)
            {
                object fixedResolution = Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType", true), "FixedResolution");
                object size = Activator.CreateInstance(sizeType, flags, null, new object[] { fixedResolution, width, height, label }, null);
                groupType.GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
                index = builtIn + custom;
            }
            Type viewType = assembly.GetType("UnityEditor.GameView", true);
            EditorWindow window = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", flags).SetValue(window, index);
            window.Repaint();
            Debug.Log($"Game View fixed render size requested: {width}x{height}. Verify Screen dimensions after its next frame.");
        }
    }
}
