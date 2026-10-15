using SectorLab.Units;
using SectorLab.Objectives;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

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
        private int unitLayerMask;

        public Unit Selected { get; private set; }
        public event System.Action<Vector2, Vector2> PanRequested;
        public event System.Action<float> ZoomRequested;
        private Vector2 pressPosition, previousPosition;
        private bool tracking, dragging, beganOverUi, suppressUntilReleased;
        private float pinchDistance;
        private bool pinchBeganOverUi;
        private PointerEventData uiPointer;
        private EventSystem uiEvents;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>(8);

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
            unitLayerMask = LayerMask.GetMask("Units");
            SetMarkerVisible(false);
        }

        private void Update()
        {
            ReadGestures();

            // The marker only makes sense while the selected unit is still on its way.
            if (Selected == null || !Selected.Movement.IsMoving)
                SetMarkerVisible(false);
        }

        private void ReadGestures()
        {
            Touchscreen screen = Touchscreen.current;
            int fingers = 0;
            Vector2 first = default, second = default;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    if (fingers == 0) first = touch.position.ReadValue();
                    if (fingers == 1) second = touch.position.ReadValue();
                    fingers++;
                }
            }
            if (fingers >= 2)
            {
                if (!suppressUntilReleased) pinchBeganOverUi = beganOverUi || IsOverUi(first) || IsOverUi(second);
                tracking = false;
                suppressUntilReleased = true;
                float distance = Vector2.Distance(first, second);
                if (!pinchBeganOverUi && pinchDistance > 1 && distance > 1 && !IsOverUi(first) && !IsOverUi(second))
                    ZoomRequested?.Invoke(pinchDistance / distance);
                pinchDistance = distance;
                return;
            }
            if (suppressUntilReleased)
            {
                if (fingers == 0) { suppressUntilReleased = false; pinchDistance = 0; }
                return; // Lifting the second finger must not turn the first finger into a tap.
            }
            Pointer pointer = Pointer.current;
            if (pointer == null) return;
            if (pointer == screen && screen.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                tracking = false;
                return;
            }
            Vector2 position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                tracking = true;
                dragging = false;
                beganOverUi = IsOverUi(position);
                pressPosition = previousPosition = position;
            }
            if (tracking)
            {
                float dpi = Screen.dpi > 0 ? Screen.dpi : FallbackDpi;
                float threshold = 3f / 25.4f * dpi;
                if ((position - pressPosition).sqrMagnitude > threshold * threshold) dragging = true;
                if (dragging && !beganOverUi && !IsOverUi(position) && position != previousPosition)
                    PanRequested?.Invoke(previousPosition, position);
                previousPosition = position;
                if (pointer.press.wasReleasedThisFrame)
                {
                    tracking = false;
                    if (!dragging && !beganOverUi) HandleTap(position);
                }
            }
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0 && !IsOverUi(mouse.position.ReadValue()))
                    ZoomRequested?.Invoke(Mathf.Exp(Mathf.Clamp(-scroll * 0.0015f, -1, 1)));
            }
        }

        private void OnDisable()
        {
            tracking = dragging = beganOverUi = suppressUntilReleased = pinchBeganOverUi = false;
            pinchDistance = 0;
        }

        public bool IsOverUi(Vector2 position)
        {
            EventSystem events = EventSystem.current;
            if (events == null) return false;
            if (uiPointer == null || uiEvents != events)
            {
                uiEvents = events;
                uiPointer = new PointerEventData(events);
            }
            uiPointer.Reset();
            uiPointer.position = position;
            uiHits.Clear();
            events.RaycastAll(uiPointer, uiHits);
            return uiHits.Count > 0;
        }

        public void HandleTap(Vector2 screenPosition)
        {
            if (!isActiveAndEnabled || IsOverUi(screenPosition) || !viewCamera.pixelRect.Contains(screenPosition))
                return;
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxTapDistance, tapMask, QueryTriggerInteraction.Ignore);

            Unit unit = hasHit ? hit.collider.GetComponentInParent<Unit>() : null;
            // A selected unit can join an occupied relay: a tap on the relay surface beside
            // an ally must not lose to the finger tolerance. A directly tapped unit keeps its
            // usual meaning — select an ally, attack an enemy.
            if (Selected != null && unit == null
                && Physics.Raycast(ray, out RaycastHit relayHit, maxTapDistance,
                    tapMask.value & ~unitLayerMask, QueryTriggerInteraction.Collide))
            {
                RelayPoint destination = relayHit.collider.GetComponentInParent<RelayPoint>();
                if (destination != null)
                {
                    OrderSelectedToRelay(destination);
                    return;
                }
            }

            // Small units outside an explicit relay order remain easy to select.
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

            RelayPoint relay = hit.collider.GetComponentInParent<RelayPoint>();
            if (relay != null)
            {
                OrderSelectedToRelay(relay);
                return;
            }

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
                if (sqr <= nearestSqr && IsVisible(candidate, onScreen))
                {
                    nearestSqr = sqr;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        private bool IsVisible(Unit candidate, Vector3 screenPosition)
        {
            // Check the candidate's own screen position, not the nearby tap.
            // The first solid collider must belong to this unit.
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            return Physics.Raycast(ray, out RaycastHit hit, maxTapDistance, tapMask, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Unit>() == candidate;
        }

        public void ClearSelection()
        {
            if (Selected != null)
            {
                Selected.Died -= OnSelectedDied;
                Selected.SetSelected(false);
            }
            Selected = null;
            SetMarkerVisible(false);
        }

        public bool SelectFriendly(Unit unit)
        {
            if (!isActiveAndEnabled || unit == null || !unit.IsAlive || unit.Team != Team.Player) return false;
            Select(unit);
            return true;
        }

        private void OnSelectedDied(Unit unit) => ClearSelection();
        private void OnDestroy() => ClearSelection();

        public bool OrderSelectedToRelay(RelayPoint relay)
        {
            if (!isActiveAndEnabled || Selected == null || !Selected.IsAlive || relay == null)
                return false;
            if (!relay.TryGetApproachPoint(Selected, out Vector3 point))
            {
                SetMarkerVisible(false);
                return false;
            }
            return MoveSelected(point, relay.GetArrivalDistance(point));
        }

        private void Select(Unit unit)
        {
            if (Selected == unit)
                return;

            if (Selected != null)
            {
                Selected.Died -= OnSelectedDied;
                Selected.SetSelected(false);
            }

            Selected = unit;
            Selected.Died += OnSelectedDied;
            Selected.SetSelected(true);
            SetMarkerVisible(false);
        }

        private void AttackWithSelected(Unit enemy)
        {
            Selected.Attack.SetTarget(enemy);
            SetMarkerVisible(false);
        }

        private bool MoveSelected(Vector3 point, float stoppingDistance = 0f)
        {
            bool accepted = Selected.Movement.MoveTo(point, stoppingDistance);
            // Only an accepted move replaces the previous attack order.
            if (accepted && Selected.Attack != null)
                Selected.Attack.ClearTarget();

            if (destinationMarker != null)
            {
                destinationMarker.position = point;
                SetMarkerVisible(accepted);
            }
            return accepted;
        }

        private void SetMarkerVisible(bool visible)
        {
            if (destinationMarker != null && destinationMarker.gameObject.activeSelf != visible)
                destinationMarker.gameObject.SetActive(visible);
        }
    }
}
