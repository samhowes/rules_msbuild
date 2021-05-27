def write_cache_manifest(ctx, caches):
    cache_manifest = ctx.actions.declare_file(ctx.attr.name + ".input_caches")
    ctx.actions.write(cache_manifest, "\n".join([c.path for c in caches.to_list()]))
    return cache_manifest
