using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SectorLab.Battle;
using SectorLab.Infrastructure;
using UnityEngine;
using VContainer;

namespace SectorLab.Units
{
    public enum UnitLifecyclePhase { Idle, Loading, Ready, Failed, Closed }

    public sealed class UnitSlotState
    {
        public UnitSpawnSlot Config { get; }
        public Unit Unit { get; internal set; }
        public RespawnTimer Respawn { get; } = new RespawnTimer();
        public bool IsSpawning { get; internal set; }
        public float SpawnRadius { get; internal set; }
        public UnitSlotState(UnitSpawnSlot config) => Config = config;
    }

    public sealed class UnitLifecycle : MonoBehaviour
    {
        [SerializeField] private Transform unitsRoot;
        [SerializeField] private BattleResultView resultView;
        private BattleController battle;
        private UnitFactory factory;
        private UnitSlotState[] slots = Array.Empty<UnitSlotState>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool initialized;
        private bool subscribed;
        public UnitLifecyclePhase Phase { get; private set; }
        public bool IsReady => Phase == UnitLifecyclePhase.Ready;
        public string Failure { get; private set; }
        public System.Collections.Generic.IReadOnlyList<UnitSlotState> Slots => slots;
        public event Action Ready;
        public event Action<string> LoadingFailed;
        public event Action<Unit> Spawned;

        [Inject]
        public void Initialize(BattleController controller, UnitFactory unitFactory, UnitRoster roster)
        {
            if (initialized) throw new InvalidOperationException("Unit lifecycle cannot be injected twice.");
            roster.Validate();
            battle = controller;
            factory = unitFactory;
            slots = new UnitSlotState[roster.slots.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = new UnitSlotState(roster.slots[i]);
            initialized = true;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() { if (initialized) Subscribe(); }
        private void Subscribe()
        {
            if (subscribed) return;
            battle.Ticked += OnTick;
            battle.Finished += OnFinished;
            subscribed = true;
        }

        private void Start()
        {
            if (!initialized) throw new InvalidOperationException("Unit lifecycle requires GameLifetimeScope.");
            StartMatchAsync().Forget(Debug.LogException);
        }

        public async UniTask StartMatchAsync()
        {
            if (Phase != UnitLifecyclePhase.Idle) return;
            Phase = UnitLifecyclePhase.Loading;
            battle.SetPaused(true);
            resultView?.ShowLoading();
            try
            {
                if (unitsRoot == null) throw new InvalidOperationException("Unit root was not configured.");
                for (int i = 0; i < slots.Length; i++)
                {
                    UnitSlotState slot = slots[i];
                    slot.IsSpawning = true;
                    Unit unit = await factory.SpawnAsync(slot.Config.prefabKey, slot.Config.position, unitsRoot, lifetime.Token);
                    Attach(slot, unit);
                    slot.IsSpawning = false;
                }
                lifetime.Token.ThrowIfCancellationRequested();
                Phase = UnitLifecyclePhase.Ready;
                resultView?.Hide();
                battle.SetPaused(false);
                Ready?.Invoke();
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception error) { Fail(error); }
        }

        private void Attach(UnitSlotState slot, Unit unit)
        {
            if (unit.Team != slot.Config.team || unit.Stats == null || unit.Stats.name != slot.Config.role.ToString())
                throw new InvalidOperationException("Prefab team/role does not match its roster slot.");
            slot.Unit = unit;
            var agent = unit.GetComponent<UnityEngine.AI.NavMeshAgent>();
            slot.SpawnRadius = agent.radius * Mathf.Max(unit.transform.lossyScale.x, unit.transform.lossyScale.z);
            slot.Respawn.Complete();
            unit.Died += OnDied;
            Spawned?.Invoke(unit);
        }

        private void OnDied(Unit unit)
        {
            unit.Died -= OnDied;
            for (int i = 0; i < slots.Length; i++)
            {
                UnitSlotState slot = slots[i];
                if (slot.Unit != unit) continue;
                slot.Unit = null;
                if (Phase == UnitLifecyclePhase.Ready) slot.Respawn.Schedule();
                return;
            }
        }

        private void OnTick(double seconds)
        {
            if (Phase != UnitLifecyclePhase.Ready) return;
            for (int i = 0; i < slots.Length; i++)
            {
                UnitSlotState slot = slots[i];
                if (!slot.Respawn.Advance(seconds) || slot.IsSpawning) continue;
                if (!UnitSpawnPositions.TryFind(slot.Config.position, slot.SpawnRadius, out _)) continue;
                RespawnAsync(slot).Forget(Debug.LogException);
            }
        }

        private async UniTask RespawnAsync(UnitSlotState slot)
        {
            slot.IsSpawning = true;
            try
            {
                Unit unit = await factory.SpawnAsync(slot.Config.prefabKey, slot.Config.position, unitsRoot,
                    lifetime.Token, UnitSpawnReason.Respawn);
                lifetime.Token.ThrowIfCancellationRequested();
                Attach(slot, unit);
            }
            catch (UnitSpawnBlockedException) { /* Space changed during loading; defer to another game tick. */ }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception error) { Fail(error); }
            finally { slot.IsSpawning = false; }
        }

        private void Fail(Exception error)
        {
            if (lifetime.IsCancellationRequested) return;
            Failure = error.Message;
            Phase = UnitLifecyclePhase.Failed;
            lifetime.Cancel();
            UnsubscribeUnits();
            factory.Dispose();
            battle.SetPaused(true);
            resultView?.ShowLoadError(battle.Restart, ExitGame);
            LoadingFailed?.Invoke(Failure);
            Debug.LogWarning("Unit lifecycle failed: " + Failure);
        }

        private static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnFinished(BattleOutcome outcome)
        {
            Phase = UnitLifecyclePhase.Closed;
            lifetime.Cancel();
            factory.CancelPendingOperations();
            UnsubscribeUnits();
            for (int i = 0; i < slots.Length; i++) slots[i].Respawn.Complete();
        }

        private void UnsubscribeUnits()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].Unit != null) slots[i].Unit.Died -= OnDied;
        }

        private void OnDisable()
        {
            if (subscribed && battle != null)
            {
                battle.Ticked -= OnTick;
                battle.Finished -= OnFinished;
            }
            subscribed = false;
            lifetime.Cancel();
            UnsubscribeUnits();
            if (Phase != UnitLifecyclePhase.Failed) Phase = UnitLifecyclePhase.Closed;
        }

        private void OnDestroy() => lifetime.Dispose();
    }
}
