using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace SectorLab.Tests
{
    public sealed class MatchScopeTests
    {
        private GameObject root;
        private readonly System.Collections.Generic.List<UnitRoster> rosters = new System.Collections.Generic.List<UnitRoster>();

        [TearDown]
        public void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            foreach (UnitRoster roster in rosters) Object.DestroyImmediate(roster);
            rosters.Clear();
            Time.timeScale = 1;
        }

        private GameLifetimeScope CreateScope()
        {
            root = new GameObject("ScopeUnderTest");
            root.SetActive(false);
            var battle = root.AddComponent<BattleController>();
            var ai = root.AddComponent<EnemyTacticsController>();
            var lifecycle = root.AddComponent<UnitLifecycle>();
            lifecycle.enabled = false; // This fixture tests DI; Main tests exercise actual asynchronous startup.
            var roster = ScriptableObject.CreateInstance<UnitRoster>();
            roster.slots = new UnitSpawnSlot[6];
            for (int i = 0; i < 6; i++) roster.slots[i] = new UnitSpawnSlot {
                team = i < 3 ? Team.Player : Team.Enemy, role = (UnitRole)(i % 3), prefabKey = "fixture"
            };
            rosters.Add(roster);
            var scope = root.AddComponent<GameLifetimeScope>();
            typeof(GameLifetimeScope).GetField("battle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scope, battle);
            typeof(GameLifetimeScope).GetField("enemyTactics", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scope, ai);
            typeof(GameLifetimeScope).GetField("roster", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scope, roster);
            typeof(GameLifetimeScope).GetField("unitLifecycle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scope, lifecycle);
            root.SetActive(true);
            return scope;
        }

        [UnityTest]
        public IEnumerator MatchUsesOneScopedStateAndClockAndANewSceneGetsNewInstances()
        {
            GameLifetimeScope first = CreateScope();
            var state = first.Container.Resolve<MatchState>();
            var clock = first.Container.Resolve<MatchClock>();
            var battle = root.GetComponent<BattleController>();
            var ai = root.GetComponent<EnemyTacticsController>();
            Assert.That(battle.State, Is.SameAs(state));
            Assert.That(state.Clock, Is.SameAs(clock));
            Assert.That(first.Container.Resolve<MatchClock>(), Is.SameAs(clock));
            battle.AdvanceSimulation(1);
            Assert.That(ai.DecisionCount, Is.EqualTo(1));
            first.DisposeCore();
            Assert.That(first.Container, Is.Null);
            Object.DestroyImmediate(root);
            GameLifetimeScope second = CreateScope();
            Assert.That(second.Container.Resolve<MatchState>(), Is.Not.SameAs(state));
            Assert.That(second.Container.Resolve<MatchClock>(), Is.Not.SameAs(clock));
            Assert.That(second.Container.Resolve<MatchClock>().ElapsedSeconds, Is.Zero);
            yield return null;
            Assert.That(second.Container.Resolve<MatchClock>().ElapsedSeconds, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void MissingClockRegistrationFailsInsteadOfConstructingAHiddenClock()
        {
            var builder = new ContainerBuilder();
            builder.Register<MatchState>(Lifetime.Scoped);
            using var container = builder.Build();
            Assert.Throws<VContainerException>(() => container.Resolve<MatchState>());
        }

        [Test]
        public void MissingStateRegistrationFailsComponentInjection()
        {
            root = new GameObject("UnwiredBattle");
            root.SetActive(false);
            var battle = root.AddComponent<BattleController>();
            using var container = new ContainerBuilder().Build();
            Assert.Throws<VContainerException>(() => container.Inject(battle));
            Assert.That(battle.State, Is.Null);
        }

        [UnityTest]
        public IEnumerator UnwiredBattleReportsMissingInjectionAndDoesNotCreateFallbackState()
        {
            root = new GameObject("UnwiredBattle");
            var battle = root.AddComponent<BattleController>();
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("MatchState was not injected"));
            yield return null;
            Assert.That(battle.State, Is.Null);
        }

        [UnityTest]
        public IEnumerator UnwiredEnemyReportsMissingInjection()
        {
            root = new GameObject("UnwiredAI");
            var ai = root.AddComponent<EnemyTacticsController>();
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Enemy tactics requires injected BattleController"));
            yield return null;
            Assert.That(ai.DecisionCount, Is.Zero);
        }
    }
}
