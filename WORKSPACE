workspace(name = "rules_msbuild")

load("@bazel_tools//tools/build_defs/repo:http.bzl", "http_archive")

### dotnet ###

load("@rules_msbuild//dotnet:repositories.bzl", "dotnet_register_toolchains", "dotnet_rules_repositories")

dotnet_rules_repositories()

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

# gazelle:nuget_macro deps:nuget.bzl%nuget_deps
nuget_deps()

### golang ###

load("@io_bazel_rules_go//go:deps.bzl", "go_register_toolchains", "go_rules_dependencies")
load("@bazel_gazelle//:deps.bzl", "gazelle_dependencies")
load("//deps:go_deps.bzl", "go_dependencies")

# gazelle:repository_macro deps/go_deps.bzl%go_dependencies
go_dependencies()

go_rules_dependencies()

go_register_toolchains(version = "1.16")

gazelle_dependencies()

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

### proto & grpc
http_archive(
    name = "rules_proto_grpc",
    sha256 = "28724736b7ff49a48cb4b2b8cfa373f89edfcb9e8e492a8d5ab60aa3459314c8",
    strip_prefix = "rules_proto_grpc-4.0.1",
    urls = ["https://github.com/rules-proto-grpc/rules_proto_grpc/archive/4.0.1.tar.gz"],
)

load("@rules_proto_grpc//:repositories.bzl", "rules_proto_grpc_repos", "rules_proto_grpc_toolchains")

rules_proto_grpc_toolchains()

rules_proto_grpc_repos()

load("@rules_proto//proto:repositories.bzl", "rules_proto_dependencies", "rules_proto_toolchains")

rules_proto_dependencies()

rules_proto_toolchains()

load("@rules_proto_grpc//go:repositories.bzl", rules_proto_grpc_go_repos = "go_repos")

rules_proto_grpc_go_repos()
