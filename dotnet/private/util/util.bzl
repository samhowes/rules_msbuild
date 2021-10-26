# Avoid using non-normalized paths (workspace/../other_workspace/path)
def to_manifest_path(ctx, file, full_path = False):
    p = file.short_path if not full_path else file.path
    if p.startswith("../"):
        return p[3:]
    else:
        return ctx.workspace_name + "/" + p
