# //tests/dotnet/tools/launcher

A set of tests that verifies that the launcher is functioning appropriately

1. `:Greeter`: a simple binary that writes its first argument to the stdout, or prints environment
   variables if no arguments are specified
1. `:run_greeter`: causes `:Greeter` to be compiled to `bazel-out/host`. This causes `:Greeter` to
   get its own runfiles.
    1. This also reproduces [#33](https://github.com/samhowes/rules_msbuild/issues/33) when built
       simultaneously with `:Greeter`, see
       [`//tests/sandboxing/parallel`](../../../sandboxing/parallel/Readme.md) for coverage of that issue.
1. `:launcher_test`: the primary set of tests for this package, a set of assertions that the
   launcher works in various scenarios:
    1. Simulate `bazel run` via checking the output file of `:run_greeter`
        1. `$0` is the result of `$(location :Greeter)` via makefile expansion
    2. Execute `:Greeter` as a data dependency: it should use `RUNFILES_DIR` and
       `RUNFILES_MANIFEST_FILE` from environment
        1. `$0` is the result of `Rlocation` run from python
    3. Simulate executing manually by the user in the directory next to `Greeter.runfiles`
        1. `$0` is `./Greeter(.exe)`, the original launcher
        1. Accomplished via constructing a fake runfiles directory/manifest file
    4. (non-windows) Simulate executing manually by the user in `:Greeter`'s runfiles
        1. `$0` is `./Greeter` which is a symlink to the original launcher
    5. (non-windows) executed manually in the runfiles of another binary
        1. `$0` is `./Greeter` which is a symlink to the original launcher
