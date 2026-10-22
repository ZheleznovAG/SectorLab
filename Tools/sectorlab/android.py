"""Install the APK on a phone and measure a running game over adb (Android SDK platform-tools)."""
from __future__ import annotations

import csv
import os
import platform
import re
import shutil
import struct
import tempfile
import time
from pathlib import Path

from .common import (EVIDENCE, PACKAGE, ROOT, ToolError, head, run, say, sha256_file, unity_version, utc_now,
                     write_evidence)


def find_adb(explicit: str | None = None) -> str:
    """--adb, $ADB, PATH, $ANDROID_HOME / $ANDROID_SDK_ROOT, then the SDK bundled with this Unity version."""
    candidates = [explicit, os.environ.get("ADB"), shutil.which("adb")]
    exe = "adb.exe" if platform.system() == "Windows" else "adb"
    for variable in ("ANDROID_HOME", "ANDROID_SDK_ROOT"):
        if os.environ.get(variable):
            candidates.append(str(Path(os.environ[variable]) / "platform-tools" / exe))
    version = unity_version()
    sdk = Path("PlaybackEngines") / "AndroidPlayer" / "SDK" / "platform-tools" / exe
    system = platform.system()
    if system == "Windows":
        candidates.append(str(Path(os.environ.get("ProgramFiles", r"C:\Program Files"))
                              / "Unity" / "Hub" / "Editor" / version / "Editor" / "Data" / sdk))
    elif system == "Darwin":
        candidates.append(str(Path("/Applications/Unity/Hub/Editor") / version / sdk))
    else:
        candidates.append(str(Path.home() / "Unity" / "Hub" / "Editor" / version / "Editor" / "Data" / sdk))
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            return candidate
    raise ToolError("adb not found: pass --adb, set ADB/ANDROID_HOME, or install Android Build Support for Unity "
                    + version + ".")


class Device:
    def __init__(self, adb: str, serial: str | None):
        self.adb = adb
        if not serial:
            listed = run([adb, "devices"]).stdout.splitlines()
            ready = [line.split()[0] for line in listed[1:] if re.match(r"^\S+\s+device$", line.strip())]
            if len(ready) != 1:
                raise ToolError(f"Connect exactly one authorized Android device (found {len(ready)}) "
                                "or pick one with --serial. Run `adb devices` and accept the USB-debugging prompt.")
            serial = ready[0]
        self.serial: str = serial
        self.failures = 0

    def adb_args(self, *args: str) -> list[str]:
        return [self.adb, "-s", self.serial, *args]

    def shell(self, command: str) -> str:
        result = run(self.adb_args("shell", command), check=False, timeout=60)
        if result.returncode != 0:
            self.failures += 1
            return ""
        return result.stdout

    def exec_out(self, command: str) -> bytes:
        result = run(self.adb_args("exec-out", command), check=False, binary=True, timeout=60)
        if result.returncode != 0:
            self.failures += 1
            return b""
        return result.stdout

    def game_in_foreground(self) -> bool:
        activities = self.shell("dumpsys activity activities")
        top = re.search(r"topResumedActivity=([^\r\n]+)", activities)
        resumed = (PACKAGE + "/" in top.group(1)) if top else bool(
            re.search(r"(?:m)?ResumedActivity:[^\r\n]*" + re.escape(PACKAGE) + "/", activities))
        if not resumed:
            return False
        # A resumed game can still sit behind the keyguard or the notification shade.
        focus = re.search(r"mCurrentFocus=([^\r\n]+)", self.shell("dumpsys window"))
        return bool(focus) and PACKAGE + "/" in focus.group(1)

    def screen_pixels(self):
        """Raw screencap: width, height and RGBA bytes, without decoding a PNG."""
        data = self.exec_out("screencap")
        if len(data) < 12:
            return None
        width, height = struct.unpack_from("<II", data, 0)
        header = len(data) - width * height * 4
        if header not in (12, 16):  # Android 9+ adds a colour-space field
            return None
        return width, height, memoryview(data)[header:]


