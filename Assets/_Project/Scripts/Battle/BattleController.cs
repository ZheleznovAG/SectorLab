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
        public int PlayerUnitsAlive { get; private set; }
        public int EnemyUnitsAlive { get; private set; }
        public System.Collections.Generic.IReadOnlyList<RelayPoint> Relays => relays;

        public event Action<BattleOutcome> Finished;
        public event Action<double> Ticked;

        private double accumulatedSeconds;
        private float runningTimeScale;

        private void Awake()
        {
            runningTimeScale = Time.timeScale > 0 ? Time.timeScale : 1;
            if (State == null)
                State = new MatchState(new MatchClock());
        }

        // The composition root supplies the same state/clock to consumers before the first tick.
        [VContainer.Inject]
        public void Initialize(MatchState state)
        {
            if (State != null && State.Clock.ElapsedSeconds > 0)
                throw new InvalidOperationException("Cannot replace a running match.");
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void InitializeRelays(RelayPoint[] points) => relays = points ?? throw new ArgumentNullException(nameof(points));

        private void Update()
        {
            CountLivingUnits();
            if (!State.IsRunning)
                return;
            accumulatedSeconds += Time.deltaTime;
            while (accumulatedSeconds >= TickSeconds && State.IsRunning)
            {
                accumulatedSeconds -= TickSeconds;
                AdvanceSimulation(TickSeconds);
            }
        }

        private void CountLivingUnits()
        {
            int players = 0;
            int enemies = 0;
            // Index loop: foreach over an IReadOnlyList boxes its enumerator and allocates every frame.
            for (int i = 0; i < Unit.All.Count; i++)
            {
                Unit unit = Unit.All[i];
                if (!unit.IsAlive)
                    continue;
                if (unit.Team == Team.Player)
                    players++;
                else
                    enemies++;
            }

            PlayerUnitsAlive = players;
            EnemyUnitsAlive = enemies;

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
                resultView.Show(Outcome, Restart, State.PlayerScore, State.EnemyScore);
            Finished?.Invoke(Outcome);
        }

        public void SetPaused(bool paused)
        {
            State.SetPaused(paused);
            ApplyRunState();
        }

        private void ApplyRunState()
        {
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
        }
    }
}
