var b = UnityEngine.Object.FindAnyObjectByType<SectorLab.Battle.BattleController>();
var n = UnityEngine.Object.FindAnyObjectByType<SectorLab.Network.NetworkMatchBootstrap>();
var l = UnityEngine.Object.FindAnyObjectByType<SectorLab.Units.UnitLifecycle>();
if (b == null || b.State == null || n == null || l == null) return new { ready = false };
var c = UnityEngine.Object.FindAnyObjectByType<SectorLab.Player.UnitCommander>();
var v = UnityEngine.Object.FindAnyObjectByType<SectorLab.Battle.BattleResultView>();
var m = b.Model;
var units = new System.Collections.Generic.List<object>();
for (int i = 0; i < m.Units.Length; i++)
{
    var u = m.Units[i];
    var proxy = b.Units.Find(u.Id);
    bool visible = false, collider = false;
    if (proxy != null)
    {
        foreach (var r in proxy.GetComponentsInChildren<UnityEngine.Renderer>())
            if (!(r is UnityEngine.LineRenderer) && r.enabled) visible = true;
        foreach (var hit in proxy.GetComponentsInChildren<UnityEngine.Collider>())
            if (hit.enabled) collider = true;
    }
    units.Add(new { id = u.Id, team = u.Team.ToString(), life = u.Life.ToString(), hp = u.Health,
        maxHp = u.MaxHealth, generation = u.Generation, shots = u.Shots, respawn = u.RespawnSeconds,
        x = u.X, z = u.Z, visible, collider, proxyHp = proxy == null ? -1 : proxy.Health.Current,
        moving = proxy != null && proxy.Movement.IsMoving });
}
var relays = new System.Collections.Generic.List<object>();
for (int i = 0; i < m.Relays.Length; i++)
{
    var r = m.Relays[i].Value;
    var point = b.Relays[i];
    relays.Add(new { owner = r.Owner.ToString(), percent = r.Percent, r.Side, r.Contested,
        fill = point.CaptureRingFill, color = point.CaptureRingColor.ToString() });
}
var labels = new System.Collections.Generic.List<string>();
if (v != null && v.IsVisible)
    foreach (var label in v.GetComponentsInChildren<UnityEngine.UI.Text>()) labels.Add(label.text);
return new { ready = l.IsReady, version = UnityEngine.Application.version,
    buildGuid = UnityEngine.Application.buildGUID, role = n.Role.ToString(), configuration = n.Configuration.ToString(),
    match = m.MatchId, sequence = m.SnapshotSequence, tick = m.SimulationTick, elapsed = m.GameSeconds,
    remaining = m.RemainingSeconds.Value, synchronized = m.IsSynchronized, paused = m.Paused.Value,
    outcome = m.Outcome.Value.ToString(), perspective = b.Perspective.Outcome(m.Outcome.Value).ToString(),
    playerScore = m.PlayerScore.Value, enemyScore = m.EnemyScore.Value,
    commander = c.enabled, selected = c.Selected == null ? -1 : c.Selected.Id,
    accepted = n.Host == null ? -1 : n.Host.Accepted, rejected = n.Host == null ? -1 : n.Host.Rejected,
    peerReady = n.Host != null && n.Host.Ready, n.WaitingForPeer,
    failure = n.Client == null ? null : n.Client.Failure, units, relays, labels,
    action = v == null ? null : v.ActionLabel };
