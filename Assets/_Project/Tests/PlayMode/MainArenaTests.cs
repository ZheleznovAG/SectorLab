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
        public IEnumerator BothTeamsStartFairlyAndEachSquadReachesEveryRelayWithoutGeometryOverlap()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
            var battle = Object.FindAnyObjectByType<BattleController>();
            var lifecycle = Object.FindAnyObjectByType<UnitLifecycle>();
            battle.enabled = false;
            Object.FindAnyObjectByType<EnemyTacticsController>().enabled = false;
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
                foreach (Team team in new[] { Team.Player, Team.Enemy })
                {
                    // Test each three-unit squad from its own bases, independently of a six-agent
                    // standstill with combat disabled. Both teams use their real movement paths.
                    foreach (var slot in lifecycle.Slots)
                    {
                        slot.Unit.Movement.Stop();
                        Assert.That(slot.Unit.GetComponent<NavMeshAgent>().Warp(slot.Config.position), Is.True);
                    }
                    yield return null;
                    foreach (var slot in lifecycle.Slots)
                    {
                        if (slot.Config.team != team) continue;
                        Assert.That(relay.TryGetApproachPoint(slot.Unit, out Vector3 point), Is.True, slot.Config.prefabKey + " path to " + relay.Config.label);
                        Assert.That(slot.Unit.Movement.MoveTo(point, relay.GetArrivalDistance(point)), Is.True);
                    }
                    float deadline = Time.realtimeSinceStartup + 12;
                    bool arrived;
                    do
                    {
                        yield return null;
                        arrived = true;
                        foreach (var slot in lifecycle.Slots) if (slot.Config.team == team) arrived &= relay.Contains(slot.Unit);
                    } while (!arrived && Time.realtimeSinceStartup < deadline);
                    if (!arrived)
                        foreach (var slot in lifecycle.Slots)
                        {
                            if (slot.Config.team != team) continue;
                            var agent = slot.Unit.GetComponent<NavMeshAgent>();
                            Debug.Log(slot.Config.prefabKey + " to " + relay.Config.label + ": position=" + slot.Unit.transform.position
                                + ", radius=" + agent.radius + ", scale=" + agent.transform.localScale + ", moving=" + slot.Unit.Movement.IsMoving
                                + ", path=" + agent.pathStatus + ", remaining=" + agent.remainingDistance + ", stop=" + agent.stoppingDistance);
                        }
                    Assert.That(arrived, Is.True, team + " squad must actually reach " + relay.Config.label + " through the baked arena.");
                }
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
