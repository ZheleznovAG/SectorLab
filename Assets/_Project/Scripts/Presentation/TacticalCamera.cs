using SectorLab.Player;
using UnityEngine;

namespace SectorLab.Presentation
{
    [RequireComponent(typeof(Camera))]
    public sealed class TacticalCamera : MonoBehaviour
    {
        [SerializeField] private UnitCommander commander;
        [SerializeField] private Vector2 focusBounds = new Vector2(2.5f, 2.5f);
        [SerializeField] private float minimumDistance = 9;
        [SerializeField] private float maximumDistance = 22;
        [SerializeField] private float initialDistance = 18;
        [SerializeField] private float pitch = 55;
        private Camera view;
        private readonly Plane ground = new Plane(Vector3.up, Vector3.zero);
        public Vector3 Focus { get; private set; }
        public float Distance { get; private set; }

        private void Awake()
        {
            view = GetComponent<Camera>();
            Distance = Mathf.Clamp(initialDistance, minimumDistance, maximumDistance);
            Apply();
        }

        private void OnEnable()
        {
            if (commander == null) return;
            commander.PanRequested += Pan;
            commander.ZoomRequested += Zoom;
        }

        private void Start()
        {
            if (commander == null) throw new System.InvalidOperationException("Tactical camera requires Main's UnitCommander.");
        }

        private void OnDisable()
        {
            if (commander == null) return;
            commander.PanRequested -= Pan;
            commander.ZoomRequested -= Zoom;
        }

        public void Pan(Vector2 previous, Vector2 current)
        {
            if (!TryGroundPoint(previous, out Vector3 from) || !TryGroundPoint(current, out Vector3 to)) return;
            Vector3 focus = Focus + from - to;
            Focus = new Vector3(Mathf.Clamp(focus.x, -focusBounds.x, focusBounds.x), 0,
                Mathf.Clamp(focus.z, -focusBounds.y, focusBounds.y));
            Apply();
        }

        public void Zoom(float factor)
        {
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(factor));
            Distance = Mathf.Clamp(Distance * factor, minimumDistance, maximumDistance);
            Apply();
        }

        private bool TryGroundPoint(Vector2 position, out Vector3 point)
        {
            Ray ray = view.ScreenPointToRay(position);
            if (ground.Raycast(ray, out float distance)) { point = ray.GetPoint(distance); return true; }
            point = default;
            return false;
        }

        private void Apply()
        {
            Quaternion rotation = Quaternion.Euler(pitch, 0, 0);
            transform.SetPositionAndRotation(Focus + rotation * Vector3.back * Distance, rotation);
        }
    }
}
