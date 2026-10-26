// Run with unity command eval_file after Main is ready in client mode.
// Only runtime objects are touched; scene/assets are not saved.
var battle = UnityEngine.Object.FindAnyObjectByType<SectorLab.Battle.BattleController>();
var lifecycle = UnityEngine.Object.FindAnyObjectByType<SectorLab.Units.UnitLifecycle>();
var commander = UnityEngine.Object.FindAnyObjectByType<SectorLab.Player.UnitCommander>();
var hud = UnityEngine.Object.FindAnyObjectByType<SectorLab.Presentation.BattleHud>();
var audio = UnityEngine.Object.FindAnyObjectByType<SectorLab.Presentation.MatchAudio>();
var hostEnd = (SectorLab.Network.LoopbackChannel)System.AppDomain.CurrentDomain.GetData("SectorLabAuditHost");
var report = new System.Collections.Generic.Dictionary<string, object>();
report["clientRole"] = SectorLab.Network.NetworkLaunch.Role.ToString();
report["lifecycleReady"] = lifecycle.IsReady;
report["authority"] = battle.IsAuthority;

using (var model = new SectorLab.Battle.MatchModel())
{
    model.SetRelayCount(3);
    model.Relays[1].Value = new SectorLab.Battle.RelayView(SectorLab.Objectives.RelayOwner.Enemy, 100, 2, false, true);
    var match = new byte[1 + SectorLab.Network.MatchSnapshot.SizeFor(3)];
    match[0] = (byte)SectorLab.Network.MessageType.MatchState;
    SectorLab.Network.MatchSnapshot.Write(model, match.AsSpan(1));
    hostEnd.Send(match);
    var units = new byte[1 + SectorLab.Network.UnitsSnapshot.SizeFor(1)];
    units[0] = (byte)SectorLab.Network.MessageType.Units;
    SectorLab.Network.UnitsSnapshot.Write(new[] { lifecycle.Slots[4].Unit }, units.AsSpan(1));
    hostEnd.Send(units);
    hostEnd.Flush();
    hud.Refresh();
    var absent = lifecycle.Slots[3].Unit;
    var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    var buttons = (UnityEngine.UI.Button[])typeof(SectorLab.Presentation.BattleHud).GetField("unitButtons", fields).GetValue(hud);
    var labels = (UnityEngine.UI.Text[])typeof(SectorLab.Presentation.BattleHud).GetField("unitLabels", fields).GetValue(hud);
    report["modelRelayOwner"] = battle.Model.Relays[1].Value.Owner.ToString();
    report["sceneRelayOwner"] = battle.Relays[1].State.Owner.ToString();
    report["sceneRelayRingFill"] = battle.Relays[1].CaptureRingFill;
    report["absentUnitRendererEnabled"] = absent.GetComponentInChildren<UnityEngine.Renderer>().enabled;
    report["absentUnitIsAlive"] = absent.IsAlive;
    report["absentUnitButtonEnabled"] = buttons[0].interactable;
    report["absentUnitCard"] = labels[0].text;
    report["absentUnitSelectable"] = commander.SelectFriendly(absent);
    var playedBefore = audio.PlayedResults;
    model.Outcome.Value = SectorLab.Battle.BattleOutcome.Defeat;
    SectorLab.Network.MatchSnapshot.Write(model, match.AsSpan(1));
    hostEnd.Send(match);
    hostEnd.Flush();
    report["clientOutcome"] = battle.Model.Outcome.Value.ToString();
    report["resultSoundDelta"] = audio.PlayedResults - playedBefore;
}

SectorLab.Network.LoopbackChannel.CreatePair(out var sessionHost, out var sessionClient);
using (var model = new SectorLab.Battle.MatchModel())
using (var remote = new SectorLab.Network.ClientSession(model, sessionClient, SectorLab.Units.Team.Enemy))
{
    var oldCommands = new SectorLab.Battle.MatchCommands(battle.Relays, new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock()));
    using (var oldSession = new SectorLab.Network.HostSession(oldCommands, model, sessionHost, SectorLab.Units.Team.Enemy))
        remote.Send(SectorLab.Battle.UnitCommand.Attack(SectorLab.Units.Team.Enemy, lifecycle.Slots[4].Unit.Id, lifecycle.Slots[0].Unit.Id));
    var newCommands = new SectorLab.Battle.MatchCommands(battle.Relays, new SectorLab.Battle.MatchState(new SectorLab.Battle.MatchClock()));
    using (var newSession = new SectorLab.Network.HostSession(newCommands, model, sessionHost, SectorLab.Units.Team.Enemy))
    {
        sessionClient.Flush();
        report["oldSessionCommandAcceptedByNewSession"] = newSession.Accepted;
    }
}

using (var disconnected = SectorLab.Network.UtpChannel.Connect("127.0.0.1", 47999))
using (var model = new SectorLab.Battle.MatchModel())
using (var remote = new SectorLab.Network.ClientSession(model, disconnected, SectorLab.Units.Team.Enemy))
{
    var sink = new SectorLab.Network.ClientCommandSink(remote, battle.Relays);
    report["disconnectedSubmitAccepted"] = sink.Submit(SectorLab.Battle.UnitCommand.MoveToRelay(SectorLab.Units.Team.Enemy, 4, 1), out var destination);
    report["disconnectedDroppedSends"] = disconnected.DroppedSends;
}
return report;
