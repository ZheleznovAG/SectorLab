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
        private Camera camera;
        private Unit unit;
        private Unit enemy;
        private UnitCommander commander;
        private Transform marker;

        public override void Setup()
        {
            base.Setup();
            root = new GameObject("TestRoot");

            var cameraObject = new GameObject("TestCamera");
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 15f, 0f), Quaternion.Euler(90f, 0f, 0f));
            camera = cameraObject.AddComponent<Camera>();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.SetParent(root.transform);
            ground.layer = LayerMask.NameToLayer("Ground");

            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Block";
            block.transform.SetParent(root.transform);
            block.transform.position = new Vector3(-3f, 0.5f, 0f);

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

            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            // Immediate: synchronous tests share a frame, and a deferred Destroy would leave
            // this test's colliders in the way of the next test's raycasts.
            Object.DestroyImmediate(root);
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
        public void TapOnUnit_SelectsIt()
        {
            commander.HandleTap(ScreenPoint(unit.transform.position));

            Assert.That(commander.Selected, Is.SameAs(unit));
            Assert.That(unit.IsSelected, Is.True);
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
            EndTouch(1, ScreenPoint(unit.transform.position), queueEventOnly: true, screen: touchscreen);
            yield return null;

            Assert.That(commander.Selected, Is.SameAs(unit));

            BeginTouch(2, ScreenPoint(new Vector3(-2f, 0f, -2f)), queueEventOnly: true, screen: touchscreen);
            yield return null;

            Assert.That(unit.Movement.IsMoving, Is.True);
        }
    }
}
