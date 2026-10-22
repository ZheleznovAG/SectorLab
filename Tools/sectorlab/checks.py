"""Repository hygiene and privacy checks; the pre-commit hook runs `privacy --staged`."""
from __future__ import annotations

import json
import os
import platform
import re
import shutil
import tempfile
from collections import Counter
from pathlib import Path

from .common import ROOT, ToolError, git, repository_files, run, say

GITLEAKS_VERSION = "8.30.1"
_FORBIDDEN = re.compile(r"(?i)(^|/)(\.env(\..*)?|[^/]+\.(keystore|jks|p12|pfx|pem|key|zip|ulf?))$")
_NOREPLY = re.compile(r"<[^>]+@users\.noreply\.github\.com>")


def find_gitleaks() -> Path:
    """gitleaks from PATH, else the pinned copy in the per-user tools folder (see docs/DEVELOPMENT.md)."""
    found = shutil.which("gitleaks")
    if found:
        return Path(found)
    executable = "gitleaks.exe" if platform.system() == "Windows" else "gitleaks"
    if platform.system() == "Windows":
        base = Path(os.environ.get("LOCALAPPDATA", Path.home() / "AppData" / "Local"))
    else:
        base = Path(os.environ.get("XDG_DATA_HOME", Path.home() / ".local" / "share"))
    pinned = base / "SectorLab" / "Tools" / "gitleaks" / GITLEAKS_VERSION / executable
    if pinned.is_file():
        return pinned
    raise ToolError(f"Gitleaks {GITLEAKS_VERSION} not found: put it in PATH or at {pinned} (docs/DEVELOPMENT.md).")


def privacy(staged: bool = False) -> None:
    paths = (git("diff", "--cached", "--name-only", "--diff-filter=ACMR").splitlines() if staged
             else repository_files())
    forbidden = [p for p in paths if _FORBIDDEN.search(p)]
    if forbidden:
        raise ToolError("Private file types must stay outside Git: " + ", ".join(forbidden))
    identity = git("var", "GIT_AUTHOR_IDENT", check=False)
    if not _NOREPLY.search(identity):
        raise ToolError("Use a repository-local GitHub noreply email for new commits (git config user.email).")
    gitleaks = str(find_gitleaks())
    with tempfile.TemporaryDirectory(prefix="SectorLab-privacy-") as work:
        report = Path(work) / "report.json"
        common = ["--config", str(ROOT / ".gitleaks.toml"), "--redact=100", "--no-banner", "--log-level", "error",
                  "--report-format", "json", "--report-path", str(report)]
        if staged:
            result = run([gitleaks, "git", "--pre-commit", "--staged", *common, "."], check=False)
        else:
            # Scan a copy of exactly the publishable files: ignored local caches and keys stay out.
            snapshot = Path(work) / "snapshot"
            for path in paths:
                target = snapshot / path
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(ROOT / path, target)
            # From inside the snapshot, so the allowlist's repository-relative paths match.
            result = run([gitleaks, "dir", *common, "."], check=False, cwd=snapshot)
        if result.returncode != 0:
            findings = json.loads(report.read_text(encoding="utf-8")) if report.is_file() else []
            where = [f"  {Path(f.get('File', '?')).as_posix().split('/snapshot/')[-1]}:{f.get('StartLine')}"
                     f" {f.get('RuleID')}" for f in findings[:30]]
            raise ToolError("Privacy scan failed (values redacted):\n"
                            + ("\n".join(where) or result.stdout + result.stderr))
    say("PASS privacy: no secrets, personal paths/emails or private file types; author email is noreply.")


def repository(skip_privacy: bool = False) -> None:
    if not skip_privacy:
        privacy()
    for args in (["diff", "--check"], ["diff", "--cached", "--check"]):
        result = run(["git", *args], check=False)
        if result.returncode != 0:
            raise ToolError("Whitespace errors (git " + " ".join(args) + "):\n" + result.stdout)
    for name in ("manifest.json", "packages-lock.json"):
        json.loads((ROOT / "Packages" / name).read_text(encoding="utf-8"))
    metas = [p for p in repository_files("Assets") if p.endswith(".meta")]
    guids = Counter()
    for meta in metas:
        match = re.search(r"^guid: (\w+)", (ROOT / meta).read_text(encoding="utf-8", errors="replace"), re.M)
        if match:
            guids[match.group(1)] += 1
    duplicates = [guid for guid, count in guids.items() if count > 1]
    if duplicates:
        raise ToolError("Duplicate asset GUIDs: " + ", ".join(duplicates))
    owned = repository_files("Assets/_Project")
    known = set(owned)
    missing = [p for p in owned if not p.endswith(".meta") and p + ".meta" not in known]
    if missing:
        raise ToolError("Missing .meta files: " + ", ".join(missing))
    links = documentation_links()
    say(f"PASS repository: whitespace, package JSON, {len(guids)} unique GUIDs, .meta for every project asset, "
        f"{links} documentation links.")


_LINK = re.compile(r"\]\(([^)\s#]+)(?:#[^)]*)?\)")


def documentation_links() -> int:
    """Relative links in the living Markdown resolve; docs/history keeps its links as they were."""
    broken, checked = [], 0
    for path in repository_files():
        if not path.endswith(".md") or path.startswith(("docs/history/", "docs/evidence/", "Assets/")):
            continue
        text = (ROOT / path).read_text(encoding="utf-8", errors="replace")
        for target in _LINK.findall(text):
            if re.match(r"^[a-z][a-z0-9+.-]*:", target, re.I):
                continue
            checked += 1
            if not (ROOT / path).parent.joinpath(target).resolve().exists():
                broken.append(f"{path} -> {target}")
    if broken:
        raise ToolError("Broken documentation links:\n  " + "\n  ".join(broken))
    return checked
