using System.Collections;
using NUnit.Framework;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class UnitMovementTests
    {
        private GameObject unitObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(unitObject);
        }

        private UnitMovement CreateUnit(Vector3 position)
        {
            unitObject = new GameObject("TestUnit");
            unitObject.transform.position = position;
            return unitObject.AddComponent<UnitMovement>();
        }

        [UnityTest]
        public IEnumerator MoveTo_GetsCloserEachFrame()
        {
            UnitMovement movement = CreateUnit(Vector3.zero);
            movement.MoveTo(new Vector3(10f, 0f, 0f));

            yield return null;
            float afterFirstFrame = unitObject.transform.position.x;
            yield return null;

            Assert.That(afterFirstFrame, Is.GreaterThan(0f));
            Assert.That(unitObject.transform.position.x, Is.GreaterThan(afterFirstFrame));
            Assert.That(movement.IsMoving, Is.True);
        }

        [UnityTest]
        public IEnumerator MoveTo_StopsExactlyOnDestination()
        {
            UnitMovement movement = CreateUnit(Vector3.zero);
            var destination = new Vector3(0.5f, 0f, 0.5f);
            movement.MoveTo(destination);

            for (int frame = 0; frame < 300 && movement.IsMoving; frame++)
                yield return null;

            Assert.That(movement.IsMoving, Is.False);
            Assert.That(unitObject.transform.position, Is.EqualTo(destination));
        }

        [UnityTest]
        public IEnumerator MoveTo_KeepsUnitHeight()
        {
            UnitMovement movement = CreateUnit(new Vector3(0f, 0.8f, 0f));
            movement.MoveTo(new Vector3(0.2f, 0f, 0f));

            for (int frame = 0; frame < 300 && movement.IsMoving; frame++)
                yield return null;

            Assert.That(unitObject.transform.position.y, Is.EqualTo(0.8f));
        }

        [UnityTest]
        public IEnumerator Stop_HaltsMovement()
        {
            UnitMovement movement = CreateUnit(Vector3.zero);
            movement.MoveTo(new Vector3(10f, 0f, 0f));
            yield return null;

            movement.Stop();
            Vector3 stoppedAt = unitObject.transform.position;
            yield return null;

            Assert.That(movement.IsMoving, Is.False);
            Assert.That(unitObject.transform.position, Is.EqualTo(stoppedAt));
        }
    }
}
