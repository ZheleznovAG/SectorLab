using System;
using System.Collections.Generic;
using SectorLab.Battle;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.AI
{
    // One decision per game second. Decisions submit the same validated match commands as the player.
    public sealed class EnemyTacticsController : MonoBehaviour
    {
        private BattleController battle;
        private readonly List<Unit> actors = new List<Unit>(3);
        private readonly Dictionary<Unit, RelayPoint> assignments = new Dictionary<Unit, RelayPoint>(3);
        private readonly List<Unit> staleAssignments = new List<Unit>(3);
        private RelayOption[] options;
        private Vector3[] approaches;
        private int[] claimed;
        private double decisionAccumulator;
        private bool initialized;
        private bool subscribed;

        public int DecisionCount { get; private set; }
        public int MoveOrdersIssued { get; private set; }
        public RelayPoint GetAssignedRelay(Unit unit) => assignments.TryGetValue(unit, out RelayPoint relay) ? relay : null;

        private void Start()
        {
            if (!initialized) throw new InvalidOperationException("Enemy tactics requires injected BattleController.");
        }

        [VContainer.Inject]
        public void Initialize(BattleController controller)
        {
            Unsubscribe();
            battle = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            options = new RelayOption[battle.Relays.Count];
            approaches = new Vector3[battle.Relays.Count];
            claimed = new int[battle.Relays.Count];
            initialized = true;
            decisionAccumulator = 0;
            assignments.Clear();
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() { if (initialized) Subscribe(); }
        private void OnDisable()
        {
            Unsubscribe();
            actors.Clear();
            assignments.Clear();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            battle.Ticked += OnTick;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (battle != null) battle.Ticked -= OnTick;
            subscribed = false;
        }

        private void OnTick(double seconds)
        {
            if (!battle.State.IsRunning) return;
            decisionAccumulator += seconds;
            if (decisionAccumulator < 1 - 1e-6) return;
            decisionAccumulator -= 1;
            Think();
        }

        private static int CompareActors(Unit a, Unit b)
        {
            if (a == b) return 0;
            int speed = b.Movement.Speed.CompareTo(a.Movement.Speed);
            if (speed != 0) return speed;
            int role = RoleOrder(a).CompareTo(RoleOrder(b));
            if (role != 0) return role;
            // Roster ids are stable across respawns and independent of other matches.
            return a.Id.CompareTo(b.Id);
        }

        // Alphabetical role names (Ranger, Scout, Tank) were the historical tie-break; keep that order.
        private static int RoleOrder(Unit unit) => unit.Stats == null ? -1
            : unit.Stats.role == UnitRole.Ranger ? 0 : unit.Stats.role == UnitRole.Scout ? 1 : 2;

        private void Think()
        {
            DecisionCount++;
            actors.Clear();
            for (int i = 0; i < battle.Units.All.Count; i++)
                if (battle.Units.All[i].Team == Team.Enemy && battle.Units.All[i].IsAlive) actors.Add(battle.Units.All[i]);
            actors.Sort(CompareActors);
            staleAssignments.Clear();
            foreach (var entry in assignments)
                if (entry.Key == null || !entry.Key.IsAlive || !entry.Key.isActiveAndEnabled) staleAssignments.Add(entry.Key);
            for (int i = 0; i < staleAssignments.Count; i++) assignments.Remove(staleAssignments[i]);
            Array.Clear(claimed, 0, claimed.Length);

            for (int actorIndex = 0; actorIndex < actors.Count; actorIndex++)
            {
                Unit unit = actors[actorIndex];
                for (int i = 0; i < options.Length; i++)
                {
                    RelayPoint relay = battle.Relays[i];
                    bool reachable = relay.TryGetApproachPoint(unit, out approaches[i]);
                    bool threatened = relay.PlayersPresent > 0 && (relay.State.Owner == RelayOwner.Enemy || relay.State.Progress < 0);
                    float cost = Vector3.Distance(unit.transform.position, relay.transform.position) + claimed[i] * 8;
                    options[i] = new RelayOption(relay.Config.kind, relay.State.Owner, threatened, reachable, cost);
                }
                Unit nearbyHostile = FindInterference(unit, null);
                EnemyGoal goal = EnemyDecisionRules.Choose(options, unit.Health.Fraction, nearbyHostile != null);
                if (goal.Kind == EnemyGoalKind.Attack)
                {
                    assignments.Remove(unit);
                    Attack(unit, nearbyHostile);
                    continue;
                }
                if (goal.RelayIndex < 0) continue;
                int index = goal.RelayIndex;
                claimed[index]++;
                RelayPoint target = battle.Relays[index];
                assignments[unit] = target;
                Unit blocker = FindInterference(unit, target);
                if (blocker != null)
                {
                    Attack(unit, blocker);
                    continue;
                }
                if (target.Contains(unit))
                {
                    battle.Commands.Execute(goal.Kind == EnemyGoalKind.Repair
                        ? UnitCommand.Stop(Team.Enemy, unit.Id) : UnitCommand.HoldPosition(Team.Enemy, unit.Id));
                    continue;
                }
                // Keep an accepted unchanged path; do not send SetDestination every decision/frame.
                if (unit.Movement.IsMoving && (unit.Movement.Destination - approaches[index]).sqrMagnitude < 0.01f) continue;
                if (battle.Commands.Execute(UnitCommand.MoveToRelay(Team.Enemy, unit.Id, index)) == CommandResult.Accepted)
                    MoveOrdersIssued++;
            }
        }

        private void Attack(Unit unit, Unit target)
        {
            if (target != null && unit.Attack != null && unit.Attack.Target != target)
                battle.Commands.Execute(UnitCommand.Attack(Team.Enemy, unit.Id, target.Id));
        }

        private Unit FindInterference(Unit actor, RelayPoint relay)
        {
            if (actor.Attack == null) return null;
            float maxDistance = actor.Attack.Range + 0.5f;
            Unit nearest = null;
            float nearestDistance = maxDistance * maxDistance;
            for (int i = 0; i < battle.Units.All.Count; i++)
            {
                Unit unit = battle.Units.All[i];
                if (!unit.IsAlive || unit.Team != Team.Player || relay != null && !relay.Contains(unit)) continue;
                Vector3 offset = unit.transform.position - actor.transform.position;
                offset.y = 0;
                float distance = offset.sqrMagnitude;
                if (distance < nearestDistance) { nearest = unit; nearestDistance = distance; }
            }
            return nearest;
        }
    }
}
