using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.Player;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    // InputTestFixture swaps real devices for virtual ones, so taps can be simulated without a phone.
    public class UnitCommanderTests : InputTestFixture
    {
        private GameObject root;
        private UnitRegistry registry;
        private int nextUnitId;
        private Camera camera;
        private Unit unit;
        private Unit enemy;
        private UnitCommander commander;
        private Transform marker;
        private SectorLab.Objectives.RelayConfig tapRelayConfig;
        private readonly System.Collections.Generic.List<SectorLab.Objectives.RelayPoint> relays = new System.Collections.Generic.List<SectorLab.Objectives.RelayPoint>();
        private SectorLab.Battle.MatchCommands commands;

        public override void Setup()
        {
            base.Setup();
            registry = new UnitRegistry(); nextUnitId = 0;
            root = new GameObject("TestRoot");

            var cameraObject = new GameObject("TestCamera");
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 15f, 0f), Quaternion.Euler(90f, 0f, 0f));
            camera = cameraObject.AddComponent<Camera>();

            TestArena.CreateGround(root.transform, 10f);
            TestArena.CreateBlock(root.transform, new Vector3(-3f, 0.5f, 0f), Vector3.one);
            TestArena.Bake(root);
            CombatTicker.AddTo(root);

            GameObject unitObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unitObject.transform.SetParent(root.transform);
            unitObject.transform.position = new Vector3(2f, 1f, 2f);
            unitObject.layer = LayerMask.NameToLayer("Units");
            unitObject.AddComponent<UnitMovement>();
            unit = unitObject.AddComponent<Unit>();
            unitObject.AddComponent<UnitAttack>();

            GameObject enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyObject.name = "Enemy";
            enemyObject.SetActive(false);
            enemyObject.transform.SetParent(root.transform);
            enemyObject.transform.position = new Vector3(-2f, 1f, 3f);
            enemyObject.layer = LayerMask.NameToLayer("Units");
            enemyObject.AddComponent<UnitMovement>();
            enemy = enemyObject.AddComponent<Unit>();
            SetField(enemy, "team", Team.Enemy);
            enemyObject.SetActive(true);

            marker = new GameObject("Marker").transform;
            marker.SetParent(root.transform);

            var commanderObject = new GameObject("Commander");
            commanderObject.transform.SetParent(root.transform);
            commanderObject.SetActive(false);
            commander = commanderObject.AddComponent<UnitCommander>();
            SetField(commander, "viewCamera", camera);
            SetField(commander, "groundMask", (LayerMask)LayerMask.GetMask("Ground"));
            SetField(commander, "destinationMarker", marker);
            commanderObject.SetActive(true);
            unit.BindRegistry(registry); enemy.BindRegistry(registry);
            commander.BindUnits(registry);
            unit.AssignId(0);
            enemy.AssignId(3);
            relays.Clear();
            commands = new SectorLab.Battle.MatchCommands(relays, units: registry);
            commander.Bind(commands);

            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            // Immediate: synchronous tests share a frame, and a deferred Destroy would leave
            // this test's colliders in the way of the next test's raycasts.
            Object.DestroyImmediate(root);
            if (tapRelayConfig != null) Object.DestroyImmediate(tapRelayConfig);
            base.TearDown();
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private Vector2 ScreenPoint(Vector3 world)
        {
            return camera.WorldToScreenPoint(world);
        }

        [Test]
        public void SelectedUnitDeathClearsSelectionAndDestinationMarkerImmediately()
        {
            Assert.That(commander.SelectFriendly(unit), Is.True);
            commander.HandleTap(ScreenPoint(new Vector3(2, 0, -2)));
            Assert.That(marker.gameObject.activeSelf, Is.True);
            unit.Health.TakeDamage(unit.Health.Max);
            Assert.That(commander.Selected, Is.Null);
            Assert.That(marker.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void TapOnUnit_SelectsIt()
        {
            commander.HandleTap(ScreenPoint(unit.transform.position));

            Assert.That(commander.Selected, Is.SameAs(unit));
            Assert.That(unit.IsSelected, Is.True);
        }

        [UnityTest]
        public IEnumerator TapOnRelay_MovesSelectedUnitIntoCaptureRadius()
        {
            var node = new GameObject("Relay");
            node.transform.SetParent(root.transform);
            node.transform.position = new Vector3(0, 0, -2);
            var config = ScriptableObject.CreateInstance<SectorLab.Objectives.RelayConfig>();
            var relay = node.AddComponent<SectorLab.Objectives.RelayPoint>();
            relay.Configure(config);
            relay.BindUnits(registry);
            relays.Add(relay);
            var collider = node.AddComponent<SphereCollider>();
            collider.radius = config.radius;
            collider.isTrigger = true;
            Physics.SyncTransforms();
            commander.HandleTap(ScreenPoint(unit.transform.position));
            commander.HandleTap(ScreenPoint(node.transform.position));
            Assert.That(unit.Movement.IsMoving, Is.True);
            Assert.That(marker.gameObject.activeSelf, Is.True);
            float deadline = Time.time + 5;
            while (unit.Movement.IsMoving && Time.time < deadline) yield return null;
            Assert.That(relay.Contains(unit), Is.True);
            relay.Simulate(5);
            Assert.That(relay.State.Owner, Is.EqualTo(SectorLab.Objectives.RelayOwner.Player));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void TapOnRelayBesideAllyOrdersSelectedUnitInsteadOfChangingSelection()
        {
            var relay = CreateTapRelay();
            SetField(enemy, "team", Team.Player);
            enemy.transform.position = relay.transform.position + Vector3.up;
            Physics.SyncTransforms();
            Assert.That(commander.SelectFriendly(unit), Is.True);

            // Next to the ally, on the relay surface rather than its body.
            commander.HandleTap(ScreenPoint(relay.transform.position + Vector3.right));

            Assert.That(commander.Selected, Is.SameAs(unit));
            Assert.That(unit.Movement.IsMoving, Is.True);
            Assert.That(Vector3.Distance(unit.Movement.Destination, relay.transform.position), Is.LessThan(0.2f));
            Assert.That(marker.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void DirectTapOnAllyInsideRelaySelectsItWhileAnotherUnitIsSelected()
        {
            var relay = CreateTapRelay();
            SetField(enemy, "team", Team.Player);
            enemy.transform.position = relay.transform.position + Vector3.up;
            Physics.SyncTransforms();
            Assert.That(commander.SelectFriendly(unit), Is.True);

            commander.HandleTap(ScreenPoint(enemy.transform.position));

            Assert.That(commander.Selected, Is.SameAs(enemy));
            Assert.That(unit.Movement.IsMoving, Is.False);
            Assert.That(enemy.Movement.IsMoving, Is.False);
            Assert.That(marker.gameObject.activeSelf, Is.False);
        }

        private SectorLab.Objectives.RelayPoint CreateTapRelay()
        {
            var node = new GameObject("OccupiedRelay");
            node.transform.SetParent(root.transform);
            node.transform.position = new Vector3(0, 0, -2);
            tapRelayConfig = ScriptableObject.CreateInstance<SectorLab.Objectives.RelayConfig>();
            var relay = node.AddComponent<SectorLab.Objectives.RelayPoint>();
            relay.Configure(tapRelayConfig);
            relays.Add(relay);
            var collider = node.AddComponent<SphereCollider>();
            collider.radius = tapRelayConfig.radius;
            collider.isTrigger = true;
            return relay;
        }

        [Test]
        public void AllyInsideRelayCanBeSelectedWhenNoUnitIsSelected()
        {
            var relay = CreateTapRelay();
            SetField(enemy, "team", Team.Player);
            enemy.transform.position = relay.transform.position + Vector3.up;
            Physics.SyncTransforms();
            commander.HandleTap(ScreenPoint(enemy.transform.position));
            Assert.That(commander.Selected, Is.SameAs(enemy));
            Assert.That(enemy.Movement.IsMoving, Is.False);
        }

        [Test]
        public void DirectEnemyTapInsideRelayStillMeansAttack()
        {
            var relay = CreateTapRelay();
            enemy.transform.position = relay.transform.position + Vector3.up;
            Physics.SyncTransforms();
            Assert.That(commander.SelectFriendly(unit), Is.True);
            commander.HandleTap(ScreenPoint(enemy.transform.position));
            Assert.That(commander.Selected, Is.SameAs(unit));
            Assert.That(unit.Attack.Target, Is.SameAs(enemy));
            Assert.That(marker.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SolidCoverStillBlocksARelayOrder()
        {
            var relay = CreateTapRelay();
            TestArena.CreateBlock(root.transform, relay.transform.position + Vector3.up * 4, new Vector3(3, 1, 3));
            Physics.SyncTransforms();
            Assert.That(commander.SelectFriendly(unit), Is.True);
            commander.HandleTap(ScreenPoint(relay.transform.position));
            Assert.That(commander.Selected, Is.SameAs(unit));
            Assert.That(unit.Movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator TouchOnOccupiedRelayIssuesOrderOnlyOnRelease()
        {
            var relay = CreateTapRelay();
            SetField(enemy, "team", Team.Player);
            enemy.transform.position = relay.transform.position + Vector3.up;
            Physics.SyncTransforms();
            Assert.That(commander.SelectFriendly(unit), Is.True);
            var screen = InputSystem.AddDevice<Touchscreen>();
            Vector2 tap = ScreenPoint(relay.transform.position + Vector3.right);
            BeginTouch(1, tap, queueEventOnly: true, screen: screen);
            yield return null;
            Assert.That(unit.Movement.IsMoving, Is.False);
            EndTouch(1, tap, queueEventOnly: true, screen: screen);
            yield return null;
            Assert.That(commander.Selected, Is.SameAs(unit));
            Assert.That(unit.Movement.IsMoving, Is.True);
            Assert.That(marker.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void UnreachableRelay_PreservesAttackAndRejectsOrder()
        {
            var node = new GameObject("UnreachableRelay");
            node.transform.SetParent(root.transform);
            node.transform.position = new Vector3(100, 0, 0);
            var config = ScriptableObject.CreateInstance<SectorLab.Objectives.RelayConfig>();
            var relay = node.AddComponent<SectorLab.Objectives.RelayPoint>();
            relay.Configure(config);
            relay.BindUnits(registry);
            relays.Add(relay);
            commander.HandleTap(ScreenPoint(unit.transform.position));
            commander.HandleTap(ScreenPoint(enemy.transform.position));
            Assert.That(commander.OrderSelectedToRelay(relay), Is.False);
            Assert.That(unit.Attack.Target, Is.SameAs(enemy));
            Assert.That(marker.gameObject.activeSelf, Is.False);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void RejectedMoveOrder_PreservesAttackAndHidesMarker()
        {
            commander.HandleTap(ScreenPoint(unit.transform.position));
            commander.HandleTap(ScreenPoint(enemy.transform.position));
            Assert.That(unit.Attack.Target, Is.SameAs(enemy));
            unit.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;

            commander.HandleTap(ScreenPoint(new Vector3(-2f, 0f, -2f)));

            Assert.That(unit.Movement.IsMoving, Is.False);
            Assert.That(marker.gameObject.activeSelf, Is.False);
            Assert.That(unit.Attack.Target, Is.SameAs(enemy), "A rejected move order must preserve the previous attack order");
        }

        [Test]
        public void TapOnGround_WithoutSelection_DoesNothing()
        {
            commander.HandleTap(ScreenPoint(new Vector3(-2f, 0f, -2f)));

            Assert.That(commander.Selected, Is.Null);
            Assert.That(unit.Movement.IsMoving, Is.False);
        }

        [Test]
        public void TapOnGround_SendsSelectedUnitAndShowsMarker()
        {
            commander.HandleTap(ScreenPoint(unit.transform.position));
            commander.HandleTap(ScreenPoint(new Vector3(-2f, 0f, -2f)));

            Assert.That(unit.Movement.IsMoving, Is.True);
            Assert.That(unit.Movement.Destination.x, Is.EqualTo(-2f).Within(0.05f));
            Assert.That(unit.Movement.Destination.z, Is.EqualTo(-2f).Within(0.05f));
            Assert.That(marker.gameObject.activeSelf, Is.True);
        }

        // Sets the tolerance so that a tap 'worldDistance' metres from the unit is just inside it.
        private void SetToleranceForWorldDistance(float worldDistance)
        {
            float pixels = Vector2.Distance(ScreenPoint(unit.transform.position), ScreenPoint(unit.transform.position + Vector3.right * worldDistance));
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            SetField(commander, "tapToleranceMm", pixels * 1.1f / dpi * 25.4f);
        }

        [Test]
        public void NearMissTap_SelectsClosestUnit()
        {
            SetToleranceForWorldDistance(1.5f);

            commander.HandleTap(ScreenPoint(unit.transform.position + new Vector3(1.2f, 0f, 0f)));

            Assert.That(commander.Selected, Is.SameAs(unit));
        }

        [Test]
        public void NearMissTap_DoesNotSelectOccludedUnit()
        {
            SetToleranceForWorldDistance(1.5f);
            GameObject cover = TestArena.CreateBlock(root.transform, Vector3.Lerp(camera.transform.position, unit.transform.position, 0.5f), Vector3.one * 0.5f);
            Physics.SyncTransforms();
            Ray ray = camera.ScreenPointToRay(ScreenPoint(unit.transform.position));
            Assert.That(Physics.Raycast(ray, out RaycastHit hit, 100f), Is.True);
            Assert.That(hit.collider.gameObject, Is.SameAs(cover), "The test must actually hide the unit from the perspective camera");

            commander.HandleTap(ScreenPoint(unit.transform.position + Vector3.right * 1.2f));

            Assert.That(commander.Selected, Is.Null);
        }

        [Test]
        public void TapOutsideTolerance_DoesNotSelect()
        {
            SetToleranceForWorldDistance(1.5f);

            commander.HandleTap(ScreenPoint(unit.transform.position + new Vector3(0f, 0f, -3f)));

            Assert.That(commander.Selected, Is.Null);
        }

        [Test]
        public void TapOnEnemy_WithoutSelection_DoesNotSelectIt()
        {
            commander.HandleTap(ScreenPoint(enemy.transform.position));

            Assert.That(commander.Selected, Is.Null);
        }

        [Test]
        public void TapOnEnemy_OrdersSelectedUnitToAttack()
        {
            commander.HandleTap(ScreenPoint(unit.transform.position));
            commander.HandleTap(ScreenPoint(enemy.transform.position));

            Assert.That(commander.Selected, Is.SameAs(unit));
            Assert.That(unit.Attack.Target, Is.SameAs(enemy));
        }

        [Test]
        public void TapOnGround_CancelsAttack()
        {
            commander.HandleTap(ScreenPoint(unit.transform.position));
            commander.HandleTap(ScreenPoint(enemy.transform.position));
            commander.HandleTap(ScreenPoint(new Vector3(2f, 0f, -2f)));

            Assert.That(unit.Attack.Target, Is.Null);
            Assert.That(unit.Movement.IsMoving, Is.True);
        }

        [Test]
        public void TapOnObstacle_IsIgnored()
        {
            commander.HandleTap(ScreenPoint(unit.transform.position));
            commander.HandleTap(ScreenPoint(new Vector3(-3f, 1f, 0f)));

            Assert.That(unit.Movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator Touch_SelectsAndMovesUnit()
        {
            var touchscreen = InputSystem.AddDevice<Touchscreen>();

            BeginTouch(1, ScreenPoint(unit.transform.position), queueEventOnly: true, screen: touchscreen);
            yield return null;
            Assert.That(commander.Selected, Is.Null, "A press may become a drag; select only on release.");
            EndTouch(1, ScreenPoint(unit.transform.position), queueEventOnly: true, screen: touchscreen);
            yield return null;

            Assert.That(commander.Selected, Is.SameAs(unit));

            BeginTouch(2, ScreenPoint(new Vector3(-2f, 0f, -2f)), queueEventOnly: true, screen: touchscreen);
            yield return null;
            Assert.That(unit.Movement.IsMoving, Is.False);
            EndTouch(2, ScreenPoint(new Vector3(-2f, 0f, -2f)), queueEventOnly: true, screen: touchscreen);
            yield return null;
            Assert.That(unit.Movement.IsMoving, Is.True);
        }

        [UnityTest]
        public IEnumerator DragReturningToItsStartDoesNotBecomeATap()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            Vector2 start = ScreenPoint(unit.transform.position);
            int pans = 0;
            commander.PanRequested += (_, _) => pans++;
            BeginTouch(1, start, queueEventOnly: true, screen: screen);
            yield return null;
            MoveTouch(1, start + Vector2.right * 180, queueEventOnly: true, screen: screen);
            yield return null;
            MoveTouch(1, start, queueEventOnly: true, screen: screen);
            yield return null;
            EndTouch(1, start, queueEventOnly: true, screen: screen);
            yield return null;
            Assert.That(pans, Is.GreaterThan(0));
            Assert.That(commander.Selected, Is.Null);
            Assert.That(unit.Movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator PinchAndLiftingSecondFingerDoNotIssueSelectionOrOrders()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            Vector2 start = ScreenPoint(unit.transform.position);
            int zooms = 0;
            commander.ZoomRequested += _ => zooms++;
            BeginTouch(1, start, queueEventOnly: true, screen: screen);
            yield return null;
            BeginTouch(2, start + Vector2.right * 100, queueEventOnly: true, screen: screen);
            yield return null;
            MoveTouch(2, start + Vector2.right * 200, queueEventOnly: true, screen: screen);
            yield return null;
            EndTouch(2, start + Vector2.right * 200, queueEventOnly: true, screen: screen);
            yield return null;
            EndTouch(1, start, queueEventOnly: true, screen: screen);
            yield return null;
            Assert.That(zooms, Is.GreaterThan(0));
            Assert.That(commander.Selected, Is.Null);
            Assert.That(unit.Movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator CancelledTouchDoesNotSelectTheUnit()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            Vector2 position = ScreenPoint(unit.transform.position);
            BeginTouch(1, position, queueEventOnly: true, screen: screen);
            yield return null;
            InputSystem.QueueStateEvent(screen, new UnityEngine.InputSystem.LowLevel.TouchState {
                touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Canceled, position = position
            });
            yield return null;
            Assert.That(commander.Selected, Is.Null);
        }

        [UnityTest]
        public IEnumerator UiCoverBlocksDirectTapAndTouchRelease()
        {
            var events = new GameObject("UiEvents", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            events.transform.SetParent(root.transform);
            var canvasObject = new GameObject("UiCover", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var cover = new GameObject("BlockingPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            cover.transform.SetParent(canvasObject.transform, false);
            var rect = cover.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            UnityEngine.EventSystems.EventSystem.current = events.GetComponent<UnityEngine.EventSystems.EventSystem>();
            yield return null; // Canvas/Graphic registration and layout settle before the UI raycast.
            Canvas.ForceUpdateCanvases();
            Vector2 position = ScreenPoint(unit.transform.position);
            Assert.That(commander.IsOverUi(position), Is.True, $"UI setup: screen={position}, rect={rect.rect}, active={cover.activeInHierarchy}");
            commander.HandleTap(position);
            Assert.That(commander.Selected, Is.Null);
            var screen = InputSystem.AddDevice<Touchscreen>();
            int pans = 0;
            commander.PanRequested += (_, _) => pans++;
            BeginTouch(1, position, queueEventOnly: true, screen: screen);
            yield return null;
            MoveTouch(1, new Vector2(Screen.width + 100, position.y), queueEventOnly: true, screen: screen);
            yield return null;
            EndTouch(1, position, queueEventOnly: true, screen: screen);
            yield return null;
            Assert.That(commander.Selected, Is.Null);
            Assert.That(pans, Is.Zero, "A gesture started over UI cannot pan the world after leaving UI.");
        }

        [UnityTest]
        public IEnumerator DragAndPinchMoveTheSubscribedCameraWithoutIssuingOrders()
        {
            camera.gameObject.SetActive(false);
            var tactical = camera.gameObject.AddComponent<SectorLab.Presentation.TacticalCamera>();
            SetField(tactical, "commander", commander);
            camera.gameObject.SetActive(true);
            var screen = InputSystem.AddDevice<Touchscreen>();
            Vector2 start = new Vector2(Screen.width / 2f, Screen.height / 2f);
            BeginTouch(1, start, queueEventOnly: true, screen: screen);
            yield return null;
            MoveTouch(1, start + Vector2.right * 100, queueEventOnly: true, screen: screen);
            yield return null;
            EndTouch(1, start + Vector2.right * 100, queueEventOnly: true, screen: screen);
            yield return null;
            Assert.That(tactical.Focus.sqrMagnitude, Is.GreaterThan(0));
            float distance = tactical.Distance;
            BeginTouch(2, start, queueEventOnly: true, screen: screen);
            yield return null;
            BeginTouch(3, start + Vector2.right * 100, queueEventOnly: true, screen: screen);
            yield return null;
            MoveTouch(3, start + Vector2.right * 200, queueEventOnly: true, screen: screen);
            yield return null;
            EndTouch(3, start + Vector2.right * 200, queueEventOnly: true, screen: screen);
            yield return null;
            EndTouch(2, start, queueEventOnly: true, screen: screen);
            yield return null;
            Assert.That(tactical.Distance, Is.LessThan(distance));
            Assert.That(commander.Selected, Is.Null);
            Assert.That(unit.Movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator MouseClickSelectsOnlyOnRelease()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, ScreenPoint(unit.transform.position), queueEventOnly: true);
            Press(mouse.leftButton, queueEventOnly: true);
            yield return null;
            Assert.That(commander.Selected, Is.Null);
            Release(mouse.leftButton, queueEventOnly: true);
            yield return null;
            Assert.That(commander.Selected, Is.SameAs(unit));
        }
    }
}
