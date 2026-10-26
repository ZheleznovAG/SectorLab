using System;
using R3;
using SectorLab.AI;
using SectorLab.Battle;
using SectorLab.Player;
using SectorLab.Presentation;
using SectorLab.Units;
using UnityEngine;

namespace SectorLab.Network
{
    // Lifecycle boundary: load local content, negotiate a match, then stream complete frames.
    public sealed class NetworkMatchBootstrap : MonoBehaviour
    {
        private BattleController battle;
        private UnitLifecycle lifecycle;
        private UnitCommander commander;
        private HostSession host;
        private ClientSession client;
        private ClientUnitView proxies;
        private IMessageChannel channel;
        private IConnectionStatus status;
        private IDisposable outcomeSubscription;
        private float lastSentAt = float.NegativeInfinity, startedAt;
        private bool showingResult, showingConnectionNotice, hostLost, timedOut;
        public const float HeartbeatSeconds = 0.25f;
        public const float HandshakeTimeoutSeconds = 20;
        public SessionRole Role { get; private set; } = SessionRole.Offline;
        public HostSession Host => host;
        public ClientSession Client => client;
        public ulong Configuration { get; private set; }
        public bool WaitingForPeer { get; private set; }

        private void Start()
        {
            Role = NetworkLaunch.Role;
            if (Role == SessionRole.Offline) return;
            channel = NetworkLaunch.Channel ?? throw new InvalidOperationException("A network match needs a channel.");
            battle = FindAnyObjectByType<BattleController>();
            lifecycle = FindAnyObjectByType<UnitLifecycle>();
            commander = FindAnyObjectByType<UnitCommander>();
            var ai = FindAnyObjectByType<EnemyTacticsController>();
            if (ai != null) ai.enabled = false;
            status = channel as IConnectionStatus;
            startedAt = Time.unscaledTime;
            if (Role == SessionRole.Host)
            {
                WaitingForPeer = true;
                battle.SetPaused(PauseReason.WaitingForPeer, true);
                if (status != null) status.Disconnected += OnPeerDisconnected;
            }
            else
            {
                battle.Model.IsSynchronized = false;
                battle.BecomeRemoteView();
                commander.SetLocalTeam(NetworkLaunch.RemoteTeam);
                if (status != null) status.Disconnected += OnHostDisconnected;
            }
            lifecycle.Ready += StartSession;
            if (lifecycle.IsReady) StartSession();
        }

        private void StartSession()
        {
            Configuration = MatchConfiguration.Compute(battle, lifecycle);
            if (Role == SessionRole.Host)
            {
                host = new HostSession(battle.Commands, battle.Model, channel, NetworkLaunch.RemoteTeam,
                    NetworkLaunch.NextHostMatch(), Configuration);
                host.PeerReady += OnPeerReady;
                battle.Ticked += SendState;
                battle.Finished += SendFinalState;
                ShowWaiting();
                SendState();
            }
            else
            {
                client = new ClientSession(battle.Model, channel, NetworkLaunch.RemoteTeam, Configuration);
                commander.Bind(new ClientCommandSink(client, battle.Relays));
                proxies = new ClientUnitView(lifecycle, battle.Model);
                battle.Model.Updated += OnClientModel;
                client.MatchStarted += commander.ClearSelection;
                client.Failed += ShowNotice;
                client.SendFailed += OnSendFailed;
                var hud = FindAnyObjectByType<BattleHud>();
                if (hud != null) hud.Rebind();
                outcomeSubscription = battle.Model.Outcome.Subscribe(OnHostOutcome);
                ShowConnectionNotice();
            }
        }

        private void PlayOffline() { NetworkLauncher.Leave(); battle.Restart(); }
        private void ShowNotice(string message)
        {
            var view = FindAnyObjectByType<BattleResultView>();
            if (view != null) view.ShowMessage(message, "Играть с ИИ", PlayOffline);
        }
        private void ShowWaiting() => ShowNotice("Ожидание соперника…\nIP: " + NetworkLauncher.LocalAddressesText());

