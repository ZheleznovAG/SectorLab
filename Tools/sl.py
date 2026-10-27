#!/usr/bin/env python3
"""SectorLab developer commands. Python 3.9+, standard library only; Windows, macOS and Linux.

    python Tools/sl.py doctor                          what this machine can run
    python Tools/sl.py test --task T [--mode both]     EditMode/PlayMode tests in the open Editor
    python Tools/sl.py test --task T --mode PlayMode --filter SectorLab.Tests.NetworkMatchTests
    python Tools/sl.py check                           repository hygiene + privacy scan
    python Tools/sl.py privacy [--staged]              secrets / personal data (pre-commit hook)
    python Tools/sl.py build-android --task T          APK into Builds/Android/SectorLab-T.apk
    python Tools/sl.py build-windows --task T --qa     Windows player with local QA commands
    python Tools/sl.py verify-network --task T --build-task B  two Windows QA players over UDP
    python Tools/sl.py install-android [--apk P]       install on the connected phone, verify hash
    python Tools/sl.py measure-android --task T        FPS/CPU/memory of the running game

Test and build records go to docs/evidence/. See docs/DEVELOPMENT.md.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from sectorlab.common import ROOT, ToolError, task_name  # noqa: E402


def latest_apk() -> Path:
    apks = sorted((ROOT / "Builds" / "Android").glob("*.apk"), key=lambda p: p.stat().st_mtime)
    if not apks:
        raise ToolError("No APK in Builds/Android; build one with: python Tools/sl.py build-android --task <name>")
    return apks[-1]


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(prog="sl.py", description=__doc__.split("\n\n")[0],
                                     formatter_class=argparse.RawDescriptionHelpFormatter, epilog=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)

    commands.add_parser("doctor", help="list available tools and the Editor/device state")

    test = commands.add_parser("test", help="run Unity tests in the open Editor")
    test.add_argument("--task", required=True, type=task_name, help="evidence name, e.g. 2026-10-22-docs")
    test.add_argument("--mode", choices=["EditMode", "PlayMode", "both"], default="both")
    test.add_argument("--filter", default="", help="one full test or fixture name instead of the whole assembly")
    test.add_argument("--timeout-minutes", type=float, default=30)

    check = commands.add_parser("check", help="whitespace, package JSON, GUIDs, .meta files, privacy")
    check.add_argument("--skip-privacy", action="store_true", help="when gitleaks is unavailable")

    privacy = commands.add_parser("privacy", help="secrets and personal data scan")
    privacy.add_argument("--staged", action="store_true", help="scan only staged changes (pre-commit)")

    build = commands.add_parser("build-android", help="build the APK through the open Editor")
    build.add_argument("--task", required=True, type=task_name)
    build.add_argument("--timeout-minutes", type=float, default=30)

    windows = commands.add_parser("build-windows", help="build Windows and its local Addressables content")
    windows.add_argument("--task", required=True, type=task_name)
    windows.add_argument("--qa", action="store_true", help="Development build with local Pipeline commands")
    windows.add_argument("--timeout-minutes", type=float, default=30)

    network = commands.add_parser("verify-network", help="acceptance with two Windows QA players over UDP")
    network.add_argument("--task", required=True, type=task_name)
    network.add_argument("--build-task", required=True, type=task_name)

    install = commands.add_parser("install-android", help="install an APK on the connected device")
    install.add_argument("--apk", type=Path, help="default: the newest APK in Builds/Android")
    install.add_argument("--adb")
    install.add_argument("--serial")
    install.add_argument("--no-launch", action="store_true")

    measure = commands.add_parser("measure-android", help="sample FPS/CPU/memory of the running game")
    measure.add_argument("--task", required=True, type=task_name)
    measure.add_argument("--duration", type=int, default=600, help="seconds (30..3600)")
    measure.add_argument("--adb")
    measure.add_argument("--serial")
    measure.add_argument("--apk", help="APK the measurement refers to (hash recorded)")
    measure.add_argument("--auto-restart", action="store_true", help="tap 'Ещё бой' on result screens")

    args = parser.parse_args(argv)
    try:
        if args.command == "doctor":
            from sectorlab.doctor import doctor
            doctor()
        elif args.command == "test":
            from sectorlab.unity import run_tests
            modes = ["EditMode", "PlayMode"] if args.mode == "both" else [args.mode]
            for mode in modes:
                run_tests(mode, args.task, args.filter, args.timeout_minutes)
        elif args.command == "check":
            from sectorlab.checks import repository
            repository(skip_privacy=args.skip_privacy)
        elif args.command == "privacy":
            from sectorlab.checks import privacy as scan
            scan(staged=args.staged)
        elif args.command == "build-android":
            from sectorlab.unity import build_android
            build_android(args.task, args.timeout_minutes)
        elif args.command == "build-windows":
            from sectorlab.unity import build_windows
            build_windows(args.task, args.qa, args.timeout_minutes)
        elif args.command == "verify-network":
            from sectorlab.network_acceptance import verify
            verify(args.task, args.build_task)
        elif args.command == "install-android":
            from sectorlab.android import install as install_apk
            install_apk(args.apk or latest_apk(), args.adb, args.serial, launch=not args.no_launch)
        elif args.command == "measure-android":
            if not 30 <= args.duration <= 3600:
                raise ToolError("--duration must be 30..3600 seconds.")
            from sectorlab.android import measure as measure_game
            measure_game(args.task, args.duration, args.adb, args.serial, args.apk, args.auto_restart)
    except ToolError as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("Interrupted.", file=sys.stderr)
        return 130
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
