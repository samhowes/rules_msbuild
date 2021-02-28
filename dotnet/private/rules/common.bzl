load("//dotnet/private:providers.bzl", "DotnetLibraryInfo")

ASSEMBLY_ATTRS = {
    "srcs": attr.label_list(allow_files = [".cs"]),
    "target_framework": attr.string(
        mandatory=True,
        doc=("Target Framework Monikor (TFM) for the target .NET Framework i.e. netcoreapp3.1" + 
        " https://docs.microsoft.com/en-us/dotnet/standard/frameworks")
    ),
    "data": attr.label_list(allow_files = True),
    "deps": attr.label_list(
        providers = [DotnetLibraryInfo],
    ),
    "_proj_template": attr.label(
        default = Label("//dotnet/private/rules:compile.tpl.proj"),
        allow_single_file = True,
    ),
    "_dotnet_context_data": attr.label(default = "//:dotnet_context_data")
}
