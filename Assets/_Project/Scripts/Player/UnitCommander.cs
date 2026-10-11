using SectorLab.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SectorLab.Player
{
    // Turns taps and clicks into orders: tap a unit to select it, tap the ground to send the selected unit there.
    // Pointer covers both mouse and touchscreen, so the same code works in the editor and on a phone.
    public class UnitCommander : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("Everything a tap can hit, including obstacles, so a tap on a block doesn't fall through to the ground behind it.")]
        [SerializeField] private LayerMask tapMask = ~0;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private Transform destinationMarker;
        [SerializeField, Min(0f)] private float maxTapDistance = 100f;

        public Unit Selected { get; private set; }

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
            SetMarkerVisible(false);
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
                HandleTap(pointer.position.ReadValue());

            // The marker only makes sense while the selected unit is still on its way.
            if (Selected == null || !Selected.Movement.IsMoving)
                SetMarkerVisible(false);
        }

        public void HandleTap(Vector2 screenPosition)
        {
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxTapDistance, tapMask))
                return;

            Unit unit = hit.collider.GetComponentInParent<Unit>();
            if (unit != null)
            {
                Select(unit);
                return;
            }

            bool hitGround = (groundMask.value & (1 << hit.collider.gameObject.layer)) != 0;
            if (hitGround && Selected != null)
                MoveSelected(hit.point);
        }

        private void Select(Unit unit)
        {
            if (Selected == unit)
                return;

            if (Selected != null)
                Selected.SetSelected(false);

            Selected = unit;
            Selected.SetSelected(true);
            SetMarkerVisible(false);
        }

        private void MoveSelected(Vector3 point)
        {
            Selected.Movement.MoveTo(point);

            if (destinationMarker != null)
            {
                destinationMarker.position = point;
                SetMarkerVisible(true);
            }
        }

        private void SetMarkerVisible(bool visible)
        {
            if (destinationMarker != null && destinationMarker.gameObject.activeSelf != visible)
                destinationMarker.gameObject.SetActive(visible);
        }
    }
}
