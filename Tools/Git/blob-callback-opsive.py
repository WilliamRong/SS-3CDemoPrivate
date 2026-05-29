if b'"dependencies"' in blob.data and b"com.opsive" in blob.data:
    import json

    try:
        data = json.loads(blob.data.decode("utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError):
        pass
    else:
        dependencies = data.get("dependencies")
        if isinstance(dependencies, dict):
            changed = False
            for key in list(dependencies.keys()):
                if key.startswith("com.opsive."):
                    del dependencies[key]
                    changed = True
            if changed:
                blob.data = (json.dumps(data, indent=2) + "\n").encode("utf-8")
