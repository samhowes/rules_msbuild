def built_path(ctx, outputs, p, is_directory=False):
    if is_directory:
        msbuild_path = p + "/"
        output = ctx.actions.declare_directory(p)        
    else:
        output = ctx.actions.declare_file(p)
        msbuild_path = p
    outputs.append(output)
    return struct(
        file = output,
        msbuild_path = msbuild_path,
        short_path = output.short_path
    )
