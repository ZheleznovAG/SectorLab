using System.Collections;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class MainArenaTests
    {
        [UnityTest]
        public IEnumerator BothTeamsStartOutsideScoreCirclesAndReachEveryRelayWithoutGeometryOverlap()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
            var battle = Object.FindFirstObjectByType<BattleController>();
            var lifecycle = Object.FindFirstObjectByType<UnitLifecycle>();
            battle.enabled = false;
            Object.FindFirstObjectByType<EnemyTacticsController>().enabled = false;
            foreach (var slot in lifecycle.Slots)
            {
                slot.Unit.Attack.enabled = false;
                slot.Unit.Movement.Stop();
                var agent = slot.Unit.GetComponent<NavMeshAgent>();
                Assert.That(agent.isOnNavMesh, Is.True);
                Vector3 bottom = slot.Unit.transform.position + Vector3.up * (agent.radius + 0.05f);
                Vector3 top = slot.Unit.transform.position + Vector3.up * (agent.height - agent.radius);
                Assert.That(Physics.CheckCapsule(bottom, top, agent.radius, 1, QueryTriggerInteraction.Ignore), Is.False,
                    slot.Config.prefabKey + " must spawn clear of solid geometry.");
                Assert.That(battle.Relays[0].Contains(slot.Unit) || battle.Relays[1].Contains(slot.Unit), Is.False,
                    "Neither team gets capture time before its first move.");
                foreach (var opposite in lifecycle.Slots)
                    if (opposite.Config.role == slot.Config.role && opposite.Config.team != slot.Config.team)
                    {
                        Assert.That(Vector3.Distance(slot.Config.position, -opposite.Config.position), Is.LessThan(0.01f));
                        Vector3 sum = slot.Unit.transform.position + opposite.Unit.transform.position;
                        Assert.That(new Vector2(sum.x, sum.z).magnitude, Is.LessThan(0.15f),
                            "Physical NavMesh spawn must preserve the fair mirrored bases.");
                        Assert.That(Mathf.Abs(slot.Unit.transform.position.y - opposite.Unit.transform.position.y), Is.LessThan(0.05f));
                        Assert.That(slot.Unit.Stats, Is.SameAs(opposite.Unit.Stats), "Shared role data, no team bonus.");
                    }
            }
            Assert.That(Vector3.Distance(battle.Relays[0].transform.position, -battle.Relays[1].transform.position), Is.LessThan(0.01f));
            for (int pair = 1; pair <= 3; pair += 2)
            {
                Transform a = GameObject.Find("Block" + pair).transform;
                Transform b = GameObject.Find("Block" + (pair + 1)).transform;
                Vector3 offset = a.position + b.position;
                Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.LessThan(0.01f));
                Assert.That(a.localScale, Is.EqualTo(b.localScale));
            }
            foreach (var relay in battle.Relays)
            {
                foreach (var slot in lifecycle.Slots)
                {
                    Assert.That(relay.TryGetApproachPoint(slot.Unit, out Vector3 point), Is.True, slot.Config.prefabKey + " path to " + relay.Config.label);
                    Assert.That(slot.Unit.Movement.MoveTo(point, relay.GetArrivalDistance(point)), Is.True);
                }
                float deadline = Time.realtimeSinceStartup + 12;
                bool arrived;
                do
                {
                    yield return null;
                    arrived = true;
                    foreach (var slot in lifecycle.Slots) arrived &= relay.Contains(slot.Unit);
                } while (!arrived && Time.realtimeSinceStartup < deadline);
                Assert.That(arrived, Is.True, "All six units must actually reach " + relay.Config.label + " through the baked arena.");
            }
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            Time.timeScale = 1;
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterArena");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
            yield return null;
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(SectorLab.Infrastructure.AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
        }
    }
}
