workspace(name = "rules_msbuild")

load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")
load("@bazel_tools//tools/build_defs/repo:git.bzl", "git_repository")

### golang ###
http_archive(
    name = "io_bazel_rules_go",
    sha256 = "7904dbecbaffd068651916dce77ff3437679f9d20e1a7956bff43826e7645fcc",
    urls = [
        "https://mirror.bazel.build/github.com/bazelbuild/rules_go/releases/download/v0.25.1/rules_go-v0.25.1.tar.gz",
        "https://github.com/bazelbuild/rules_go/releases/download/v0.25.1/rules_go-v0.25.1.tar.gz",
    ],
)

load("@io_bazel_rules_go//go:deps.bzl", "go_register_toolchains", "go_rules_dependencies")

go_rules_dependencies()

go_register_toolchains(version = "1.16.2")

# todo(#93) put this back in
#http_archive(
#    name = "bazel_gazelle",
#    sha256 = "62ca106be173579c0a167deb23358fdfe71ffa1e4cfdddf5582af26520f1c66f",
#    urls = [
#        "https://mirror.bazel.build/github.com/bazelbuild/bazel-gazelle/releases/download/v0.23.0/bazel-gazelle-v0.23.0.tar.gz",
#        "https://github.com/bazelbuild/bazel-gazelle/releases/download/v0.23.0/bazel-gazelle-v0.23.0.tar.gz",
#    ],
#)

git_repository(
    name = "bazel_gazelle",
    branch = "windows-custom-lang",
    remote = "https://github.com/samhowes/bazel-gazelle",
)

load("@bazel_gazelle//:deps.bzl", "gazelle_dependencies")
load("//deps:go_deps.bzl", "go_dependencies")

# gazelle:repository_macro deps/go_deps.bzl%go_dependencies
go_dependencies()

gazelle_dependencies()

### bzl ###
load("@bazel_skylib//:workspace.bzl", "bazel_skylib_workspace")

bazel_skylib_workspace()

### dotnet ###

load("@rules_msbuild//dotnet:deps.bzl", "dotnet_register_toolchains", "dotnet_rules_dependencies")

dotnet_rules_dependencies()

dotnet_register_toolchains(
    shas = {
        "darwin_amd64": "b38e6f8935d4b82b283d85c6b83cd24b5253730bab97e0e5e6f4c43e2b741aab",
        "windows_amd64": "abcd034b230365d9454459e271e118a851969d82516b1529ee0bfea07f7aae52",
        "linux_amd64": "3687b2a150cd5fef6d60a4693b4166994f32499c507cd04f346b6dda38ecdc46",
    },
    #    version = "3.1.100",
    version = "host",
)

load("//deps:nuget.bzl", "nuget_deps")

nuget_deps()

load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")

http_archive(
    name = "rules_pkg",
    sha256 = "038f1caa773a7e35b3663865ffb003169c6a71dc995e39bf4815792f385d837d",
    urls = [
        "https://mirror.bazel.build/github.com/bazelbuild/rules_pkg/releases/download/0.4.0/rules_pkg-0.4.0.tar.gz",
        "https://github.com/bazelbuild/rules_pkg/releases/download/0.4.0/rules_pkg-0.4.0.tar.gz",
    ],
)

load("@rules_pkg//:deps.bzl", "rules_pkg_dependencies")

rules_pkg_dependencies()

load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")

http_archive(
    name = "build_bazel_integration_testing",
    sha256 = "bfc43a94d42e08c89a26a4711ea396a0a594bd5d55394d76aae861b299628dca",
    strip_prefix = "bazel-integration-testing-3a6136e8f6287b04043217d94d97ba17edcb7feb",
    type = "zip",
    url = "https://github.com/bazelbuild/bazel-integration-testing/archive/3a6136e8f6287b04043217d94d97ba17edcb7feb.zip",
)

load("@build_bazel_integration_testing//tools:repositories.bzl", "bazel_binaries")
load("@rules_msbuild//dotnet:defs.bzl", "BAZEL_VERSION")

#depend on the Bazel binaries, also accepts an array of versions
bazel_binaries([BAZEL_VERSION])
