def fetch(ctx):
    outputs = [ctx.actions.declare_file(
        "packages.lock.json",
        sibling = restore_file,
    )]
