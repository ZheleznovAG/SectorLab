using UnityEngine;

namespace SectorLab.Units
{
    // World-space health bar made of two quads: the fill pivot sits on the bar's left edge and is scaled by the health fraction.
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Transform fillPivot;

        private Unit unit;
        private Camera viewCamera;

        private void Start()
        {
            unit = GetComponentInParent<Unit>();
            viewCamera = Camera.main;
            unit.Health.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (unit != null)
                unit.Health.Changed -= Refresh;
        }

        private void LateUpdate()
        {
            // Face the camera so the bar never shows edge-on.
            if (viewCamera != null)
                transform.rotation = viewCamera.transform.rotation;
        }

        private void Refresh()
        {
            Vector3 scale = fillPivot.localScale;
            scale.x = unit.Health.Fraction;
            fillPivot.localScale = scale;
        }
    }
}
