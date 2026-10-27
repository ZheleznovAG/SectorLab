"""Validate the files produced by the explicit Addressables build against a player artifact."""
from __future__ import annotations

import hashlib
import re
from pathlib import PurePosixPath
from typing import Callable

from .common import ToolError


def verify_content(manifest: dict, target: str, read: Callable[[str], bytes]) -> list[dict]:
    if not manifest.get("succeeded") or manifest.get("target") != target:
        raise ToolError(f"Addressables content was not built successfully for {target}.")
    files = manifest.get("files") or []
    paths = [entry.get("path", "") for entry in files]
    if len(paths) != len(set(paths)):
        raise ToolError("Duplicate files in the content manifest.")
    bundles = [path for path in paths if path.endswith(".bundle")]
    if not bundles or any(not path.startswith(f"aa/{target}/") for path in bundles):
        raise ToolError(f"Content manifest lacks local {target} bundles or contains another platform.")
    if not {"aa/catalog.bin", "aa/catalog.hash", "aa/settings.json"}.issubset(paths):
        raise ToolError("Content manifest lacks its local catalog/settings.")
    for entry in files:
        path = entry["path"]
        relative = PurePosixPath(path)
        if (relative.is_absolute() or ".." in relative.parts or "\\" in path
                or not path.startswith("aa/") or ":" in path):
            raise ToolError("Invalid relative path in content manifest.")
        digest = entry.get("sha256", "")
        if not re.fullmatch(r"[0-9A-Fa-f]{64}", digest) or entry.get("bytes", 0) <= 0:
            raise ToolError(f"Invalid content fingerprint: {path}")
        try:
            data = read(path)
        except (KeyError, FileNotFoundError) as error:
            raise ToolError(f"Player is missing required local content: {path}") from error
        if len(data) != entry["bytes"] or hashlib.sha256(data).hexdigest().upper() != digest.upper():
            raise ToolError(f"Player content differs from the target build: {path}")
        if path.endswith(".bundle") and not data.startswith(b"UnityFS\0"):
            raise ToolError(f"Invalid AssetBundle header: {path}")
    return files
