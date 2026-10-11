using SectorLab.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SectorLab.Player
{
    // Turns taps and clicks into orders: tap your unit to select it, an enemy to attack it, the ground to move there.
    // Pointer covers both mouse and touchscreen, so the same code works in the editor and on a phone.
    public class UnitCommander : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("Everything a tap can hit, including obstacles, so a tap on a block doesn't fall through to the ground behind it.")]
        [SerializeField] private LayerMask tapMask = ~0;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private Transform destinationMarker;
        [SerializeField, Min(0f)] private float maxTapDistance = 100f;
        [Tooltip("A tap this close (on screen) to a unit counts as a tap on it. A fingertip covers roughly 7 mm.")]
        [SerializeField, Min(0f)] private float tapToleranceMm = 7f;

        // The editor and some devices report dpi 0; 160 is Android's baseline density.
        private const float FallbackDpi = 160f;

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
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxTapDistance, tapMask);

            // Small units are hard to hit with a finger, so a near miss still counts.
            Unit unit = hasHit ? hit.collider.GetComponentInParent<Unit>() : null;
            if (unit == null)
                unit = FindUnitNear(screenPosition);

            if (unit != null)
            {
                if (unit.Team == Team.Player)
                    Select(unit);
                else if (Selected != null && Selected.Attack != null)
                    AttackWithSelected(unit);
                return;
            }

            if (!hasHit)
                return;

            bool hitGround = (groundMask.value & (1 << hit.collider.gameObject.layer)) != 0;
            if (hitGround && Selected != null)
                MoveSelected(hit.point);
        }

        private Unit FindUnitNear(Vector2 screenPosition)
        {
            float dpi = Screen.dpi > 0f ? Screen.dpi : FallbackDpi;
            float tolerancePixels = tapToleranceMm / 25.4f * dpi;

            Unit nearest = null;
            float nearestSqr = tolerancePixels * tolerancePixels;
            // Index loop: foreach over an IReadOnlyList boxes its enumerator and allocates every frame.
            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit candidate = Unit.All[i];
                if (!candidate.IsAlive)
                    continue;

                Vector3 onScreen = viewCamera.WorldToScreenPoint(candidate.transform.position);
                if (onScreen.z <= 0f)
                    continue;

                float sqr = ((Vector2)onScreen - screenPosition).sqrMagnitude;
                if (sqr <= nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = candidate;
                }
            }
            return nearest;
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

        private void AttackWithSelected(Unit enemy)
        {
            Selected.Attack.SetTarget(enemy);
            SetMarkerVisible(false);
        }

        private void MoveSelected(Vector3 point)
        {
            // A move order cancels the attack, otherwise the unit would keep chasing its target.
            if (Selected.Attack != null)
                Selected.Attack.ClearTarget();
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
