using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.AI;
using SectorLab.Battle;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace SectorLab.Tests
{
    public sealed class MatchScopeTests
    {
        private GameObject root;

        [TearDown]
        public void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            Time.timeScale = 1;
        }

        private GameLifetimeScope CreateScope()
        {
            root = new GameObject("ScopeUnderTest");
            root.SetActive(false);
            var battle = root.AddComponent<BattleController>();
            var ai = root.AddComponent<EnemyTacticsController>();
            var scope = root.AddComponent<GameLifetimeScope>();
            typeof(GameLifetimeScope).GetField("battle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scope, battle);
            typeof(GameLifetimeScope).GetField("enemyTactics", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scope, ai);
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
    }
}
