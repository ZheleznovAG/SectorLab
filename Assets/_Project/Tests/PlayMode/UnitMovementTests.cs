using System.Collections;
using NUnit.Framework;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class UnitMovementTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1;
            root = new GameObject("TestRoot");
            TestArena.CreateGround(root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        private UnitMovement CreateUnit(Vector3 position)
        {
            var unitObject = new GameObject("TestUnit");
            unitObject.transform.SetParent(root.transform);
            unitObject.transform.position = position;
            return unitObject.AddComponent<UnitMovement>();
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        [UnityTest]
        public IEnumerator MoveTo_GetsCloserEachFrame()
        {
            TestArena.Bake(root);
            UnitMovement movement = CreateUnit(Vector3.zero);
            var destination = new Vector3(5f, 0f, 0f);
            movement.MoveTo(destination);

            yield return null;
            yield return null;
            float afterTwoFrames = FlatDistance(movement.transform.position, destination);
            yield return null;
            yield return null;

            Assert.That(movement.IsMoving, Is.True);
            Assert.That(FlatDistance(movement.transform.position, destination), Is.LessThan(afterTwoFrames));
        }

        [UnityTest]
        public IEnumerator MoveTo_StopsAtDestination()
        {
            TestArena.Bake(root);
            UnitMovement movement = CreateUnit(Vector3.zero);
            var destination = new Vector3(1.5f, 0f, 1.5f);
            movement.MoveTo(destination);

            // Headless runs can render hundreds of frames before an agent has walked.
            float deadline = Time.time + 10f;
            while (Time.time < deadline && movement.IsMoving)
                yield return null;

            Assert.That(movement.IsMoving, Is.False);
            Assert.That(FlatDistance(movement.transform.position, destination), Is.LessThan(0.15f));
        }

        [UnityTest]
        public IEnumerator MoveTo_PlansPathAroundObstacle()
        {
            TestArena.CreateBlock(root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(6f, 1f, 1f));
            TestArena.Bake(root);
            UnitMovement movement = CreateUnit(new Vector3(0f, 0f, -3f));

            movement.MoveTo(new Vector3(0f, 0f, 3f));
            yield return null;

            NavMeshPath path = movement.GetComponent<NavMeshAgent>().path;
            Assert.That(path.corners.Length, Is.GreaterThan(2), "A straight two-point path would go through the wall");
        }

        [UnityTest]
        public IEnumerator DisabledAgent_MoveToClearsMovingState()
        {
            TestArena.Bake(root);
            UnitMovement movement = CreateUnit(Vector3.zero);
            movement.MoveTo(new Vector3(5f, 0f, 0f));
            yield return null;
            movement.GetComponent<NavMeshAgent>().enabled = false;

            bool accepted = movement.MoveTo(Vector3.right);

            Assert.That(accepted, Is.False);
            Assert.That(movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator DisabledAgent_WithoutNewOrderClearsMovingState()
        {
            TestArena.Bake(root);
            UnitMovement movement = CreateUnit(Vector3.zero);
            Assert.That(movement.MoveTo(new Vector3(5f, 0f, 0f)), Is.True);
            yield return null;
            movement.GetComponent<NavMeshAgent>().enabled = false;
            yield return null;

            Assert.That(movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator UnreachableDestination_StopsAtEndOfPartialPath()
        {
            TestArena.CreateBlock(root.transform, new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 20f));
            TestArena.Bake(root);
            var start = new Vector3(-2f, 0f, 0f);
            var destination = new Vector3(2f, 0f, 0f);
            UnitMovement movement = CreateUnit(start);
            NavMeshAgent agent = movement.GetComponent<NavMeshAgent>();
            Assert.That(movement.MoveTo(destination), Is.True, "The request must be accepted before testing a partial path");
            for (int frame = 0; frame < 60 && agent.pathPending; frame++)
                yield return null;

            Assert.That(agent.pathPending, Is.False);
            Assert.That(agent.hasPath, Is.True);
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathPartial));
            Assert.That(agent.remainingDistance, Is.GreaterThan(0.3f), "The partial path must have a segment to walk");
            float deadline = Time.time + 10f;
            while (Time.time < deadline && movement.IsMoving)
                yield return null;

            Assert.That(movement.IsMoving, Is.False);
            Assert.That(FlatDistance(start, movement.transform.position), Is.GreaterThan(0.5f), "An immediate refusal must not pass this test");
            Assert.That(FlatDistance(destination, movement.transform.position), Is.GreaterThan(0.5f));
            Assert.That(movement.transform.position.x, Is.LessThan(0f), "The agent must not cross the disconnected wall");
        }

        [UnityTest]
        public IEnumerator Stop_HaltsMovement()
        {
            TestArena.Bake(root);
            UnitMovement movement = CreateUnit(Vector3.zero);
            movement.MoveTo(new Vector3(5f, 0f, 0f));
            yield return null;

            movement.Stop();
            yield return null;
            Vector3 stoppedAt = movement.transform.position;
            yield return null;
            yield return null;

            Assert.That(movement.IsMoving, Is.False);
            Assert.That(FlatDistance(movement.transform.position, stoppedAt), Is.LessThan(0.05f));
        }
    }
}
