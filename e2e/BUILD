load(":test.bzl", "e2e_test")
# gazelle:exclude *

e2e_test(
    name = "e2e_hello",
    commands = [
        "build //...",
    ],
    run = {
        "//RunfilesTest": "bar\n\n",
        "//:test_external": "pong\n\n",
    },
)

e2e_test(
    name = "e2e_bzl_init",
    commands = [
        "init",
        "build //Binary",
    ],
)
