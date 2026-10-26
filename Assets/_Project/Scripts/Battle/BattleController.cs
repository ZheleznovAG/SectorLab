using System;
using SectorLab.Player;
using SectorLab.Objectives;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SectorLab.Battle
{
    // Owns fixed game ticks and the scene boundary. Unit deaths no longer decide the match.
    public class BattleController : MonoBehaviour
    {
        [SerializeField] private UnitCommander commander;
        [SerializeField] private BattleResultView resultView;
        [SerializeField] private RelayPoint[] relays = Array.Empty<RelayPoint>();

        public const double TickSeconds = 0.1;
        public MatchState State { get; private set; }
        public BattleOutcome Outcome => State.Outcome;
        public System.Collections.Generic.IReadOnlyList<RelayPoint> Relays => relays;
        private MatchCommands commands;
        private UnitLifecycle lifecycle;
        public void BindLifecycle(UnitLifecycle value) => lifecycle = value;
        // Player orders enter the simulation here, validated against this match's relays and state.
        public MatchCommands Commands => commands ??= BindCommands();

        // Viewer-facing state, published after every change; HUD and audio subscribe to it.
        public MatchModel Model { get; } = new MatchModel();
        // Which side this device plays; presentation reads "mine/theirs" through it.
        public TeamPerspective Perspective => new TeamPerspective(commander != null ? commander.LocalTeam : Team.Player);

        public event Action<BattleOutcome> Finished;
        public event Action<double> Ticked;

        private double accumulatedSeconds;
        private float runningTimeScale;
        private PauseReason pauses;
        public bool IsPausedBy(PauseReason reason) => (pauses & reason) != 0;

        private void Awake()
        {
            runningTimeScale = Time.timeScale > 0 ? Time.timeScale : 1;
            Model.Updated += PresentModel;
        }

        private void PresentModel()
        {
            for (int i = 0; i < relays.Length && i < Model.Relays.Length; i++)
                if (relays[i] != null) relays[i].Show(Model.Relays[i].Value);
            if (commander != null) commander.ValidateSelection();
            if (!IsAuthority) ApplyRunState();
        }

        private void Start()
        {
            if (State == null)
                throw new InvalidOperationException("MatchState was not injected. Main requires GameLifetimeScope.");
        }

        // The composition root supplies the same state/clock to consumers before the first tick.
        [VContainer.Inject]
        public void Initialize(MatchState state)
        {
            if (State != null && State.Clock.ElapsedSeconds > 0)
                throw new InvalidOperationException("Cannot replace a running match.");
            State = state ?? throw new ArgumentNullException(nameof(state));
            commands = BindCommands();
            PublishModel();
        }

        public void InitializeRelays(RelayPoint[] points)
        {
            relays = points ?? throw new ArgumentNullException(nameof(points));
            commands = BindCommands();
            PublishModel();
        }

        private MatchCommands BindCommands()
        {
            var created = new MatchCommands(relays, State);
            if (commander != null) { commander.Bind(created); commander.BindModel(Model); }
            return created;
        }

        private void Update()
        {
            if (State == null || !State.IsRunning)
                return;
            accumulatedSeconds += Time.deltaTime;
            while (accumulatedSeconds >= TickSeconds && State.IsRunning)
            {
                accumulatedSeconds -= TickSeconds;
                AdvanceSimulation(TickSeconds);
            }
        }

        // Editor scenarios and Update use the same bounded ticks, including long frames.
        public void AdvanceSimulation(double seconds)
        {
            if (!State.IsRunning)
                return;
            if (!MatchRules.IsValid(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            while (seconds > MatchRules.Epsilon && State.IsRunning)
            {
                double tick = Math.Min(TickSeconds, Math.Min(seconds, State.Clock.RemainingSeconds));
                SimulateTick(tick);
                seconds -= tick;
            }
        }

        private void SimulateTick(double tick)
        {
            // Combat first, so a unit killed this tick no longer holds a relay.
            for (int i = 0; i < Unit.All.Count; i++)
            {
                UnitAttack attack = Unit.All[i].Attack;
                if (attack != null) attack.Simulate(tick);
            }
            double playerPoints = 0;
            double enemyPoints = 0;
            for (int i = 0; i < relays.Length; i++)
            {
                relays[i].Simulate(tick);
                RelayPoint relay = relays[i];
                if (relay.Config.kind != RelayKind.Score) continue;
                double points = relay.State.EffectiveSeconds * relay.Config.scorePerSecond;
                if (relay.State.Owner == RelayOwner.Player) playerPoints += points;
                if (relay.State.Owner == RelayOwner.Enemy) enemyPoints += points;
            }
            double advanced = State.Tick(tick, tick > 0 ? playerPoints / tick : 0, tick > 0 ? enemyPoints / tick : 0);
            PublishModel();
            if (State.IsRunning)
            {
                Ticked?.Invoke(advanced);
                return;
            }

            ApplyRunState();
            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit unit = Unit.All[i];
                unit.Movement.Stop();
                if (unit.Attack != null)
                {
                    unit.Attack.ClearTarget();
                    unit.Attack.enabled = false;
                }
            }
            if (commander != null)
                commander.ClearSelection();
            if (resultView != null)
            {
                bool localIsPlayer = Perspective.Local == Team.Player;
                resultView.Show(Perspective.Outcome(Outcome), Restart,
                    localIsPlayer ? State.PlayerScore : State.EnemyScore, localIsPlayer ? State.EnemyScore : State.PlayerScore);
            }
            Finished?.Invoke(Outcome);
        }

        // Current income: fully owned, uncontested score relays.
        public void CurrentRates(out double player, out double enemy)
        {
            player = enemy = 0;
            for (int i = 0; i < relays.Length; i++)
            {
                RelayPoint relay = relays[i];
                if (relay.Config.kind != RelayKind.Score || relay.State.IsContested) continue;
                if (relay.State.Owner == RelayOwner.Player) player += relay.Config.scorePerSecond;
                if (relay.State.Owner == RelayOwner.Enemy) enemy += relay.Config.scorePerSecond;
            }
        }

        public BattleOutcome Forecast()
        {
            if (!State.IsRunning && !State.Clock.IsPaused) return State.Outcome;
            CurrentRates(out double player, out double enemy);
            return MatchRules.Forecast(State.PlayerScore, State.EnemyScore, player, enemy, State.Clock.RemainingSeconds);
        }

        // A client only blocks its own input; the host's clock and local effects keep running.
        public bool IsAuthority { get; private set; } = true;

        public void BecomeRemoteView()
        {
            IsAuthority = false;
            enabled = false;
            Time.timeScale = runningTimeScale;
            ApplyRunState();
        }

        public void SetPaused(bool paused) => SetPaused(PauseReason.User, paused);

        public void SetPaused(PauseReason reason, bool paused)
        {
            int bits = (int)reason;
            if (bits <= 0 || (bits & (bits - 1)) != 0 || bits > (int)PauseReason.WaitingForPeer)
                throw new ArgumentOutOfRangeException(nameof(reason), "Set exactly one owned pause reason.");
            pauses = paused ? pauses | reason : pauses & ~reason;
            if (State == null) return;
            if (IsAuthority) State.SetPaused(pauses != PauseReason.None);
            ApplyRunState();
            PublishModel();
        }

        // Writes the authoritative state into the viewer model; unchanged values do not notify.
        public void PublishModel()
        {
            if (State == null || !IsAuthority) return;
            // Injection runs before relay Awake; the first tick or a view's Start publishes instead.
            for (int i = 0; i < relays.Length; i++)
                if (relays[i] == null || relays[i].Config == null || relays[i].State == null) return;
            Model.SetRelayCount(relays.Length);
            for (int i = 0; i < relays.Length; i++) Model.Relays[i].Value = RelayView.From(relays[i]);
            double player = 0, enemy = 0;
            if (State.IsRunning) CurrentRates(out player, out enemy);
            Model.PlayerScore.Value = State.PlayerScore;
            Model.EnemyScore.Value = State.EnemyScore;
            Model.PlayerRate.Value = player;
            Model.EnemyRate.Value = enemy;
            Model.RemainingSeconds.Value = MatchModel.WholeSeconds(State.Clock.RemainingSeconds);
            Model.Paused.Value = State.Clock.IsPaused;
            Model.Forecast.Value = Forecast();
            if (lifecycle != null) lifecycle.PublishUnits(Model);
            Model.Outcome.Value = State.Outcome;
            Model.Commit();
        }

        private void ApplyRunState()
        {
            if (!IsAuthority)
            {
                if (commander != null) commander.enabled = pauses == PauseReason.None
                    && !Model.Paused.Value && Model.Outcome.Value == BattleOutcome.InProgress;
                return;
            }
            Time.timeScale = State.IsRunning ? runningTimeScale : 0;
            if (commander != null)
                commander.enabled = State.IsRunning;
        }

        public void Restart()
        {
            Time.timeScale = runningTimeScale;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnDestroy()
        {
            Time.timeScale = runningTimeScale;
            Model.Dispose();
        }
    }
}
