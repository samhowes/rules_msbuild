load(":xml_util.bzl", "inline_element")

NUGET_BUILD_CONFIG = "NuGet.Build.Config"

def prepare_nuget_config(packages_folder, restore_enabled, package_sources):
    sources = []
    for name, url in package_sources.items():
        attrs = {"key": name, "value": url}

        # todo(#46)
        if True:
            attrs["protocolVersion"] = "3"
        sources.append(inline_element("add", attrs))

    return {
        "{packages_folder}": packages_folder,
        "{restore_enabled}": str(restore_enabled),
        "{package_sources}": "\n    ".join(sources),
    }
