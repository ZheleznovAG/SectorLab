using System;
using R3;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Player;
using SectorLab.Presentation;
using SectorLab.Units;
using UnityEngine;
using UnityEngine.AI;
using Unit = SectorLab.Units.Unit;

namespace SectorLab.Network
{
    // Turns Main into a host or a client according to NetworkLaunch; offline it does nothing.
    // Host: the real match runs here, the remote human replaces the red AI, state goes out every tick
    // and, while the clock stands (pause, waiting, result), as a heartbeat: snapshots travel unreliably,
    // so a single lost "paused" or "finished" packet must not leave the client behind.
    // Client: no simulation; units are proxies placed by the host's snapshots, orders go to the host.
    // Every blocking notice offers "Играть с ИИ", so a lost connection never strands the player.
    public sealed class NetworkMatchBootstrap : MonoBehaviour
    {
        private BattleController battle;
        private UnitLifecycle lifecycle;
        private UnitCommander commander;
        private HostSession host;
        private ClientSession client;
        private bool proxiesReady;
        private float lastSentAt = float.NegativeInfinity;
        public const float HeartbeatSeconds = 0.25f;
        private readonly System.Collections.Generic.Dictionary<Unit, (Renderer[] renderers, Collider[] colliders)> proxyParts =
            new System.Collections.Generic.Dictionary<Unit, (Renderer[], Collider[])>(6);

        public SessionRole Role { get; private set; } = SessionRole.Offline;
        public HostSession Host => host;
        public ClientSession Client => client;

        private void Start()
        {
            Role = NetworkLaunch.Role;
            if (Role == SessionRole.Offline) return;
            IMessageChannel channel = NetworkLaunch.Channel ?? throw new InvalidOperationException("A network match needs a channel.");
            battle = FindAnyObjectByType<BattleController>();
            lifecycle = FindAnyObjectByType<UnitLifecycle>();
            commander = FindAnyObjectByType<UnitCommander>();
            var ai = FindAnyObjectByType<EnemyTacticsController>();
            if (ai != null) ai.enabled = false; // red is a human now
            status = channel as IConnectionStatus;
            if (Role == SessionRole.Host) StartHost(channel);
            else StartClient(channel);
        }

        private void PlayOffline()
        {
            NetworkLauncher.Leave();
            battle.Restart();
        }

        private void ShowNotice(string message)
        {
            var view = FindAnyObjectByType<BattleResultView>();
            if (view != null) view.ShowMessage(message, "Играть с ИИ", PlayOffline);
        }

        private void StartHost(IMessageChannel channel)
        {
            host = new HostSession(battle.Commands, battle.Model, channel, NetworkLaunch.RemoteTeam);
            battle.Ticked += SendState;
            battle.Finished += SendFinalState;
            // The clock waits for the opponent; a dropped opponent pauses the match.
            if (status == null) return;
            status.Connected += OnPeerConnected;
            status.Disconnected += OnPeerDisconnected;
            lifecycle.Ready += HoldUntilConnected;
            if (lifecycle.IsReady) HoldUntilConnected();
        }

        private IConnectionStatus status;
        public bool WaitingForPeer { get; private set; }

        private void HoldUntilConnected()
        {
            if (status.IsConnected) return;
            WaitingForPeer = true;
            battle.SetPaused(PauseReason.WaitingForPeer, true);
            ShowWaiting();
        }

        private void ShowWaiting() => ShowNotice("Ожидание соперника…\nIP: " + NetworkLauncher.LocalAddressesText());

        private void Update()
        {
            if (host == null) return;
            // Ticks stop while the clock stands; keep the client's picture current anyway.
            bool reachable = status == null || status.IsConnected;
            if (reachable && Time.unscaledTime - lastSentAt >= HeartbeatSeconds) SendState();
        }

        private void OnPeerConnected()
        {
            if (!WaitingForPeer) return;
            WaitingForPeer = false;
            var view = FindAnyObjectByType<BattleResultView>();
            if (view != null && view.IsShowingMessage) view.Hide();
            battle.SetPaused(PauseReason.WaitingForPeer, false);
            SendState();
        }

        private void OnPeerDisconnected()
        {
            if (battle.Outcome != BattleOutcome.InProgress) return;
            WaitingForPeer = true;
            battle.SetPaused(PauseReason.WaitingForPeer, true);
            ShowWaiting();
        }

        private void SendState()
        {
            host.SendState(Unit.All);
            lastSentAt = Time.unscaledTime;
        }

        private void SendState(double seconds) => SendState();
        private void SendFinalState(BattleOutcome outcome) => SendState();

        private void StartClient(IMessageChannel channel)
        {
            battle.BecomeRemoteView(); // the host owns time, scores, combat and respawns
            client = new ClientSession(battle.Model, channel, NetworkLaunch.RemoteTeam);
            commander.SetLocalTeam(NetworkLaunch.RemoteTeam);
            commander.Bind(new ClientCommandSink(client, battle.Relays));
            var hud = FindAnyObjectByType<BattleHud>();
            if (hud != null) hud.Rebind();
            client.UnitsUpdated += ApplyUnits;
            lifecycle.Ready += MakeProxies;
            if (lifecycle.IsReady) MakeProxies();
            // The host decides the match and when the next one starts; the client follows.
            outcomeSubscription = battle.Model.Outcome.Subscribe(OnHostOutcome);
            if (status == null) return;
            status.Connected += OnHostConnected;
            status.Disconnected += OnHostDisconnected;
            // Loading hides the result panel when it ends, so the notice goes up after it.
            lifecycle.Ready += ShowConnectionNotice;
            if (lifecycle.IsReady) ShowConnectionNotice();
        }

