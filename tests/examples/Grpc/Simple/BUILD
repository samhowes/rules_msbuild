load("@rules_msbuild//dotnet:defs.bzl", "msbuild_binary")

msbuild_binary(
    name = "Simple",
    srcs = [
        "Program.cs",
        "Services/GreeterService.cs",
        "Startup.cs",
    ],
    protos = ["//tests/examples/Grpc/Simple/Protos:greet_proto"],
    target_framework = "net5.0",
    visibility = ["//visibility:public"],
    deps = ["@nuget//Grpc.AspNetCore"],
)
