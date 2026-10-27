"""Capture a real offline match directly from a disposable Windows QA player."""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import time

from .common import ROOT, ToolError, head, require_program, run, say, sha256_file, utc_now, write_evidence


def record_demo(task: str, build_task: str, max_seconds: int = 190) -> None:
    if os.name != "nt":
        raise ToolError("The demo recorder uses the Windows QA player.")
    if not 3 <= max_seconds <= 190:
        raise ToolError("--max-seconds must be between 3 and 190.")
    scratch = ROOT / "Temp" / "media" / task
    if scratch.exists():
        raise ToolError("Use a new task name to preserve the earlier recording.")
    build_report = ROOT / "docs" / "evidence" / f"windows-build-{build_task}.json"
    build = json.loads(build_report.read_text(encoding="utf-8"))
    if not build.get("qa") or build.get("result") != "Succeeded":
        raise ToolError("A successful Windows QA build is required.")
    source = ROOT / "Builds" / "Windows" / build_task
    for item in build["files"]:
        if sha256_file(source / item["path"]) != item["sha256"]:
            raise ToolError(f"Build changed: {item['path']}")
    ffmpeg = require_program("ffmpeg", "Install FFmpeg with the libx264 encoder.")
    unity = require_program("unity", "Install Unity CLI.")
    scratch.mkdir(parents=True)
    player = scratch / "player"
    shutil.copytree(source, player, ignore=shutil.ignore_patterns(".unity-pipeline-runtime-port"))
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = 0
    process = subprocess.Popen([str(player / "SectorLab.exe"), "-screen-fullscreen", "0", "-popupwindow",
                                "-screen-width", "1280", "-screen-height", "720", "-logFile", str(scratch / "player.log")],
                               cwd=player, startupinfo=startup, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    def evaluate(path):
        response = json.loads(run([unity, "command", "--json", "--no-banner", "--runtime-path", str(player),
                                   "eval_file", "--file", str(path)], timeout=60).stdout)
        data = response.get("data") or {}
        result = data.get("result") or {}
        if not response.get("success") or not data.get("success") or not result.get("success"):
            raise ToolError(f"Recorder setup failed: {result}")
        return result.get("result")

    started = utc_now()
    try:
        deadline = time.monotonic() + 60
        while not (player / ".unity-pipeline-runtime-port").exists():
            if process.poll() is not None or time.monotonic() > deadline:
                raise ToolError("QA player did not start; inspect its log in Temp/media.")
            time.sleep(0.5)
        code_path = ROOT / "Tools" / "scenarios" / "RecordGameplay.cs"
        code = code_path.read_text(encoding="utf-8")
        script = """
System.Reflection.Assembly.Load("Unity.Collections");
var request = new Unity.Pipeline.Compilation.CompilationRequest {
    SourceCode = SOURCE,
    AssemblyName = "SectorLabDemo_" + System.Guid.NewGuid().ToString("N"),
    AdditionalAssemblyPrefixes = new string[] { "SectorLab", "R3", "UniTask", "Unity.", "System" }
};
var compiler = typeof(Unity.Pipeline.Compilation.CompilationRequest).Assembly.GetType(
    "Unity.Pipeline.Compilation.RoslynCompilationService");
var compiled = (Unity.Pipeline.Compilation.CompilationResult)compiler.GetMethod("Compile",
    new System.Type[] { typeof(Unity.Pipeline.Compilation.CompilationRequest) }).Invoke(null, new object[] { request });
if (!compiled.Success) throw new System.InvalidOperationException(string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
var type = compiled.Assembly.GetType("SectorLabDemoRecorder");
type.GetField("Folder").SetValue(null, FOLDER);
type.GetField("Encoder").SetValue(null, ENCODER);
type.GetField("LimitSeconds").SetValue(null, LIMIT);
return type.GetMethod("Launch").Invoke(null, null);
"""
        script = script.replace("SOURCE", json.dumps(code, ensure_ascii=False))
        script = script.replace("FOLDER", json.dumps(str(scratch)))
        script = script.replace("ENCODER", json.dumps(ffmpeg)).replace("LIMIT", str(max_seconds))
        setup = scratch / "start-recording.cs"
        setup.write_text(script, encoding="utf-8")
        time.sleep(2)  # Local prefabs complete loading while the first runtime compiler warms up.
        evaluate(setup)
        deadline = time.monotonic() + 900
        previous_frames = -1
        while True:
            time.sleep(2)
            progress = scratch / "recording.json"
            try:
                status = json.loads(progress.read_text(encoding="utf-8-sig"))
            except (FileNotFoundError, json.JSONDecodeError):
                status = {}
            if status.get("error"):
                raise ToolError(status["error"])
            if status.get("completed"):
                break
            frames = status.get("frames", 0)
            if frames // 300 != previous_frames // 300:
                say(f"Captured {frames / 30:.0f}s; score {status.get('playerScore', 0):.0f}:{status.get('enemyScore', 0):.0f}")
            previous_frames = frames
            if process.poll() is not None or time.monotonic() > deadline:
                raise ToolError("Recording stalled; inspect Temp/media logs.")
        output = scratch / "gameplay.mp4"
        run([ffmpeg, "-hide_banner", "-loglevel", "error", "-i", str(scratch / "video.mp4"),
             "-f", "f32le", "-ar", str(status["sampleRate"]), "-ac", str(status["channels"]),
             "-i", str(scratch / "audio.f32"), "-map", "0:v:0", "-map", "1:a:0",
             "-c:v", "copy", "-c:a", "aac", "-b:a", "128k", "-shortest", "-movflags", "+faststart", str(output)])
        record = {"task": task, "sourceHead": head(), "startedAtUtc": started, "completedAtUtc": utc_now(),
                  "buildReport": build_report.relative_to(ROOT).as_posix(), "buildReportSha256": sha256_file(build_report),
                  "recorderSha256": sha256_file(code_path), "capture": "Unity rendered frames and AudioRenderer game audio",
                  "controls": "Automated real UnitCommander orders; normal AI, navigation, damage, capture and respawn",
                  "recording": status, "video": output.relative_to(ROOT).as_posix(),
                  "videoBytes": output.stat().st_size, "videoSha256": sha256_file(output)}
        write_evidence(f"media-{task}.json", record)
        say(f"Recorded {status['frames'] / 30:.1f}s -> {output.relative_to(ROOT).as_posix()}")
    finally:
        if process.poll() is None:
            stop = scratch / "quit.cs"
            stop.write_text("UnityEngine.Application.Quit(); return true;", encoding="utf-8")
            try:
                evaluate(stop)
                process.wait(timeout=10)
            except Exception:
                process.terminate()
                process.wait(timeout=10)
