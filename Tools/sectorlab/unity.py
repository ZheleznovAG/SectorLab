"""Tests and builds through an open Unity Editor, via the Unity CLI (`unity command ...`).

The Editor must be running with this project and the Pipeline package (com.unity.pipeline);
`unity open .` starts it. Every run writes a JSON record to docs/evidence/.
"""
from __future__ import annotations

import hashlib
import json
import time
import zipfile
from pathlib import Path

from .common import (ROOT, ToolError, git, head, input_hashes, require_program, run, say,
                     sha256_file, utc_now, warn, write_evidence)

# A domain reload briefly removes the Pipeline endpoint; only read-only polling may retry.
_POLLING = {"console_status", "test_status", "build_status", "editor_status"}


def command(*args: str, timeout: int = 30) -> dict:
    unity = require_program("unity", "Install the Unity CLI (https://unity.com) and open the Editor: unity open .")
    started = time.monotonic()
    while True:
        result = run([unity, "command", "--json", "--no-banner", "--timeout", str(timeout), *args], check=False)
        if result.returncode == 0:
            break
        if args[0] not in _POLLING or time.monotonic() - started > 60:
            raise ToolError(f"Unity CLI failed: {' '.join(args)}\n{result.stdout}{result.stderr}")
        say(f"Waiting for the Unity endpoint after a domain reload: {args[0]}")
        time.sleep(2)
    try:
        response = json.loads(result.stdout)
    except json.JSONDecodeError as error:
        raise ToolError(f"Unity CLI returned non-JSON output for {args[0]}: {error}\n{result.stdout}") from error
    if not response.get("success") or not (response.get("data") or {}).get("success"):
        raise ToolError(f"Unity command {args[0]} failed:\n{result.stdout}")
    return response


def _result(response: dict) -> dict:
    return response["data"].get("result") or {}


def wait_for_compilation(limit_seconds: float = 180) -> None:
    """Leaves Play Mode, refreshes assets and waits until scripts compile cleanly."""
    command("editor_stop")
    started = time.monotonic()
    while _result(command("eval", "--code", "return UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;")).get("result"):
        if time.monotonic() - started > 60:
            raise ToolError("Timed out leaving Play Mode.")
        time.sleep(0.5)
    command("eval", "--code", "UnityEditor.AssetDatabase.Refresh();")
    started = time.monotonic()
    stable = 0
    while stable < 3 or time.monotonic() - started < 3:
        truth = _result(command("console_status")).get("groundTruth") or {}
        elapsed = time.monotonic() - started
        # Refresh schedules compilation; the first status can still describe the old assembly.
        if truth.get("compilationFailed") and not truth.get("compiling") and elapsed > 5:
            raise ToolError("Unity compilation failed; read the console: unity command console --level error")
        if elapsed > limit_seconds:
            raise ToolError("Timed out waiting for compilation.")
        stable = 0 if truth.get("compiling") or truth.get("compilationFailed") else stable + 1
        time.sleep(0.5)