def install(apk: Path, adb: str | None, serial: str | None, launch: bool) -> None:
    apk = apk if apk.is_absolute() else ROOT / apk
    if not apk.is_file():
        raise ToolError(f"No APK at {apk}")
    device = Device(find_adb(adb), serial)
    digest = sha256_file(apk)
    say(f"Installing {apk.name} ({apk.stat().st_size} bytes, SHA-256 {digest}) on {device.serial}...")
    run(device.adb_args("install", "-r", str(apk)), timeout=300)
    installed = device.shell(f"pm path {PACKAGE}").strip().splitlines()
    if not installed:
        raise ToolError("Install reported success but the package is missing.")
    remote = installed[0].removeprefix("package:").strip()
    pulled = device.shell(f"sha256sum {remote}").split()
    if pulled and pulled[0].upper() == digest:
        say("PASS: the installed base.apk matches the local SHA-256.")
    else:
        say("Installed; the device could not confirm the hash (sha256sum missing or a split APK).")
    model = device.shell("getprop ro.product.model").strip()
    say(f"Device: {model}, Android {device.shell('getprop ro.build.version.release').strip()}")
    if launch:
        device.shell(f"monkey -p {PACKAGE} -c android.intent.category.LAUNCHER 1")
        say("Launched SectorLab.")


