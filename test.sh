#!/bin/bash

rm -rf ~/.nuget/packages/samhowes.microsoft.build

bazel clean --expunge

bazel build //tests/examples/HelloBazel --sandbox_debug

