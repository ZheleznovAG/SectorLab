using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Player;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    // These scripts issue the real player's orders, including replacements after respawn.
    // They measure natural matches, not a human playtest or injected score/health.
    public class MainTacticsTests
    {
        // Five matches, each guarded at 150 s of real time below; the default 180 s suite timeout is too short.
        [UnityTest, Timeout(900000)]
        public IEnumerator FiveSustainedPlansFinishWithRealCapturesCombatAndRespawns()
        {
            string report = "utc,plan,game_seconds,player_score,enemy_score,outcome,player_orders,player_deaths,enemy_deaths,player_captures,enemy_captures\n";
            string[] plans = { "split-pressure", "hold-then-flank", "repair-rotation", "cover-B-intercept-A", "take-B-then-flank-A" };
            string task = System.Environment.GetEnvironmentVariable("SECTORLAB_VERIFICATION_TASK");
            if (string.IsNullOrEmpty(task)) task = "manual-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string reportPath = System.IO.Path.Combine(Application.dataPath, "../docs/agent_execution/evidence/" + task + "-sustained-plans.csv");
            foreach (string plan in plans)
            {
                yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                yield return MainLifecycleTests.WaitForReady();
                var battle = Object.FindAnyObjectByType<BattleController>();
                var lifecycle = Object.FindAnyObjectByType<UnitLifecycle>();
                var commander = Object.FindAnyObjectByType<UnitCommander>();
                var destinations = new Dictionary<Unit, RelayPoint>();
                int orders = 0, playerDeaths = 0, enemyDeaths = 0, playerCaptures = 0, enemyCaptures = 0;
                var previousOwners = new RelayOwner[battle.Relays.Count];
                System.Action<Unit> onDeath = unit => { if (unit.Team == Team.Player) playerDeaths++; else enemyDeaths++; };
                System.Action<Unit> onSpawn = unit => unit.Died += onDeath;
                lifecycle.Spawned += onSpawn;
                foreach (var slot in lifecycle.Slots) onSpawn(slot.Unit);
                Time.timeScale = 4;
                bool securedB = false;
                double nextOrders = 0;
                // Rules end every match by 180 game seconds; this only guards against a hang on a slow machine.
                float deadline = Time.realtimeSinceStartup + 150;
                while (battle.Outcome == BattleOutcome.InProgress && Time.realtimeSinceStartup < deadline)
                {
                    if (battle.State.Clock.ElapsedSeconds >= nextOrders)
                    {
                        nextOrders = battle.State.Clock.ElapsedSeconds + 1;
                        foreach (var slot in lifecycle.Slots)
                        {
                            Unit unit = slot.Unit;
                            if (slot.Config.team != Team.Player || unit == null || !unit.IsAlive) continue;
                            int index = slot.Config.role == UnitRole.Scout ? 1 : 0;
                            if (plan == "cover-B-intercept-A") index = slot.Config.role == UnitRole.Scout ? 0 : 1;
                            if (plan == "take-B-then-flank-A")
                            {
                                securedB |= battle.Relays[1].State.Owner == RelayOwner.Player;
                                index = securedB && slot.Config.role == UnitRole.Scout ? 0 : 1;
                            }
                            if (plan == "hold-then-flank" && battle.State.Clock.ElapsedSeconds < 10) index = 0;
                            if (plan == "repair-rotation")
                            {
                                if (slot.Config.role == UnitRole.Scout && battle.Relays[2].State.Owner != RelayOwner.Player) index = 2;
                                if (unit.Health.Fraction < 0.35f && battle.Relays[2].State.Owner == RelayOwner.Player) index = 2;
                                if (destinations.TryGetValue(unit, out var healing) && healing == battle.Relays[2]
                                    && unit.Health.Fraction < 0.85f && battle.Relays[2].State.Owner == RelayOwner.Player) index = 2;
                            }
                            RelayPoint target = battle.Relays[index];
                            bool changed = !destinations.TryGetValue(unit, out var previous) || previous != target;
                            bool idleAway = !unit.Movement.IsMoving && unit.Attack.Target == null && !target.Contains(unit);
                            bool chasingAway = !target.Contains(unit) && unit.Attack.Target != null;
                            if (!changed && !idleAway && !chasingAway) continue;
                            Assert.That(commander.SelectFriendly(unit), Is.True);
                            Assert.That(commander.OrderSelectedToRelay(target), Is.True, plan + ": " + slot.Config.role);
                            destinations[unit] = target;
                            orders++;
                        }
                        for (int i = 0; i < previousOwners.Length; i++)
                        {
                            RelayOwner owner = battle.Relays[i].State.Owner;
                            if (owner != previousOwners[i])
                            {
                                if (owner == RelayOwner.Player) playerCaptures++;
                                if (owner == RelayOwner.Enemy) enemyCaptures++;
                                previousOwners[i] = owner;
                            }
                        }
                    }
                    yield return null;
                }
                lifecycle.Spawned -= onSpawn;
                foreach (var slot in lifecycle.Slots) if (slot.Unit != null) slot.Unit.Died -= onDeath;
                Assert.That(battle.Outcome, Is.Not.EqualTo(BattleOutcome.InProgress), plan + " must finish naturally.");
                Assert.That(orders, Is.GreaterThanOrEqualTo(3));
                report += System.DateTime.UtcNow.ToString("o") + "," + plan + ","
                    + battle.State.Clock.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture) + ","
                    + battle.State.PlayerScore.ToString("0.0", CultureInfo.InvariantCulture) + ","
                    + battle.State.EnemyScore.ToString("0.0", CultureInfo.InvariantCulture) + ","
                    + battle.Outcome + "," + orders + "," + playerDeaths + "," + enemyDeaths + "," + playerCaptures + "," + enemyCaptures + "\n";
                System.IO.File.WriteAllText(reportPath, report);
                Debug.Log("Sustained " + plan + ": " + battle.Outcome);
            }
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            Time.timeScale = 1;
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterTactics");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
            yield return MainLifecycleTests.WaitForReleasedLoads();
            Assert.That(Unit.All.Count, Is.Zero);
            Assert.That(SectorLab.Infrastructure.AddressableUnitPrefabProvider.OutstandingHandles, Is.Zero);
        }
    }
}
