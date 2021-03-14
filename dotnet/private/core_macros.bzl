"""Core macros of my_rules_dotnet"""

load(
    "//dotnet/private/rules:core.bzl",
    _dotnet_binary = "dotnet_binary",
    _dotnet_library = "dotnet_library",
)
load(
    "//dotnet/private/rules:nuget.bzl",
    "nuget_pre_restore",
)

def _dotnet_target(name, target_framework, deps, tags, visibility, kwargs, target_rule):
    pre_restore_label = name + "_pre_restore"
    restore_label = name + "_restore"
    pre_restore_tags = list(tags)
    pre_restore_tags.append("dotnet_pre_restore")
    primary_tags = list(tags)
    primary_tags.append("dotnet_primary")

    pre_restore_visibility = list(visibility)
    pre_restore_visibility.append("@nuget//:__pkg__")

    nuget_pre_restore(
        name = pre_restore_label,
        primary_name = name,
        target_framework = target_framework,
        deps = deps,
        tags = pre_restore_tags,
        visibility = pre_restore_visibility,
    )

    # nuget_restore(
    #     name = restore_label,
    #     primary_name = name,
    #     target_framework = target_framework,
    #     pre_restore = pre_restore_label,
    #     tags = tags,
    # )

    target_rule(
        name = name,
        target_framework = target_framework,
        deps = deps,
        restore = pre_restore_label,
        tags = primary_tags,
        **kwargs
    )

def dotnet_binary(name, target_framework, deps = [], tags = [], visibility = [], **kwargs):
    """Defines labels: name,name_restore"""
    _dotnet_target(name, target_framework, deps, tags, visibility, kwargs, _dotnet_binary)

def dotnet_library(name, target_framework, deps = [], tags = [], visibility = [], **kwargs):
    """Defines labels: name, name_restore"""
    _dotnet_target(name, target_framework, deps, tags, visibility, kwargs, _dotnet_library)
