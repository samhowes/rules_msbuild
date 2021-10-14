def gazelle(name, args = []):
    """Convenience macro for running gazelle-dotnet with `bazel run //:gazelle` in your own workspace

    See https://github.com/samhowes/rules_msbuild for more information
    """
    native.sh_binary(
        name = name,
        srcs = ["@rules_msbuild//tools:gazelle.sh"],
        args = ["$(location @rules_msbuild//tools:gazelle-dotnet)"] + args,
        data = ["@rules_msbuild//tools:gazelle-dotnet"],
        deps = ["@bazel_tools//tools/bash/runfiles"],
    )