def run_tests(mode: str, task: str, test_filter: str = "", limit_minutes: float = 30) -> dict:
    source_head, started = head(), utc_now()
    hashes = input_hashes()
    wait_for_compilation()
    # The Editor is another process: hand it the evidence task name before the tests start.
    context = ROOT / "Temp" / "verification-task.cs"
    context.parent.mkdir(exist_ok=True)
    context.write_text(f'System.Environment.SetEnvironmentVariable("SECTORLAB_VERIFICATION_TASK", "{task}");\n',
                       encoding="utf-8")
    command("eval_file", "--file", str(context))
    filter_type, filter_value = ("testName", test_filter) if test_filter else ("assembly", "SectorLab.Tests." + mode)
    command("run_tests", "--mode", mode, "--filter_type", filter_type, "--filter", filter_value, "--async_tests", "true")
    started_run = time.monotonic()
    while True:
        time.sleep(2)
        status = command("test_status")
        if _result(status).get("status") == "completed":
            break
        if time.monotonic() - started_run > limit_minutes * 60:
            raise ToolError("Timed out waiting for Unity tests; they may still be running in the Editor.")
    record = {
        "task": task, "mode": mode, "sourceHead": source_head,
        "startedAtUtc": started, "completedAtUtc": utc_now(),
        "command": f"unity command run_tests --mode {mode} --filter_type {filter_type} --filter {filter_value} --async_tests true",
        "inputHashes": hashes, "response": status,
        "privacyRedaction": "Machine-local user directories redacted; test results and source hashes unchanged.",
    }
    path = write_evidence(f"tests-{task}-{mode.lower()}.json", record)
    summary = _result(status).get("summary") or {}
    say(f"{task} {mode}: {json.dumps(summary)} -> {path.relative_to(ROOT).as_posix()}")
    if (summary.get("total", 0) <= 0 or summary.get("failed", 1) != 0
            or summary.get("skipped", 0) != 0 or summary.get("inconclusive", 0) != 0):
        failures = [t.get("name") for t in _result(status).get("tests", []) if t.get("result") not in (None, "Passed")]
        raise ToolError("Tests did not all pass" + (f": {', '.join(map(str, failures[:20]))}" if failures else "")
                        + "; see the evidence JSON.")
    return summary


def build_android(task: str, limit_minutes: float = 30) -> Path:
    apk = ROOT / "Builds" / "Android" / f"SectorLab-{task}.apk"
    if apk.exists():
        raise ToolError("Use a new task name: an APK with this name exists and stays as evidence.")
    apk_relative = apk.relative_to(ROOT).as_posix()
    source_head, started = head(), utc_now()
    hashes = input_hashes()
    settings_before = git("diff", "--name-only", "--", "ProjectSettings")
    command("build", "--target", "Android", "--outputPath", apk_relative, "--confirm", "true")
    started_build = time.monotonic()
    while True:
        time.sleep(3)
        status = command("build_status")
        if _result(status).get("status") == "completed":
            break
        if time.monotonic() - started_build > limit_minutes * 60:
            raise ToolError("Android build timeout; inspect the Editor and keep the pending build.")
    result = _result(status)
    record = {
        "task": task, "sourceHead": source_head, "startedAtUtc": started, "completedAtUtc": utc_now(),
        "command": f"unity command build --target Android --outputPath {apk_relative} --confirm true",
        **{key: result.get(key) for key in ("status", "result", "buildId", "buildStartedAt", "buildEndedAt",
                                            "buildTimeMs", "totalErrors", "totalWarnings")},
        "apkPath": apk_relative, "inputHashes": hashes,
        "privacyRedaction": "Machine-local user directories redacted; build results and source hashes unchanged.",
    }
    if result.get("result") == "Succeeded" and apk.exists():
        record["apkBytes"] = apk.stat().st_size
        record["apkSha256"] = sha256_file(apk)
        with zipfile.ZipFile(apk) as archive:
            record["localContent"] = [
                {"path": entry.filename, "bytes": entry.file_size,
                 "sha256": hashlib.sha256(archive.read(entry)).hexdigest().upper()}
                for entry in archive.infolist() if entry.filename.startswith("assets/aa/")]
    path = write_evidence(f"android-build-{task}.json", record)
    say(f"{task}: result={result.get('result')}, errors={result.get('totalErrors')} -> {path.relative_to(ROOT).as_posix()}")
    if git("diff", "--name-only", "--", "ProjectSettings") != settings_before:
        warn("The build rewrote ProjectSettings (usually preloadedAssets). Review `git diff ProjectSettings` "
             "and restore it with `git checkout -- ProjectSettings/ProjectSettings.asset` if that is all.")
    if result.get("result") != "Succeeded" or result.get("totalErrors") or "apkSha256" not in record:
        raise ToolError("Android build failed; see the evidence JSON and the Editor console.")
    if not record.get("localContent"):
        raise ToolError("APK contains no local Addressables content (assets/aa/).")
    say(f"APK {apk_relative}: {record['apkBytes']} bytes, SHA-256 {record['apkSha256']}")
    return apk
