load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("@bazel_skylib//lib:paths.bzl", "paths")

def make_launcher(ctx, dotnet, info):
    sdk = dotnet.sdk

    launcher = ctx.actions.declare_file(
        ctx.attr.name + dotnet.ext,
        sibling = info.output_dir,
    )

    is_bin_launcher = dotnet.os == "windows"

    # ../dotnet_sdk/dotnet => dotnet_sdk/dotnet
    dotnet_path = sdk.dotnet.short_path[3:]

    launch_data = {
        "dotnet_bin_path": dotnet_path,
        "target_bin_path": paths.join(ctx.workspace_name, info.assembly.short_path),
        "output_dir": info.output_dir.short_path,
        "dotnet_root": sdk.root_file.dirname,
        "dotnet_args": _format_launcher_args([], is_bin_launcher),
        "assembly_args": _format_launcher_args([], is_bin_launcher),
        "workspace_name": ctx.workspace_name,
        "package": ctx.label.package,
        "dotnet_cmd": "exec",
        "dotnet_logger": "junit",
        "log_path_arg_name": "LogFilePath",
    }

    is_test = getattr(dotnet.config, "is_test", False)
    if is_test:
        launch_data = dicts.add(launch_data, {
            "dotnet_cmd": ctx.attr.dotnet_cmd,
        })
    extra_env = getattr(ctx.attr, "test_env", {})
    print(extra_env)
    env = dicts.add(dotnet.env.items(), dict([
        [
            k,
            ctx.expand_make_variables("test_env", v, {}),
        ]
        for k, v in extra_env.items()
    ]))

    print(env)
    launcher_template = ctx.file._launcher_template
    if is_bin_launcher:
        args = ctx.actions.args()
        args.add_all([
            dotnet.builder.assembly,
            "launcher",
            launcher_template,
            launcher,
            "symlink_runfiles_enabled",
            "0",
            "dotnet_env",
        ])

        args.add(";".join([
            "{}={}".format(k, v)
            for k, v in env.items()
        ]))

        for k, v in launch_data.items():
            args.add_all([
                k,
                v,
            ])

        ctx.actions.run(
            inputs = [launcher_template],
            outputs = [launcher],
            executable = sdk.dotnet,
            arguments = [args],
            env = dotnet.env,
            tools = dotnet.builder.files,
        )
    else:
        substitutions = dict([
            ("%{}%".format(k), v)
            for k, v in launch_data.items()
        ])
        substitutions["%dotnet_env%"] = "\n".join([
            "export {}=\"{}\"".format(k, v)
            for k, v in env.items()
        ])

        ctx.actions.expand_template(
            template = launcher_template,
            output = launcher,
            is_executable = True,
            substitutions = substitutions,
        )
    return launcher

def _format_launcher_args(args, bin_launcher):
    if not bin_launcher:
        return " ".join(["\"{}\"".format(a) for a in args])
    else:
        return "*~*".join(args)
