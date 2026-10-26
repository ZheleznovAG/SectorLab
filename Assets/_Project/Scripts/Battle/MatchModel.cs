using System;
using R3;
using SectorLab.Objectives;
using SectorLab.Units;

namespace SectorLab.Battle
{
    // What a relay looks like to a viewer. Value equality lets ReactiveProperty skip unchanged ticks.
    public readonly struct RelayView : IEquatable<RelayView>
    {
        public readonly RelayOwner Owner;
        public readonly int Percent;       // capture progress 0..100 toward Side
        public readonly int Side;          // 0 nobody, 1 player, 2 enemy
        public readonly bool Contested;
        public readonly bool OwnerPresent; // an owner's unit stands on it (repair works)

        public RelayView(RelayOwner owner, int percent, int side, bool contested, bool ownerPresent)
        {
            Owner = owner; Percent = percent; Side = side; Contested = contested; OwnerPresent = ownerPresent;
        }

        public static RelayView From(RelayPoint relay)
        {
            RelayState state = relay.State;
            int side = state.Progress > MatchRules.Epsilon ? 1 : state.Progress < -MatchRules.Epsilon ? 2 : 0;
            bool present = state.Owner == RelayOwner.Player ? relay.PlayersPresent > 0
                : state.Owner == RelayOwner.Enemy && relay.EnemiesPresent > 0;
            return new RelayView(state.Owner, (int)Math.Round(Math.Abs(state.Progress) * 100), side, state.IsContested, present);
        }

        public bool Equals(RelayView other) => Owner == other.Owner && Percent == other.Percent && Side == other.Side
            && Contested == other.Contested && OwnerPresent == other.OwnerPresent;
        public override bool Equals(object obj) => obj is RelayView other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Owner, Percent, Side, Contested, OwnerPresent);
    }

    // The match as viewers see it. The authority publishes it after every tick; a network client
    // would fill the same properties from replicated data, so HUD and audio need not change.
    // Properties notify only when a value actually changes.
    public sealed class MatchModel : IDisposable
    {
        private UnitView[] units = Array.Empty<UnitView>();
        public ReadOnlySpan<UnitView> Units => units;
        public event Action Updated;

        public void SetUnitCount(int count)
        {
            if (units.Length != count) units = new UnitView[count];
        }
        public void SetUnit(int index, in UnitView view) => units[index] = view;
        public bool TryGetUnit(int id, out UnitView view)
        {
            for (int i = 0; i < units.Length; i++)
                if (units[i].Id == id) { view = units[i]; return true; }
            view = default;
            return false;
        }
        public bool TryGetRole(Team team, UnitRole role, out UnitView view)
        {
            for (int i = 0; i < units.Length; i++)
                if (units[i].Team == team && units[i].Role == role) { view = units[i]; return true; }
            view = default;
            return false;
        }
        // Publish once after all fields of a local update or validated snapshot are applied.
        public void Commit() => Updated?.Invoke();
        public readonly ReactiveProperty<double> PlayerScore = new ReactiveProperty<double>(0);
        public readonly ReactiveProperty<double> EnemyScore = new ReactiveProperty<double>(0);
        // Income actually earned right now: zero while paused, loading or contested.
        public readonly ReactiveProperty<double> PlayerRate = new ReactiveProperty<double>(0);
        public readonly ReactiveProperty<double> EnemyRate = new ReactiveProperty<double>(0);
        public readonly ReactiveProperty<int> RemainingSeconds = new ReactiveProperty<int>((int)MatchRules.DurationSeconds);
        public readonly ReactiveProperty<bool> Paused = new ReactiveProperty<bool>(false);
        public readonly ReactiveProperty<BattleOutcome> Outcome = new ReactiveProperty<BattleOutcome>(BattleOutcome.InProgress);
        public readonly ReactiveProperty<BattleOutcome> Forecast = new ReactiveProperty<BattleOutcome>(BattleOutcome.Draw);
        public ReactiveProperty<RelayView>[] Relays { get; private set; } = Array.Empty<ReactiveProperty<RelayView>>();

        // Running = scores and timers advance (not paused, not decided).
        public Observable<bool> Running => Paused.CombineLatest(Outcome, (paused, outcome) => !paused && outcome == BattleOutcome.InProgress)
            .DistinctUntilChanged();

        public void SetRelayCount(int count)
        {
            if (Relays.Length == count) return;
            foreach (var relay in Relays) relay.Dispose();
            Relays = new ReactiveProperty<RelayView>[count];
            for (int i = 0; i < count; i++) Relays[i] = new ReactiveProperty<RelayView>(default);
        }

        public static int WholeSeconds(double remaining) => (int)Math.Ceiling(remaining - MatchRules.Epsilon);
        public static int WholePoints(double score) => (int)Math.Floor(score + MatchRules.Epsilon);

        public void Dispose()
        {
            Updated = null;
            PlayerScore.Dispose(); EnemyScore.Dispose(); PlayerRate.Dispose(); EnemyRate.Dispose();
            RemainingSeconds.Dispose(); Paused.Dispose(); Outcome.Dispose(); Forecast.Dispose();
            foreach (var relay in Relays) relay.Dispose();
        }
    }
}
