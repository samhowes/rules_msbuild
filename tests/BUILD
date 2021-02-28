load("//dotnet:defs.bzl", "dotnet_binary")

sh_test(
    name = "hello_test",
    srcs = ["hello_test.sh"],
    args = ["$(location :hello)"],
    data = [":hello"],
)

dotnet_binary(
    name = "hello",
    srcs = [
        "hello.cs",
    ],
)
