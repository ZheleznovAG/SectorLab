using UnityEngine;

namespace SectorLab.Presentation
{
    // Screen-space overlay canvas child. Its descendants share one safe rectangle.
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaLayout : MonoBehaviour
    {
        private RectTransform rect;
        private Rect previousArea;
        private Vector2 previousSize;
        public Rect NormalizedArea { get; private set; } = new Rect(0, 0, 1, 1);

        private void Awake() { rect = GetComponent<RectTransform>(); Refresh(); }
        private void Update() => Refresh();

        private void Refresh()
        {
            Vector2 size = new Vector2(Screen.width, Screen.height);
            if (Screen.safeArea != previousArea || size != previousSize) Apply(Screen.safeArea, size);
        }

        public void Apply(Rect area, Vector2 screenSize)
        {
            if (screenSize.x <= 0 || screenSize.y <= 0 || area.width <= 0 || area.height <= 0) return;
            if (rect == null) rect = GetComponent<RectTransform>();
            previousArea = area; previousSize = screenSize;
            Vector2 min = new Vector2(Mathf.Clamp01(area.xMin / screenSize.x), Mathf.Clamp01(area.yMin / screenSize.y));
            Vector2 max = new Vector2(Mathf.Clamp01(area.xMax / screenSize.x), Mathf.Clamp01(area.yMax / screenSize.y));
            NormalizedArea = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log($"SectorLab layout: screen={screenSize}, safe={area}, anchors={NormalizedArea}");
#endif
        }
    }
}
