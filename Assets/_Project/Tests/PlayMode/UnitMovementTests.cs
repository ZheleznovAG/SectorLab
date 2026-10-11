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

            for (int frame = 0; frame < 600 && movement.IsMoving; frame++)
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
