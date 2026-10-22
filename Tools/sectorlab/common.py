"""Paths, processes and evidence files shared by every command. Standard library only."""
from __future__ import annotations

import datetime
import hashlib
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / "docs" / "evidence"
PACKAGE = "com.zheleznovag.sectorlab"
TASK_PATTERN = re.compile(r"^[A-Za-z0-9._-]+$")
_USER_HOME = re.compile(r"(?i)(?:[a-z]:[\\/]+Users[\\/]+|/Users/|/home/)[\w.-]+")


class ToolError(Exception):
    """A failed check or command; sl.py prints it and exits with code 1."""


def task_name(value: str) -> str:
    if not TASK_PATTERN.match(value):
        raise ToolError(f"Task name may contain only letters, digits, '.', '_' and '-': {value!r}")
    return value


def utc_now() -> str:
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def run(args: list[str], *, check: bool = True, binary: bool = False, timeout: float | None = None,
        cwd: Path = ROOT) -> subprocess.CompletedProcess:
    """Runs a program without a shell; output is captured as text (UTF-8) unless binary."""
    result = subprocess.run(args, cwd=cwd, capture_output=True, timeout=timeout,
                            text=not binary, encoding=None if binary else "utf-8",
                            errors=None if binary else "replace")
    if check and result.returncode != 0:
        output = result.stdout if binary else (result.stdout + result.stderr)
        raise ToolError(f"{' '.join(args[:4])}... exited with {result.returncode}\n{output}")
    return result


def require_program(name: str, hint: str) -> str:
    found = shutil.which(name)
    if not found:
        raise ToolError(f"'{name}' not found in PATH. {hint}")
    return found


def git(*args: str, check: bool = True) -> str:
    return run(["git", "-c", "core.quotepath=false", *args], check=check).stdout


def head() -> str:
    return git("rev-parse", "HEAD").strip()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def repository_files(*paths: str) -> list[str]:
    """Tracked and untracked, not ignored files that exist on disk, sorted, with '/' separators."""
    listed = git("ls-files", "--cached", "--others", "--exclude-standard", "--", *paths).splitlines()
    return sorted({p for p in listed if p and (ROOT / p).is_file()})


def input_hashes() -> list[dict]:
    """SHA-256 of every project input, so evidence names exactly what was tested or built."""
    return [{"path": p, "sha256": sha256_file(ROOT / p)}
            for p in repository_files("Assets", "Packages", "ProjectSettings")]


def redact(text: str) -> str:
    """Hides machine-local user directories; results, hashes and timestamps stay intact."""
    return _USER_HOME.sub("[LOCAL_USER_HOME]", text)


def write_evidence(name: str, record: dict) -> Path:
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    path = EVIDENCE / name
    text = json.dumps(record, ensure_ascii=False, indent=2)
    path.write_text(redact(text) + "\n", encoding="utf-8", newline="\n")
    return path


def unity_version() -> str:
    text = (ROOT / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8")
    match = re.search(r"m_EditorVersion:\s*(\S+)", text)
    if not match:
        raise ToolError("ProjectSettings/ProjectVersion.txt has no m_EditorVersion.")
    return match.group(1)


def say(message: str) -> None:
    print(message, flush=True)


def warn(message: str) -> None:
    print("WARNING: " + message, file=sys.stderr, flush=True)
