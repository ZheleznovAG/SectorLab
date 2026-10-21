using System;
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
    // Host: the real match runs here, the remote human replaces the red AI, state goes out every tick.
    // Client: no simulation; units are proxies placed by the host's snapshots, orders go to the host.
    public sealed class NetworkMatchBootstrap : MonoBehaviour
    {
        private BattleController battle;
        private UnitLifecycle lifecycle;
        private UnitCommander commander;
        private HostSession host;
        private ClientSession client;
        private bool proxiesReady;
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
            if (Role == SessionRole.Host) StartHost(channel);
            else StartClient(channel);
        }

        private void StartHost(IMessageChannel channel)
        {
            host = new HostSession(battle.Commands, battle.Model, channel, NetworkLaunch.RemoteTeam);
            battle.Ticked += SendState;
            battle.Finished += SendFinalState;
        }

        private void SendState(double seconds) => host.SendState(Unit.All);
        private void SendFinalState(BattleOutcome outcome) => host.SendState(Unit.All);

        private void StartClient(IMessageChannel channel)
        {
            battle.enabled = false; // the host owns time, scores, combat and respawns
            client = new ClientSession(battle.Model, channel, NetworkLaunch.RemoteTeam);
            commander.SetLocalTeam(NetworkLaunch.RemoteTeam);
            commander.Bind(new ClientCommandSink(client, battle.Relays));
            var hud = FindAnyObjectByType<BattleHud>();
            if (hud != null) hud.Rebind();
            client.UnitsUpdated += ApplyUnits;
            lifecycle.Ready += MakeProxies;
            if (lifecycle.IsReady) MakeProxies();
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
                if (proxyParts.TryGetValue(unit, out var parts)) SetVisible(parts.renderers, parts.colliders, present);
            }
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
            if (lifecycle != null) lifecycle.Ready -= MakeProxies;
            if (client != null) { client.UnitsUpdated -= ApplyUnits; client.Dispose(); }
            host?.Dispose();
        }
    }
}
