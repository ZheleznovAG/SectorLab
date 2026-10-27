"""Two real Windows players over UDP; local QA Pipeline controls and observes the scenario."""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import time

from .common import ROOT, ToolError, head, require_program, run, say, sha256_file, utc_now, write_evidence

_BATTLE = "var b = UnityEngine.Object.FindAnyObjectByType<SectorLab.Battle.BattleController>();\n"
_NETWORK = "var n = UnityEngine.Object.FindAnyObjectByType<SectorLab.Network.NetworkMatchBootstrap>();\n"
_COMMANDER = "var c = UnityEngine.Object.FindAnyObjectByType<SectorLab.Player.UnitCommander>();\n"
_DISMISS = """
var t = UnityEngine.Object.FindAnyObjectByType<SectorLab.Presentation.TutorialView>();
string key = SectorLab.Presentation.TutorialView.SeenPreference;
bool existed = UnityEngine.PlayerPrefs.HasKey(key);
int previous = UnityEngine.PlayerPrefs.GetInt(key, 0);
t.Dismiss();
if (existed) UnityEngine.PlayerPrefs.SetInt(key, previous); else UnityEngine.PlayerPrefs.DeleteKey(key);
UnityEngine.PlayerPrefs.Save();
"""


def verify(task: str, build_task: str) -> None:
    if os.name != "nt":
        raise ToolError("This acceptance scenario launches Windows players; run it on Windows.")
    report_path = ROOT / "docs" / "evidence" / f"network-{task}.json"
    build_report = ROOT / "docs" / "evidence" / f"windows-build-{build_task}.json"
    folder = ROOT / "Builds" / "Windows" / build_task
    scratch = ROOT / "Temp" / "network-acceptance" / task
    if report_path.exists() or scratch.exists():
        raise ToolError("Use a new task name; earlier acceptance evidence is preserved.")
    build = json.loads(build_report.read_text(encoding="utf-8"))
    if not build.get("qa") or build.get("result") != "Succeeded":
        raise ToolError("Build a Windows QA player first with build-windows --qa.")
    # Validate the complete build before copying; executable-only hashes miss managed gameplay code.
    for item in build["files"]:
        if sha256_file(folder / item["path"]) != item["sha256"]:
            raise ToolError(f"Build changed since its report: {item['path']}")
    scratch.mkdir(parents=True)
    unity = require_program("unity", "Install the Unity CLI.")
    processes = {}
    locations = {}
    record = {"task": task, "sourceHead": head(), "startedAtUtc": utc_now(),
              "buildReport": build_report.relative_to(ROOT).as_posix(), "buildReportSha256": sha256_file(build_report),
              "transport": "Unity Transport UDP over 127.0.0.1, two Windows processes",
              "scenario": "Production commands and lifecycle, with host-controlled damage, placement and game ticks",
              "steps": [], "passed": False}

    def evaluate(role: str, code: str):
        script = scratch / f"{role}-command.cs"
        # Runtime eval's default references omit custom asmdefs. Compile the probe with
        # explicit references through Pipeline's compiler, without changing the game.
        source = "public static class NetworkAcceptanceProbe { public static object Run() {\n" + code + "\n} }"
        wrapper = """
var request = new Unity.Pipeline.Compilation.CompilationRequest {
        SourceCode = SOURCE,
        AssemblyName = "NetworkAcceptance_" + System.Guid.NewGuid().ToString("N"),
        AdditionalAssemblyPrefixes = new string[] { "SectorLab", "R3", "UniTask" }
    };
var compiler = typeof(Unity.Pipeline.Compilation.CompilationRequest).Assembly.GetType(
    "Unity.Pipeline.Compilation.RoslynCompilationService");
var compiled = (Unity.Pipeline.Compilation.CompilationResult)compiler.GetMethod("Compile",
    new System.Type[] { typeof(Unity.Pipeline.Compilation.CompilationRequest) }).Invoke(null, new object[] { request });
if (!compiled.Success) throw new System.InvalidOperationException(
    string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
return compiled.Assembly.GetType("NetworkAcceptanceProbe").GetMethod("Run").Invoke(null, null);
""".replace("SOURCE", json.dumps(source, ensure_ascii=False))
        script.write_text(wrapper, encoding="utf-8")
        response = json.loads(run([unity, "command", "--json", "--no-banner", "--runtime-path",
                                   str(locations[role]),
                                   "eval_file", "--file", str(script)], timeout=45).stdout)
        data = response.get("data") or {}
        result = data.get("result") or {}
        if not response.get("success") or not data.get("success") or not result.get("success"):
            raise ToolError(f"{role} eval failed: {result}")
        return result.get("result")

    probe = (ROOT / "Tools" / "scenarios" / "network-probe.cs").read_text(encoding="utf-8")

    def snapshot(role: str) -> dict:
        return evaluate(role, probe)

    def await_state(role, predicate, seconds=30):
        deadline = time.monotonic() + seconds
        last = None
        while time.monotonic() < deadline:
            last = snapshot(role)
            if predicate(last):
                return last
            time.sleep(0.5)
        raise ToolError(f"{role} state timeout: {last}")

    def check(name, passed, **details):
        record["steps"].append({"name": name, "passed": bool(passed), "atUtc": utc_now(), **details})
        say(f"{name}: {'PASS' if passed else 'FAIL'}")
        if not passed:
            raise ToolError(f"Network acceptance failed: {name}")

    def pair(name):
        h = snapshot("host")
        c = await_state("client", lambda s: s.get("match") == h["match"] and s.get("tick") == h["tick"])
        fields = ("match", "tick", "remaining", "paused", "outcome")
        same = all(h[k] == c[k] for k in fields)
        same &= all(abs(h[k] - c[k]) < 0.02 for k in ("elapsed", "playerScore", "enemyScore"))
        same &= len(h["units"]) == len(c["units"]) == 6
        same &= all(all(a[k] == b[k] for k in ("id", "life", "hp", "generation"))
                    for a, b in zip(h["units"], c["units"]))
        same &= h["relays"] == c["relays"]
        check(name, same, host=h, client=c)
        return h, c

    try:
        for role in ("host", "client"):
            target = scratch / role
            shutil.copytree(folder, target)
            locations[role] = target
            startup = subprocess.STARTUPINFO()
            startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            startup.wShowWindow = 0
            args = [str(target / "SectorLab.exe"), "-batchmode", "-screen-fullscreen", "0",
                    "-screen-width", "960", "-screen-height", "540", "-logFile", str(scratch / f"{role}.log")]
            args += ["-sectorlab-host", "17777"] if role == "host" else ["-sectorlab-join", "127.0.0.1", "17777"]
            processes[role] = subprocess.Popen(args, cwd=target, startupinfo=startup,
                                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        for role in ("host", "client"):
            deadline = time.monotonic() + 60
            while not (locations[role] / ".unity-pipeline-runtime-port").exists():
                if processes[role].poll() is not None or time.monotonic() > deadline:
                    raise ToolError(f"{role} QA endpoint did not start; inspect {role}.log in Temp.")
                time.sleep(0.5)
            await_state(role, lambda s: s.get("ready"))
            evaluate(role, _DISMISS + "return true;")
        await_state("host", lambda s: s.get("peerReady"))
        await_state("client", lambda s: s.get("synchronized"))
        evaluate("host", _BATTLE + "b.enabled = false; b.SetPaused(true); return true;")
        h, c = pair("handshake and complete paused snapshot")
        check("compatible builds and configuration", h["configuration"] == c["configuration"]
              and h["version"] == c["version"] and h["buildGuid"] == c["buildGuid"])

        evaluate("host", _BATTLE + "b.SetPaused(false); return true;")
        await_state("client", lambda s: s.get("commander"))
        sent = evaluate("client", _BATTLE + _COMMANDER +
                        "return c.SelectFriendly(b.Units.Find(4)) && c.OrderSelectedToRelay(b.Relays[0]);")
        h = await_state("host", lambda s: s.get("accepted", 0) >= 1)
        check("client command reaches authoritative host", sent and h["units"][4]["moving"], host=h)
        # Freeze after damage so the client must show a missing slot and clear its selection.
        evaluate("host", _BATTLE + "b.Units.Find(4).Health.TakeDamage(10000); b.SetPaused(true); return true;")
        h, c = pair("death and respawn timer replicate")
        dead = c["units"][4]
        check("dead client proxy is hidden and unselectable", dead["life"] == "Respawning"
              and dead["hp"] == 0 and not dead["visible"] and not dead["collider"] and c["selected"] == -1)
        generation = dead["generation"]
        evaluate("host", _BATTLE + "b.SetPaused(false); b.AdvanceSimulation(10.2); return true;")
        await_state("host", lambda s: s["units"][4]["life"] == "Alive")
        evaluate("host", _BATTLE + "b.SetPaused(true); return true;")
        h, c = pair("respawn generation and health replicate")
        alive = c["units"][4]
        check("respawned client proxy is restored", alive["generation"] == generation + 1
              and alive["hp"] == alive["maxHp"] and alive["proxyHp"] == alive["hp"]
              and alive["visible"] and alive["collider"])

        # Controlled placement uses the real NavMesh agents, relay simulation and score rules.
        evaluate("host", _BATTLE + """
var life = UnityEngine.Object.FindAnyObjectByType<SectorLab.Units.UnitLifecycle>();
foreach (var slot in life.Slots) if (slot.Unit != null) {
    slot.Unit.Movement.Stop();
    slot.Unit.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(slot.Config.position);
}
for (int i = 0; i < 2; i++)
    b.Units.Find(3 + i).GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(b.Relays[i].transform.position);
b.SetPaused(false); b.AdvanceSimulation(6); b.SetPaused(true); return true;
""")
        h, c = pair("captured relay visuals and scores replicate")
        check("client shows both enemy-owned score relays", all(r["owner"] == "Enemy" for r in c["relays"][:2]))
        evaluate("host", _BATTLE + "b.SetPaused(false); b.AdvanceSimulation(50); return true;")
        h, c = pair("final result replicates")
        check("opposite result perspectives", h["perspective"] == "Defeat" and c["perspective"] == "Victory"
              and any("Поражение" in x for x in h["labels"]) and any("Победа" in x for x in c["labels"]))
        old_match = h["match"]
        evaluate("host", _BATTLE + "b.Restart(); return true;")
        await_state("host", lambda s: s.get("ready") and s.get("match", 0) > old_match)
        evaluate("host", _BATTLE + "b.enabled = false; b.SetPaused(true);" + _DISMISS + "return true;")
        await_state("client", lambda s: s.get("synchronized") and s.get("match", 0) > old_match)
        h, c = pair("new host match resets existing client")
        check("new match clears result and score", c["outcome"] == "InProgress"
              and c["playerScore"] == c["enemyScore"] == 0 and c["selected"] == -1)
        evaluate("host", "SectorLab.Network.NetworkLauncher.Leave(); return true;")
        c = await_state("client", lambda s: not s.get("synchronized") and not s.get("commander"))
        check("disconnect blocks input and offers offline play", c["selected"] == -1
              and c["action"] == "Играть с ИИ", client=c)
        sent = evaluate("client", _NETWORK +
                        "return n.Client.Send(SectorLab.Battle.UnitCommand.Stop(SectorLab.Units.Team.Enemy, 4)).ToString();")
        check("disconnected command reports failure", sent == "Disconnected", sendResult=sent)
        record["passed"] = True
    except Exception as error:
        record["error"] = str(error)
        raise
    finally:
        for role, process in processes.items():
            if process.poll() is None:
                try:
                    evaluate(role, "UnityEngine.Application.Quit(); return true;")
                    process.wait(timeout=10)
                except Exception:
                    process.terminate()
                    process.wait(timeout=10)
        record["completedAtUtc"] = utc_now()
        write_evidence(report_path.name, record)