        private IDisposable outcomeSubscription;
        private bool showingResult, showingConnectionNotice, hostLost;

        private void ShowConnectionNotice()
        {
            if (status.IsConnected || showingResult) return;
            showingConnectionNotice = true;
            ShowNotice(hostLost ? "Связь с хостом потеряна." : "Подключение к хосту…");
        }

        private void OnHostConnected()
        {
            hostLost = false;
            if (!showingConnectionNotice) return;
            showingConnectionNotice = false;
            var view = FindAnyObjectByType<BattleResultView>();
            if (view != null && view.IsShowingMessage) view.Hide();
        }

        private void OnHostDisconnected()
        {
            hostLost = true;
            commander.ClearSelection();
            showingResult = false;
            ShowConnectionNotice();
        }

        private void OnHostOutcome(BattleOutcome outcome)
        {
            if (outcome != BattleOutcome.InProgress) ShowClientResult(outcome);
            else if (showingResult) HideClientResult(); // the host started the next match
        }

        private void ShowClientResult(BattleOutcome outcome)
        {
            if (showingResult) return;
            showingResult = true;
            showingConnectionNotice = false;
            commander.ClearSelection();
            commander.enabled = false;
            var view = FindAnyObjectByType<BattleResultView>();
            if (view == null) return;
            bool localIsPlayer = NetworkLaunch.RemoteTeam == Team.Player;
            MatchModel model = battle.Model;
            // Only the host restarts; this side waits and follows into the next match by itself.
            view.Show(battle.Perspective.Outcome(outcome), () => ShowNotice("Новый бой начнёт хост."),
                localIsPlayer ? model.PlayerScore.Value : model.EnemyScore.Value,
                localIsPlayer ? model.EnemyScore.Value : model.PlayerScore.Value);
        }

        private void HideClientResult()
        {
            showingResult = false;
            var view = FindAnyObjectByType<BattleResultView>();
            if (view != null) view.Hide();
            commander.enabled = true;
        }

        private void MakeProxies()
        {
            foreach (UnitSlotState slot in lifecycle.Slots)
            {
                Unit unit = slot.Unit;
                if (unit == null) continue;
                var agent = unit.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;
                unit.Movement.enabled = false;
                if (unit.Attack != null) unit.Attack.enabled = false;
                proxyParts[unit] = (unit.GetComponentsInChildren<Renderer>(true), unit.GetComponentsInChildren<Collider>(true));
            }
            proxiesReady = true;
            ApplyUnits();
        }

        private void ApplyUnits()
        {
            if (!proxiesReady) return;
            ReadOnlySpan<UnitState> states = client.Units;
            battle.Model.SetUnitCount(lifecycle.Slots.Count);
            int index = 0;
            foreach (UnitSlotState slot in lifecycle.Slots)
            {
                Unit unit = slot.Unit;
                if (unit == null) continue;
                bool present = false;
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i].Id != unit.Id) continue;
                    present = true;
                    Vector3 position = unit.transform.position;
                    unit.transform.position = new Vector3(states[i].X, position.y, states[i].Z);
                    unit.Health.SetReplicated(states[i].Health);
                    break;
                }
                // Dead or waiting to respawn on the host. Deactivation would drop it from Unit.All.
                if (!present) unit.Health.SetReplicated(0);
                battle.Model.SetUnit(index++, new UnitView(unit.Id, unit.Team, unit.Stats.role,
                    present ? UnitLife.Alive : UnitLife.Respawning, unit.Health.Current, unit.Health.Max,
                    0, slot.Generation, 0, unit.transform.position.x, unit.transform.position.z));
                if (proxyParts.TryGetValue(unit, out var parts)) SetVisible(parts.renderers, parts.colliders, present);
            }
            battle.Model.Commit();
        }

        private static void SetVisible(Renderer[] renderers, Collider[] colliders, bool visible)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].enabled != visible) renderers[i].enabled = visible;
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null && colliders[i].enabled != visible) colliders[i].enabled = visible;
        }

        private void OnDestroy()
        {
            if (battle != null) { battle.Ticked -= SendState; battle.Finished -= SendFinalState; }
            if (lifecycle != null)
            {
                lifecycle.Ready -= MakeProxies;
                lifecycle.Ready -= HoldUntilConnected;
                lifecycle.Ready -= ShowConnectionNotice;
            }
            if (status != null)
            {
                status.Connected -= OnPeerConnected;
                status.Disconnected -= OnPeerDisconnected;
                status.Connected -= OnHostConnected;
                status.Disconnected -= OnHostDisconnected;
            }
            if (client != null) { client.UnitsUpdated -= ApplyUnits; client.Dispose(); }
            host?.Dispose();
            outcomeSubscription?.Dispose();
        }
    }
}