def measure(task: str, duration: int, adb: str | None, serial: str | None, apk: str | None,
            auto_restart: bool) -> None:
    prefix = EVIDENCE / task
    if prefix.with_suffix(".json").exists():
        raise ToolError("Use a new task name to keep earlier measurements.")
    device = Device(find_adb(adb), serial)
    if not device.game_in_foreground():
        raise ToolError("Open SectorLab on the selected device (unlocked, in front) before measuring.")
    model = device.shell("getprop ro.product.model").strip()
    android = device.shell("getprop ro.build.version.release").strip()
    kind = "emulator" if device.shell("getprop ro.kernel.qemu").strip() == "1" else "physical-device"
    clock_ticks = int(device.shell("getconf CLK_TCK").strip() or 0)
    layer = next((line.strip() for line in device.shell("dumpsys SurfaceFlinger --list").splitlines()
                  if line.startswith("SurfaceView - " + PACKAGE + "/")), None)
    if not layer or clock_ticks <= 0:
        raise ToolError("The running Unity surface or process metrics are unavailable.")
    captures = Path(tempfile.mkdtemp(prefix="SectorLab-android-metrics-"))
    started, source_head = utc_now(), head()
    begin = time.monotonic()
    presented: set[int] = set()
    rows, memory, restarts, views = [], [], [], []
    last_pid, last_ticks, last_cpu_time = "", 0, 0.0
    last_memory = last_capture = -100.0
    last_progress = -60.0
    while (elapsed := time.monotonic() - begin) < duration:
        pid = device.shell(f"pidof {PACKAGE}").strip()
        foreground = device.game_in_foreground()
        new_frames, cpu, pss, rss = 0, None, None, None
        if foreground and pid.isdigit():
            for line in device.shell(f"dumpsys SurfaceFlinger --latency '{layer}'").splitlines():
                match = re.match(r"^\d+\s+(\d+)\s+\d+$", line.strip())
                if match:
                    present = int(match.group(1))
                    if 0 < present < 2 ** 63 - 1 and present not in presented:
                        presented.add(present)
                        new_frames += 1
            stat = re.search(r"\)\s+(.+)$", device.shell(f"cat /proc/{pid}/stat"))
            if stat:
                fields = stat.group(1).split()
                ticks, now = int(fields[11]) + int(fields[12]), time.monotonic() - begin
                if last_pid == pid and now > last_cpu_time:
                    cpu = 100.0 * (ticks - last_ticks) / clock_ticks / (now - last_cpu_time)
                last_pid, last_ticks, last_cpu_time = pid, ticks, now
            if elapsed - last_memory >= 10:
                info = device.shell(f"dumpsys meminfo {PACKAGE}")
                if m := re.search(r"TOTAL PSS:\s*(\d+)", info):
                    pss = int(m.group(1))
                if m := re.search(r"TOTAL RSS:\s*(\d+)", info):
                    rss = int(m.group(1))
                if pss is not None:
                    memory.append({"elapsedSeconds": elapsed, "pssKiB": pss, "rssKiB": rss})
                last_memory = elapsed
            if auto_restart and elapsed - last_capture >= 20:
                last_capture = elapsed
                screen = device.screen_pixels()
                if screen and device.game_in_foreground():
                    width, height, pixels = screen
                    # The result screen's restart button spans this band; three separated yellow
                    # samples avoid mistaking its text or a small unit marker for the button.
                    y = int(height * 0.65)
                    def yellow(fraction: float) -> bool:
                        offset = (y * width + int(width * fraction)) * 4
                        r, g, b = pixels[offset], pixels[offset + 1], pixels[offset + 2]
                        return r > 220 and g > 170 and b < 110
                    button = all(yellow(f) for f in (0.32, 0.42, 0.72))
                    views.append({"elapsedSeconds": elapsed, "resultButtonVisible": button})
                    if button and device.game_in_foreground():
                        (captures / f"result-{len(restarts)}.png").write_bytes(device.exec_out("screencap -p"))
                        device.shell(f"input tap {width // 2} {y}")
                        restarts.append({"elapsedSeconds": elapsed,
                                         "action": "Tapped the result screen's restart button seen in a foreground screenshot"})
        rows.append({"elapsedSeconds": elapsed, "foreground": foreground, "appPid": pid,
                     "newPresentedFrames": new_frames, "cpuOneCorePercent": cpu, "pssKiB": pss, "rssKiB": rss})
        if elapsed - last_progress >= 60:
            say(f"{task}: elapsed={elapsed:.0f}s, foreground={foreground}, restart taps={len(restarts)}")
            last_progress = elapsed
        time.sleep(max(0.0, 1.5 - (time.monotonic() - begin - elapsed)))
    stamps = sorted(presented)
    intervals = sorted((b - a) / 1e6 for a, b in zip(stamps, stamps[1:]))
    cpus = [r["cpuOneCorePercent"] for r in rows if r["cpuOneCorePercent"] is not None]
    record = {
        "task": task, "sourceHead": source_head, "startedAtUtc": started, "completedAtUtc": utc_now(),
        "referencedApkSha256": sha256_file(Path(apk)) if apk else None,
        "durationSeconds": time.monotonic() - begin, "model": model, "androidVersion": android,
        "deviceKind": kind, "package": PACKAGE, "samples": len(rows),
        "foregroundSamples": sum(1 for r in rows if r["foreground"]), "uniquePresentedFrames": len(stamps),
        "fpsMean": 1000.0 / (sum(intervals) / len(intervals)) if intervals else None,
        "frameIntervalMsMedian": intervals[len(intervals) // 2] if intervals else None,
        "frameIntervalMsP95": intervals[int((len(intervals) - 1) * 0.95)] if intervals else None,
        "gapsOverOneSecond": sum(1 for i in intervals if i > 1000),
        "cpuOneCorePercentMean": sum(cpus) / len(cpus) if cpus else None,
        "memory": memory, "detectedResultRestarts": restarts, "adbFailures": device.failures,
        "autoRestartEnabled": auto_restart, "workloadViews": views,
        "scriptSha256": sha256_file(Path(__file__)),
        "measurement": "Actual-present timestamps of the Unity SurfaceView; /proc/PID/stat; dumpsys meminfo.",
        "gcAllocations": "NOT_AVAILABLE: no Unity Profiler attached to a release APK.",
        "limitation": "The APK hash does not prove what is installed (use `sl.py install-android`). Restarts count "
                      "detected result-button taps; screenshots stay outside Git. An automated workload is not a "
                      "human playtest. GPU time unavailable. Emulator figures do not stand for a physical phone.",
    }
    path = write_evidence(task + ".json", record)
    with open(prefix.with_suffix(".csv"), "w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]) if rows else ["elapsedSeconds"])
        writer.writeheader()
        writer.writerows(rows)
    with open(EVIDENCE / f"{task}-frames.csv", "w", newline="", encoding="utf-8") as stream:
        stream.write("actualPresentNanoseconds\n" + "".join(f"{s}\n" for s in stamps))
    fps = record["fpsMean"]
    say(f"{task}: {record['durationSeconds']:.1f}s, mean FPS={fps:.2f}" if fps else f"{task}: no frames")
    say(f"foreground {record['foregroundSamples']}/{record['samples']}, restarts {len(restarts)} -> "
        f"{path.relative_to(ROOT).as_posix()}; screenshots in {captures}")
