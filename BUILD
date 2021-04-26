load("@my_rules_dotnet//dotnet/private/rules:context.bzl", "dotnet_context_data")
load("@bazel_gazelle//:def.bzl", "gazelle")

# dotnet_context_data collects build options and is depended on by all Dotnet targets.
dotnet_context_data(
    name = "dotnet_context_data",
    visibility = ["//visibility:public"],
)

# gazelle:prefix github.com/samhowes/my_rules_dotnet
gazelle(
    name = "gazelle_repos",
    args = [
        "-from_file=go.mod",
        "-to_macro=go_deps.bzl%go_dependencies",
    ],
    command = "update-repos",
)

# gazelle:go_naming_convention import
gazelle(
    name = "gazelle",
    args = [
        "--go_naming_convention=import",
    ],
)

gazelle(
    name = "gazelle-dotnet",
    gazelle = "//gazelle/dotnet:gazelle-dotnet",
)
