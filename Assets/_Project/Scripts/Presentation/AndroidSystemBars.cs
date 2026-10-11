using UnityEngine;

namespace SectorLab.Presentation
{
    public sealed class AndroidSystemBars : MonoBehaviour
    {
        private void Start() => Hide();
        private void OnApplicationFocus(bool focused) { if (focused) Hide(); }

        private static void Hide()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Unity 6.5+ controls bars with the Insets API, including GameActivity focus changes.
            var insets = UnityEngine.Android.AndroidApplication.currentWindowInsets;
            if (insets == null) return;
            insets.SetSystemBarsBehavior(UnityEngine.Android.AndroidWindowInsets.SystemBarsBehavior.ShowTransientBarsBySwipe);
            insets.Hide(UnityEngine.Android.AndroidWindowInsets.Type.StatusBars | UnityEngine.Android.AndroidWindowInsets.Type.NavigationBars);
#endif
        }
    }
}
