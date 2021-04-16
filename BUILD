load("@my_rules_dotnet//dotnet/private/rules:context.bzl", "dotnet_context_data")
load("@bazel_gazelle//:def.bzl", "gazelle")

# dotnet_context_data collects build options and is depended on by all Dotnet targets.
dotnet_context_data(
    name = "dotnet_context_data",
    visibility = ["//visibility:public"],
)

# gazelle:prefix github.com/samhowes/my_rules_dotnet
gazelle(name = "gazelle")
