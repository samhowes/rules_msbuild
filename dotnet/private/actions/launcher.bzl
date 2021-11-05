load("@bazel_skylib//lib:dicts.bzl", "dicts")
load("@bazel_skylib//lib:paths.bzl", "paths")
load("//dotnet/private/util:util.bzl", "to_manifest_path")

def make_launcher(ctx, dotnet, info):
    sdk = dotnet.sdk

    launcher = ctx.actions.declare_file(
        ctx.attr.name + dotnet.ext,
        sibling = info.output_dir,
    )

    is_bin_launcher = dotnet.os == "windows"

    launch_data = {
        "dotnet_bin_path": to_manifest_path(ctx, sdk.dotnet),
        "target_bin_path": to_manifest_path(ctx, info.assembly),
        "output_dir": to_manifest_path(ctx, info.output_dir),
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

    env = dicts.add(dotnet.env.items(), dict([
        [
            k,
            ctx.expand_make_variables("test_env", v, {}),
        ]
        for k, v in extra_env.items()
    ]))

    for k in ["HOME", "USERPROFILE"]:
        env.pop(k, 0)

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
