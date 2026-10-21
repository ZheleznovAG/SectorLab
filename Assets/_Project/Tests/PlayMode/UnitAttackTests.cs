using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class UnitAttackTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1;
            root = new GameObject("TestRoot");
            TestArena.CreateGround(root.transform);
            TestArena.Bake(root);
            CombatTicker.AddTo(root);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1;
            Object.DestroyImmediate(root);
        }

        private static UnitStats Stats(int maxHealth, float speed, int damage, float range, float cooldown)
        {
            var stats = ScriptableObject.CreateInstance<UnitStats>();
            stats.maxHealth = maxHealth;
            stats.speed = speed;
            stats.damage = damage;
            stats.attackRange = range;
            stats.attackCooldown = cooldown;
            return stats;
        }

        private Unit CreateUnit(Team team, Vector3 position, int maxHealth = 100, float autoAcquireRange = 0f, UnitStats stats = null)
        {
            var go = new GameObject(team + "Unit");
            go.SetActive(false);
            go.transform.SetParent(root.transform);
            go.transform.position = position;
            go.AddComponent<UnitMovement>();
            Unit unit = go.AddComponent<Unit>();
            SetField(unit, "team", team);
            SetField(unit, "maxHealth", maxHealth);
            SetField(unit, "stats", stats);
            UnitAttack attack = go.AddComponent<UnitAttack>();
            SetField(attack, "autoAcquireRange", autoAcquireRange);
            go.SetActive(true);
            return unit;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        [Test]
        public void SetTarget_IgnoresAllies()
        {
            Unit a = CreateUnit(Team.Player, Vector3.zero);
            Unit b = CreateUnit(Team.Player, Vector3.right);

            a.Attack.SetTarget(b);

            Assert.That(a.Attack.Target, Is.Null);
        }

        [Test]
        public void DamageDependsOnGameTimeNotOnFramePacing()
        {
            var battle = root.AddComponent<SectorLab.Battle.BattleController>();
            battle.Initialize(new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock()));
            battle.enabled = false;
            UnitStats stats = Stats(500, 0, 10, 2, 0.5f);

            int Damage(double[] frames)
            {
                Unit attacker = CreateUnit(Team.Player, Vector3.zero, stats: stats);
                Unit target = CreateUnit(Team.Enemy, Vector3.right, maxHealth: 500);
                attacker.Attack.SetTarget(target);
                foreach (double frame in frames) battle.AdvanceSimulation(frame);
                int damage = target.Health.Max - target.Health.Current;
                Object.DestroyImmediate(attacker.gameObject);
                Object.DestroyImmediate(target.gameObject);
                return damage;
            }

            var steady = new double[120];
            for (int i = 0; i < steady.Length; i++) steady[i] = 1.0 / 60;          // 2 s at 60 FPS
            int smooth = Damage(steady);
            int stutter = Damage(new[] { 0.3, 0.02, 0.48, 0.1, 0.7, 0.4 });        // 2 s in uneven frames
            // Ticks at 0.1 s; cooldown 0.5 s: hits at 0.1, 0.6, 1.1, 1.6.
            Assert.That(smooth, Is.EqualTo(40));
            Assert.That(stutter, Is.EqualTo(smooth));
            Object.DestroyImmediate(stats);
        }

        [UnityTest]
        public IEnumerator PausedFirstShotWaitsForResumeWithoutDamageOrSound()
        {
            Unit attacker = CreateUnit(Team.Player, Vector3.zero);
            Unit enemy = CreateUnit(Team.Enemy, Vector3.right);
            int fired = 0;
            attacker.Attack.Fired += _ => fired++;
            attacker.Attack.SetTarget(enemy);
            Time.timeScale = 0;
            yield return null; yield return null;
            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max));
            Assert.That(fired, Is.Zero);
            Time.timeScale = 1;
            yield return null;
            Assert.That(enemy.Health.Current, Is.LessThan(enemy.Health.Max));
            Assert.That(fired, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TargetInRange_TakesDamageRightAway()
        {
            Unit attacker = CreateUnit(Team.Player, Vector3.zero);
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(1f, 0f, 0f));

            attacker.Attack.SetTarget(enemy);
            yield return null;

            Assert.That(enemy.Health.Current, Is.LessThan(enemy.Health.Max));
            Assert.That(attacker.Movement.IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator TargetOutOfRange_AttackerWalksUpToIt()
        {
            Unit attacker = CreateUnit(Team.Player, Vector3.zero);
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(4f, 0f, 0f));

            attacker.Attack.SetTarget(enemy);
            yield return null;

            Assert.That(attacker.Movement.IsMoving, Is.True);
            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max));

            float deadline = Time.time + 10f;
            while (Time.time < deadline && enemy.Health.Current == enemy.Health.Max)
                yield return null;

            Assert.That(attacker.Attack.IsInRange(enemy), Is.True);
            Assert.That(enemy.Health.Current, Is.LessThan(enemy.Health.Max));
        }

        [UnityTest]
        public IEnumerator Cooldown_LimitsHitsPerSecond()
        {
            Unit attacker = CreateUnit(Team.Player, Vector3.zero);
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(1f, 0f, 0f), maxHealth: 1000);

            attacker.Attack.SetTarget(enemy);
            yield return null;
            int afterFirstHit = enemy.Health.Current;
            yield return null;
            yield return null;

            Assert.That(enemy.Health.Current, Is.EqualTo(afterFirstHit));
        }

        [UnityTest]
        public IEnumerator IdleUnit_AcquiresNearestHostileInRange()
        {
            Unit guard = CreateUnit(Team.Enemy, Vector3.zero, autoAcquireRange: 4f);
            Unit far = CreateUnit(Team.Player, new Vector3(3.5f, 0f, 0f));
            Unit near = CreateUnit(Team.Player, new Vector3(0f, 0f, 2.5f));
            CreateUnit(Team.Enemy, new Vector3(1f, 0f, 0f));

            yield return null;

            Assert.That(guard.Attack.Target, Is.SameAs(near));
            Assert.That(far.Health.Current, Is.EqualTo(far.Health.Max));
        }

        [UnityTest]
        public IEnumerator IdleUnit_IgnoresHostilesOutsideRange()
        {
            Unit guard = CreateUnit(Team.Enemy, Vector3.zero, autoAcquireRange: 4f);
            CreateUnit(Team.Player, new Vector3(5f, 0f, 0f));

            yield return null;

            Assert.That(guard.Attack.Target, Is.Null);
        }

        [UnityTest]
        public IEnumerator MovingUnit_DoesNotAcquireTargets()
        {
            Unit runner = CreateUnit(Team.Player, Vector3.zero, autoAcquireRange: 4f);
            CreateUnit(Team.Enemy, new Vector3(2f, 0f, 0f));

            runner.Movement.MoveTo(new Vector3(0f, 0f, -8f));
            yield return null;

            Assert.That(runner.Attack.Target, Is.Null);
            Assert.That(runner.Movement.IsMoving, Is.True);
        }

        [Test]
        public void Stats_AreAppliedToHealthMovementAndAttack()
        {
            Unit tank = CreateUnit(Team.Player, Vector3.zero, stats: Stats(250, 2f, 12, 1.5f, 1.2f));

            Assert.That(tank.Health.Max, Is.EqualTo(250));
            Assert.That(tank.Movement.Speed, Is.EqualTo(2f));
            Assert.That(tank.Attack.Damage, Is.EqualTo(12));
            Assert.That(tank.Attack.Range, Is.EqualTo(1.5f));
            Assert.That(tank.Attack.Cooldown, Is.EqualTo(1.2f));
        }

        [UnityTest]
        public IEnumerator UnreachableTarget_DoesNotRestartMovementEveryFrame()
        {
            TestArena.CreateBlock(root.transform, new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 20f));
            root.GetComponent<Unity.AI.Navigation.NavMeshSurface>().BuildNavMesh();
            Unit attacker = CreateUnit(Team.Player, new Vector3(-3f, 0f, 0f));
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(3f, 0f, 0f));
            attacker.Attack.SetTarget(enemy);
            yield return null;
            var agent = attacker.GetComponent<UnityEngine.AI.NavMeshAgent>();
            for (int frame = 0; frame < 60 && agent.pathPending; frame++)
                yield return null;
            Assert.That(agent.pathStatus, Is.EqualTo(UnityEngine.AI.NavMeshPathStatus.PathPartial));

            // Force the stopped state, as at the end of a partial path. Observe actual
            // new movement attempts without exposing private retry timers to the test.
            int restarts = 0;
            float until = Time.time + 0.3f;
            while (Time.time < until)
            {
                attacker.Movement.Stop();
                yield return null;
                if (attacker.Movement.IsMoving)
                    restarts++;
            }

            Assert.That(restarts, Is.LessThanOrEqualTo(1), "A stationary unreachable target must not cause a request every frame");
            Assert.That(attacker.Attack.Target, Is.SameAs(enemy));
            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max));
        }

        [UnityTest]
        public IEnumerator UnreachableTarget_AfterOpeningPathIsAttacked()
        {
            GameObject wall = TestArena.CreateBlock(root.transform, new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 20f));
            var surface = root.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.BuildNavMesh();
            Unit attacker = CreateUnit(Team.Player, new Vector3(-3f, 0f, 0f));
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(3f, 0f, 0f));
            attacker.Attack.SetTarget(enemy);
            yield return new WaitForSeconds(1.2f);
            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max));
            Assert.That(attacker.transform.position.x, Is.LessThan(0f));

            Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            float deadline = Time.time + 10f;
            while (Time.time < deadline && enemy.Health.Current == enemy.Health.Max)
                yield return null;

            Assert.That(enemy.Health.Current, Is.LessThan(enemy.Health.Max), "Retrying must resume pursuit when the path opens");
        }

        [UnityTest]
        public IEnumerator TargetBehindObstacle_TakesNoDamageUntilObstacleIsRemoved()
        {
            Unit ranger = CreateUnit(Team.Player, Vector3.zero, stats: Stats(70, 0f, 20, 4f, 1f));
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(3f, 0f, 0f));
            GameObject block = TestArena.CreateBlock(root.transform, new Vector3(1.5f, 1f, 0f), new Vector3(0.5f, 4f, 2f));
            Physics.SyncTransforms();

            ranger.Attack.SetTarget(enemy);
            yield return null;
            yield return null;
            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max), "An obstacle must block damage, not just the shot visual");

            Object.DestroyImmediate(block);
            Physics.SyncTransforms();
            yield return null;
            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max - 20));
        }

        [UnityTest]
        public IEnumerator TargetBehindObstacle_AttackerWalksAroundAndHits()
        {
            GameObject block = TestArena.CreateBlock(root.transform, new Vector3(1.5f, 1f, 0f), new Vector3(0.5f, 2f, 2f));
            root.GetComponent<Unity.AI.Navigation.NavMeshSurface>().BuildNavMesh();
            Unit ranger = CreateUnit(Team.Player, Vector3.zero, stats: Stats(70, 3f, 20, 4f, 1f));
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(3f, 0f, 0f));
            Physics.SyncTransforms();

            ranger.Attack.SetTarget(enemy);
            yield return null;
            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max));
            Assert.That(ranger.Movement.IsMoving, Is.True);

            float deadline = Time.time + 10f;
            while (Time.time < deadline && enemy.Health.Current == enemy.Health.Max)
                yield return null;

            Assert.That(block, Is.Not.Null);
            Assert.That(enemy.Health.Current, Is.LessThan(enemy.Health.Max));
            Assert.That(Physics.Linecast(ranger.transform.position, enemy.transform.position, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore), Is.False);
        }

        [UnityTest]
        public IEnumerator RangedUnit_HitsFromDistanceWithoutMoving()
        {
            Unit ranger = CreateUnit(Team.Player, Vector3.zero, stats: Stats(70, 3f, 20, 4f, 1f));
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(3.5f, 0f, 0f));

            ranger.Attack.SetTarget(enemy);
            yield return null;

            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max - 20));
            // Only horizontal position matters: the agent snaps the unit onto the NavMesh height.
            Vector3 flat = ranger.transform.position;
            flat.y = 0f;
            Assert.That(flat.magnitude, Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator IdleRangedUnit_AcquiresAnythingWithinWeaponRange()
        {
            Unit ranger = CreateUnit(Team.Player, Vector3.zero, autoAcquireRange: 2.5f, stats: Stats(70, 3f, 20, 4f, 1f));
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(3.5f, 0f, 0f));

            yield return null;

            Assert.That(ranger.Attack.Target, Is.SameAs(enemy));
        }

        [UnityTest]
        public IEnumerator Damage_FlashesBodyBriefly()
        {
            Unit attacker = CreateUnit(Team.Player, Vector3.zero);
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(1f, 0f, 0f));
            enemy.gameObject.AddComponent<MeshRenderer>();
            var flash = enemy.gameObject.AddComponent<DamageFlash>();
            yield return null;

            attacker.Attack.SetTarget(enemy);
            yield return null;
            Assert.That(flash.IsFlashing, Is.True);

            yield return new WaitForSeconds(0.2f);
            Assert.That(flash.IsFlashing, Is.False);
        }

        [UnityTest]
        public IEnumerator KilledTarget_IsDestroyedAndCleared()
        {
            Unit attacker = CreateUnit(Team.Player, Vector3.zero);
            Unit enemy = CreateUnit(Team.Enemy, new Vector3(1f, 0f, 0f), maxHealth: 1);
            bool diedRaised = false;
            enemy.Died += _ => diedRaised = true;

            attacker.Attack.SetTarget(enemy);
            yield return null;
            yield return null;

            Assert.That(diedRaised, Is.True);
            Assert.That(enemy == null, Is.True, "Dead unit should be destroyed");
            Assert.That(attacker.Attack.Target == null, Is.True);
        }

        [UnityTest]
        public IEnumerator HealingDoesNotShowDamageFlash()
        {
            Unit unit = CreateUnit(Team.Player, Vector3.zero);
            unit.gameObject.AddComponent<MeshRenderer>();
            var flash = unit.gameObject.AddComponent<DamageFlash>();
            yield return null;
            unit.Health.TakeDamage(10);
            Assert.That(flash.IsFlashing, Is.True);
            yield return new WaitForSeconds(0.2f);
            Assert.That(flash.IsFlashing, Is.False);
            unit.Health.Heal(6);
            Assert.That(flash.IsFlashing, Is.False);
        }
    }
}
