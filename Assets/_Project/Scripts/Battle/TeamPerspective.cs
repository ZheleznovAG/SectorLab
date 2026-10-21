using R3;
using SectorLab.Objectives;
using SectorLab.Units;

namespace SectorLab.Battle
{
    // The match seen from one device's team: "mine" and "theirs" instead of Player and Enemy.
    // Simulation keeps absolute teams; only presentation goes through this.
    public readonly struct TeamPerspective
    {
        public readonly Team Local;
        public TeamPerspective(Team local) => Local = local;

        private bool LocalIsPlayer => Local == Team.Player;

        public bool IsMine(RelayOwner owner) => owner == (LocalIsPlayer ? RelayOwner.Player : RelayOwner.Enemy);
        public bool IsTheirs(RelayOwner owner) => owner == (LocalIsPlayer ? RelayOwner.Enemy : RelayOwner.Player);

        // RelayView.Side (1 player, 2 enemy) as 1 = mine, 2 = theirs.
        public int Side(int side) => LocalIsPlayer || side == 0 ? side : 3 - side;

        public BattleOutcome Outcome(BattleOutcome outcome) => LocalIsPlayer ? outcome
            : outcome == BattleOutcome.Victory ? BattleOutcome.Defeat
            : outcome == BattleOutcome.Defeat ? BattleOutcome.Victory : outcome;

        public ReactiveProperty<double> MyScore(MatchModel model) => LocalIsPlayer ? model.PlayerScore : model.EnemyScore;
        public ReactiveProperty<double> TheirScore(MatchModel model) => LocalIsPlayer ? model.EnemyScore : model.PlayerScore;
        public ReactiveProperty<double> MyRate(MatchModel model) => LocalIsPlayer ? model.PlayerRate : model.EnemyRate;
        public ReactiveProperty<double> TheirRate(MatchModel model) => LocalIsPlayer ? model.EnemyRate : model.PlayerRate;
    }
}
