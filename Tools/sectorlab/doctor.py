"""What this machine can do right now: one line per tool, nothing changed."""
from __future__ import annotations

import shutil
import sys

from .common import ROOT, ToolError, git, run, unity_version


def _line(name: str, ok: bool, detail: str) -> bool:
    print(f"{'OK  ' if ok else '--  '} {name:<16} {detail}")
    return ok


def doctor() -> None:
    _line("python", sys.version_info >= (3, 9), sys.version.split()[0])
    _line("git", bool(shutil.which("git")), git("--version").strip())
    lfs = run(["git", "lfs", "version"], check=False)
    _line("git-lfs", lfs.returncode == 0, lfs.stdout.strip() or "install Git LFS")
    hooks = git("config", "--get", "core.hooksPath", check=False).strip()
    _line("hooks", hooks == ".githooks", hooks or "run: git config core.hooksPath .githooks")
    email = git("config", "--get", "user.email", check=False).strip()
    _line("author email", email.endswith("@users.noreply.github.com"), "noreply" if email.endswith(
        "@users.noreply.github.com") else "set a GitHub noreply user.email for this repository")
    unity = shutil.which("unity")
    _line("unity CLI", bool(unity), "found" if unity else "install the Unity CLI")
    if unity:
        status = run([unity, "status", "--no-banner"], check=False).stdout
        project = str(ROOT).replace("\\", "/").lower()
        open_here = any(project in line.replace("\\", "/").lower() and "ready" in line for line in status.splitlines())
        _line("Unity Editor", open_here, f"{unity_version()} " + ("open and ready" if open_here else "not open: unity open ."))
    try:
        from .checks import find_gitleaks
        _line("gitleaks", True, str(find_gitleaks()))
    except ToolError as error:
        _line("gitleaks", False, str(error))
    try:
        from .android import find_adb
        adb = find_adb()
        devices = [l for l in run([adb, "devices"], check=False).stdout.splitlines()[1:] if l.strip()]
        _line("adb", True, f"{len(devices)} device(s): " + ", ".join(l.split()[-1] for l in devices) if devices
              else "no device connected")
    except ToolError as error:
        _line("adb", False, str(error))
