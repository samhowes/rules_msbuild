"""Core macros of my_rules_dotnet"""

load("//dotnet/private/rules:core.bzl", _dotnet_binary = "dotnet_binary", _dotnet_library = "dotnet_library", _dotnet_restore = "dotnet_restore")

def _dotnet_target(name, target_framework, deps, kwargs, target_rule):
    restore_label = name + "_restore"
    _dotnet_restore(
        name = restore_label,
        target_framework=target_framework,
        deps = deps
    )

    target_deps = list(deps)
    target_deps.append(restore_label)

    target_rule(
        name=name,
        target_framework = target_framework,
        deps=target_deps,
        **kwargs
    )

def dotnet_binary(name, target_framework, deps = [], **kwargs):
    """Defines labels: name,name_restore"""
    _dotnet_target(name, target_framework, deps, kwargs, _dotnet_binary)


def dotnet_library(name, target_framework, deps = [], **kwargs):
    """Defines labels: name, name_restore"""
    _dotnet_target(name, target_framework, deps, kwargs, _dotnet_library)


