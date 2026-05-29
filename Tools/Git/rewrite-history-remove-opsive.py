"""Rewrite history to remove Opsive commercial assets."""

from __future__ import annotations

import json
import sys

import git_filter_repo as fr


def blob_callback(blob, meta):
    if b'"dependencies"' not in blob.data or b"com.opsive" not in blob.data:
        return

    try:
        data = json.loads(blob.data.decode("utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError):
        return

    dependencies = data.get("dependencies")
    if not isinstance(dependencies, dict):
        return

    changed = False
    for key in list(dependencies.keys()):
        if key.startswith("com.opsive."):
            del dependencies[key]
            changed = True

    if changed:
        blob.data = (json.dumps(data, indent=2) + "\n").encode("utf-8")


def main() -> int:
    args = fr.FilteringOptions.parse_args(
        [
            "--force",
            "--invert-paths",
            "--path",
            "Packages/com.opsive.behaviordesigner",
            "--path",
            "Packages/com.opsive.graphdesigner",
            "--path",
            "Packages/com.opsive.shared",
            "--path",
            "Assets/Opsive",
            "--path",
            "Assets/Samples/Opsive Behavior Designer",
            "--path",
            "Assets/Opsive.meta",
            "--path",
            "Assets/Samples/Opsive Behavior Designer.meta",
        ]
    )
    fr.RepoFilter(args, blob_callback=blob_callback).run()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
