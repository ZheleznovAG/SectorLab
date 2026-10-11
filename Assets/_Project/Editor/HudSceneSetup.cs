using SectorLab.Battle;
using SectorLab.Player;
using SectorLab.Presentation;
using SectorLab.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SectorLab.Editor
{
    public static class HudSceneSetup
    {
        private static Font font;
        private static readonly Color Ink = new Color(0.055f, 0.075f, 0.105f, 0.98f);
        [MenuItem("SectorLab/Configure Main match HUD")]
        public static void ConfigureMain()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/_Project/Scenes/Main.unity")
                throw new System.InvalidOperationException("Open Main outside Play Mode.");
            var battle = Object.FindFirstObjectByType<BattleController>();
            var result = Object.FindFirstObjectByType<BattleResultView>();
            var canvas = result.GetComponentInParent<Canvas>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var oldCounter = Object.FindFirstObjectByType<ForceCounterView>();
            if (oldCounter != null) oldCounter.gameObject.SetActive(false);
            var existing = canvas.transform.Find("MatchHUD");
            if (existing != null) Object.DestroyImmediate(existing.gameObject); // Only this generated UI subtree.
            var root = Rect("MatchHUD", canvas.transform, Vector2.zero, Vector2.one);
            var hud = root.gameObject.AddComponent<BattleHud>();
            var top = Panel("TopBar", root, new Vector2(0, 1), Vector2.one, 140, true);
            Label("Brand", top, new Vector2(0.015f, 0.52f), new Vector2(0.25f, 0.97f), "SECTORLAB / RELAY SKIRMISH", 24, TextAnchor.MiddleLeft);
            var blue = Label("PlayerScore", top, new Vector2(0.25f, 0.3f), new Vector2(0.47f, 0.95f), "СИНИЕ  0", 48, TextAnchor.MiddleRight);
            blue.color = new Color(0.32f, 0.68f, 1);
            var red = Label("EnemyScore", top, new Vector2(0.51f, 0.3f), new Vector2(0.74f, 0.95f), "0  КРАСНЫЕ", 48, TextAnchor.MiddleLeft);
            red.color = new Color(1, 0.46f, 0.40f);
            var time = Label("Timer", top, new Vector2(0.34f, 0), new Vector2(0.66f, 0.35f), "03:00 • до 45 очков", 26, TextAnchor.MiddleCenter);
            var pause = Button("Pause", top, new Vector2(0.80f, 0.22f), new Vector2(0.985f, 0.85f), "Пауза", 30);
            var objectives = Panel("Relays", root, new Vector2(0, 1), Vector2.one, 85, true);
            objectives.anchoredPosition = new Vector2(0, -140);
            var relays = new Button[3]; var relayLabels = new Text[3];
            var units = new Button[3]; var unitLabels = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                relays[i] = Button("Relay" + i, objectives, new Vector2(i / 3f + 0.01f, 0.07f), new Vector2((i + 1) / 3f - 0.01f, 0.93f), "Узел", 27);
                relayLabels[i] = relays[i].GetComponentInChildren<Text>();
            }
            var bottom = Panel("Squad", root, Vector2.zero, new Vector2(1, 0), 175, false);
            var selected = Label("Selection", bottom, new Vector2(0.02f, 0.67f), new Vector2(0.98f, 0.98f), "", 25, TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
            {
                units[i] = Button("Role" + i, bottom, new Vector2(i / 3f + 0.01f, 0.05f), new Vector2((i + 1) / 3f - 0.01f, 0.65f), ((UnitRole)i).ToString(), 32);
                unitLabels[i] = units[i].GetComponentInChildren<Text>();
            }
            var data = new SerializedObject(hud);
            Set(data, "battle", battle); Set(data, "lifecycle", battle.GetComponent<UnitLifecycle>());
            Set(data, "commander", Object.FindFirstObjectByType<UnitCommander>());
            Set(data, "worldCamera", Camera.main);
            Set(data, "playerScore", blue); Set(data, "enemyScore", red); Set(data, "timer", time); Set(data, "selection", selected);
            Set(data, "pauseButton", pause); Set(data, "pauseLabel", pause.GetComponentInChildren<Text>());
            SetArray(data, "relayButtons", relays); SetArray(data, "relayLabels", relayLabels);
            SetArray(data, "unitButtons", units); SetArray(data, "unitLabels", unitLabels);
            data.ApplyModifiedPropertiesWithoutUndo();
            var resultData = new SerializedObject(result);
            var panel = (GameObject)resultData.FindProperty("panel").objectReferenceValue;
            panel.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 0.97f);
            var title = (Text)resultData.FindProperty("title").objectReferenceValue;
            title.fontSize = 64;
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 28; title.resizeTextMaxSize = 64;
            Fit(title.rectTransform, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.85f));
            var restart = (Button)resultData.FindProperty("restartButton").objectReferenceValue;
            Fit(restart.GetComponent<RectTransform>(), new Vector2(0.25f, 0.30f), new Vector2(0.75f, 0.40f));
            restart.GetComponentInChildren<Text>().fontSize = 36;
            panel.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Main HUD now displays match scores/time, objective state, role HP/respawn and selection.");
        }

        private static void Set(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;
        private static void SetArray(SerializedObject data, string field, Object[] values)
        {
            var array = data.FindProperty(field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>(); Fit(rect, min, max); return rect;
        }
        private static void Fit(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, float height, bool top)
        {
            var rect = Rect(name, parent, min, max);
            rect.pivot = new Vector2(0.5f, top ? 1 : 0); rect.sizeDelta = new Vector2(0, height);
            rect.gameObject.AddComponent<Image>().color = Ink; return rect;
        }
        private static Text Label(string name, Transform parent, Vector2 min, Vector2 max, string caption, int size, TextAnchor alignment)
        {
            var rect = Rect(name, parent, min, max); var label = rect.gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.text = caption; label.color = Color.white;
            label.alignment = alignment; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 18; label.resizeTextMaxSize = size;
            return label;
        }
        private static Button Button(string name, Transform parent, Vector2 min, Vector2 max, string caption, int size)
        {
            var rect = Rect(name, parent, min, max); var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.20f, 0.26f); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label("Label", rect, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f), caption, size, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
