using System.Reflection;
using NUnit.Framework;
using SectorLab.Player;
using SectorLab.Presentation;
using UnityEngine;

namespace SectorLab.Tests
{
    public class TacticalCameraTests
    {
        private GameObject root;
        private TacticalCamera tactical;
        private Camera view;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("CameraFixture");
            root.SetActive(false);
            view = root.AddComponent<Camera>();
            var commander = root.AddComponent<UnitCommander>();
            typeof(UnitCommander).GetField("viewCamera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(commander, view);
            tactical = root.AddComponent<TacticalCamera>();
            typeof(TacticalCamera).GetField("commander", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tactical, commander);
            root.SetActive(true);
        }

        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);

        [Test]
        public void PanKeepsGroundUnderFingerAndClampsFocusAtArenaBounds()
        {
            Vector2 start = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var plane = new Plane(Vector3.up, Vector3.zero);
            Ray ray = view.ScreenPointToRay(start);
            Assert.That(plane.Raycast(ray, out float distance), Is.True);
            Vector3 anchor = ray.GetPoint(distance);
            Vector2 end = start + Vector2.right * 30;
            tactical.Pan(start, end);
            Assert.That(Vector2.Distance(view.WorldToScreenPoint(anchor), end), Is.LessThan(0.1f));
            Quaternion angle = view.transform.rotation;
            for (int i = 0; i < 50; i++) tactical.Pan(start, end + Vector2.right * 100);
            Assert.That(Mathf.Abs(tactical.Focus.x), Is.LessThanOrEqualTo(2.5f));
            Assert.That(Mathf.Abs(tactical.Focus.z), Is.LessThanOrEqualTo(2.5f));
            Assert.That(view.transform.rotation, Is.EqualTo(angle));
        }

        [Test]
        public void ZoomClampsDistanceAndPreservesFocusAndAngle()
        {
            Quaternion angle = view.transform.rotation;
            tactical.Zoom(0.001f);
            Assert.That(tactical.Distance, Is.EqualTo(9));
            tactical.Zoom(1000);
            Assert.That(tactical.Distance, Is.EqualTo(22));
            Assert.That(tactical.Focus, Is.EqualTo(Vector3.zero));
            Assert.That(view.transform.rotation, Is.EqualTo(angle));
        }

        [TestCase(0f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidZoomCannotCorruptTheCamera(float factor)
        {
            Vector3 position = view.transform.position;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => tactical.Zoom(factor));
            Assert.That(view.transform.position, Is.EqualTo(position));
        }
    }
}