        private void Update()
        {
            if (host != null && (status == null || status.IsConnected)
                && Time.unscaledTime - lastSentAt >= HeartbeatSeconds) SendState();
            bool pending = host != null ? !host.Ready : client != null && !client.IsSynchronized;
            if (pending && !timedOut && Time.unscaledTime - startedAt >= HandshakeTimeoutSeconds)
            {
                timedOut = true;
                ShowNotice(client?.Failure ?? "Не удалось согласовать матч. Проверьте сеть и одинаковую версию игры.");
            }
        }

        private void OnPeerReady()
        {
            WaitingForPeer = false; timedOut = false;
            HideMessage();
            battle.SetPaused(PauseReason.WaitingForPeer, false);
        }

        private void OnPeerDisconnected()
        {
            host?.PeerDisconnected();
            if (battle.Outcome != BattleOutcome.InProgress) return;
            WaitingForPeer = true; startedAt = Time.unscaledTime; timedOut = false;
            battle.SetPaused(PauseReason.WaitingForPeer, true);
            ShowWaiting();
        }

        private void SendState() { host.SendState(); lastSentAt = Time.unscaledTime; }
        private void SendState(double seconds) => SendState();
        private void SendFinalState(BattleOutcome outcome) => SendState();

        private void ShowConnectionNotice()
        {
            if (client.IsSynchronized || showingResult) return;
            showingConnectionNotice = true;
            ShowNotice(hostLost ? "Связь с хостом потеряна." : "Подключение к хосту…");
        }

        private void OnClientModel()
        {
            proxies.Apply();
            commander.ValidateSelection();
            if (!client.IsSynchronized) return;
            hostLost = false; timedOut = false;
            if (showingConnectionNotice) { showingConnectionNotice = false; HideMessage(); }
        }

        private void HideMessage()
        {
            var view = FindAnyObjectByType<BattleResultView>();
            if (view != null && view.IsShowingMessage) view.Hide();
        }

        private void OnHostDisconnected()
        {
            hostLost = true; timedOut = false; startedAt = Time.unscaledTime;
            client?.Disconnect();
            commander.ClearSelection(); showingResult = false;
            if (client != null) ShowConnectionNotice();
        }

        private void OnSendFailed(SendResult result)
        {
            var audio = FindAnyObjectByType<MatchAudio>();
            if (audio != null) audio.ShowNetworkNotice(result == SendResult.Disconnected
                ? "Нет связи с хостом. Приказ не отправлен." : "Приказ не отправлен. Повторите его.");
        }

        private void OnHostOutcome(BattleOutcome outcome)
        {
            if (outcome != BattleOutcome.InProgress) ShowClientResult(outcome);
            else if (showingResult)
            {
                showingResult = false;
                var view = FindAnyObjectByType<BattleResultView>();
                if (view != null) view.Hide();
            }
        }

        private void ShowClientResult(BattleOutcome outcome)
        {
            if (showingResult) return;
            showingResult = true; showingConnectionNotice = false;
            commander.ClearSelection(); commander.enabled = false;
            var view = FindAnyObjectByType<BattleResultView>();
            if (view == null) return;
            bool localIsPlayer = NetworkLaunch.RemoteTeam == Team.Player;
            MatchModel model = battle.Model;
            view.Show(battle.Perspective.Outcome(outcome), () => ShowNotice("Новый бой начнёт хост."),
                localIsPlayer ? model.PlayerScore.Value : model.EnemyScore.Value,
                localIsPlayer ? model.EnemyScore.Value : model.PlayerScore.Value);
        }

        private void OnDestroy()
        {
            if (battle != null)
            {
                battle.Ticked -= SendState; battle.Finished -= SendFinalState;
                battle.Model.Updated -= OnClientModel;
            }
            if (lifecycle != null) lifecycle.Ready -= StartSession;
            if (status != null)
            {
                status.Disconnected -= OnPeerDisconnected;
                status.Disconnected -= OnHostDisconnected;
            }
            client?.Dispose(); host?.Dispose(); outcomeSubscription?.Dispose();
        }
    }
}
