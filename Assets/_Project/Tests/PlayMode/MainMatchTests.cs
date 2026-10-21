using System.Collections;
using System.Globalization;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    // Scripted plans exercise the real Main/NavMesh/combat. These are not human playtests.
    public class MainMatchTests
    {
        [UnityTest]
        public IEnumerator FourMainMatchesUseDifferentPlansAndResetTheirState()
        {
            string report = "utc,plan,game_seconds,player_score,enemy_score,outcome,ai_decisions,ai_move_orders\n";
            string[] plans = { "passive", "split", "focus-A", "hold-A-then-flank-B" };
            MatchState previousState = null;
#if UNITY_EDITOR
            string task = System.Environment.GetEnvironmentVariable("SECTORLAB_VERIFICATION_TASK");
            if (string.IsNullOrEmpty(task)) task = "manual-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string reportPath = System.IO.Path.Combine(Application.dataPath, "../docs/agent_execution/evidence/" + task + "-main-matches.csv");
#endif
            for (int run = 0; run < plans.Length; run++)
            {
                yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                yield return MainLifecycleTests.WaitForReady();
                var battle = Object.FindAnyObjectByType<BattleController>();
                var ai = Object.FindAnyObjectByType<EnemyTacticsController>();
                Assert.That(battle, Is.Not.Null);
                Assert.That(ai, Is.Not.Null);
                var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
                Assert.That(scope, Is.Not.Null);
                Assert.That(battle.State, Is.Not.SameAs(previousState));
                if (previousState != null) Assert.That(battle.State.Clock, Is.Not.SameAs(previousState.Clock));
                previousState = battle.State;
                Assert.That(Unit.All.Count, Is.EqualTo(6), "A new Main must have one unit of each role/team.");
                Assert.That(battle.State.PlayerScore, Is.Zero);
                Assert.That(battle.State.EnemyScore, Is.Zero);
                Assert.That(battle.State.Clock.ElapsedSeconds, Is.LessThan(1));
                Assert.That(battle.Relays.Count, Is.EqualTo(3));
                Time.timeScale = 4;
                if (plans[run] != "passive")
                {
                    for (int i = 0; i < Unit.All.Count; i++)
                    {
                        Unit unit = Unit.All[i];
                        if (unit.Team != Team.Player) continue;
                        int relayIndex = plans[run] != "split" ? 0 : unit.Stats.role == UnitRole.Tank ? 0 : unit.Stats.role == UnitRole.Scout ? 1 : 2;
                        RelayPoint relay = battle.Relays[relayIndex];
                        Assert.That(relay.TryGetApproachPoint(unit, out Vector3 point), Is.True);
                        Assert.That(unit.Movement.MoveTo(point, relay.GetArrivalDistance(point)), Is.True);
                        unit.Attack.ClearTarget();
                    }
                }
                float deadline = Time.realtimeSinceStartup + 60;
                bool flanked = false;
                while (battle.Outcome == BattleOutcome.InProgress && Time.realtimeSinceStartup < deadline)
                {
                    if (plans[run] == "hold-A-then-flank-B" && !flanked && battle.State.Clock.ElapsedSeconds >= 12)
                    {
                        for (int i = 0; i < Unit.All.Count; i++)
                        {
                            Unit unit = Unit.All[i];
                            if (unit.Team != Team.Player || unit.Stats.role != UnitRole.Scout) continue;
                            RelayPoint relay = battle.Relays[1];
                            if (relay.TryGetApproachPoint(unit, out Vector3 point) && unit.Movement.MoveTo(point, relay.GetArrivalDistance(point)))
                            {
                                unit.Attack.ClearTarget();
                                flanked = true;
                            }
                        }
                    }
                    yield return null;
                }
                Assert.That(battle.Outcome, Is.Not.EqualTo(BattleOutcome.InProgress), plans[run] + " did not finish.");
                Assert.That(battle.State.Clock.ElapsedSeconds, Is.LessThanOrEqualTo(180));
                Assert.That(ai.DecisionCount, Is.GreaterThan(0));
                Assert.That(ai.MoveOrdersIssued, Is.GreaterThan(0));
                Assert.That(battle.State.PlayerScore > 0 || battle.State.EnemyScore > 0, Is.True);
                Assert.That(Time.timeScale, Is.Zero);
                for (int i = 0; i < Unit.All.Count; i++)
                {
                    Assert.That(Unit.All[i].Movement.IsMoving, Is.False);
                    Assert.That(Unit.All[i].Attack.enabled, Is.False);
                }
                report += System.DateTime.UtcNow.ToString("o") + "," + plans[run] + ","
                    + battle.State.Clock.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture) + ","
                    + battle.State.PlayerScore.ToString("0.0", CultureInfo.InvariantCulture) + ","
                    + battle.State.EnemyScore.ToString("0.0", CultureInfo.InvariantCulture) + ","
                    + battle.Outcome + "," + ai.DecisionCount + "," + ai.MoveOrdersIssued + "\n";
#if UNITY_EDITOR
                System.IO.File.WriteAllText(reportPath, report);
#endif
                Debug.Log("Main plan " + plans[run] + " finished " + battle.Outcome);
            }
        }

        [UnityTearDown]
        public IEnumerator UnloadMainAndRemoveItsUnitsAndSubscriptions()
        {
            Time.timeScale = 1;
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("TestsAfterMain");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
            yield return MainLifecycleTests.WaitForReleasedLoads();
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(SectorLab.Infrastructure.AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }
    }
}
