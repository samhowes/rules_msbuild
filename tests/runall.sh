#!/usr/bin/env bash

set -e


export MSYS2_ARG_CONV_EXCL=*

# if these don't build, nothing will
bazel build //tests/sanity //tests/examples/HelloBazel

bazel test //...

bazel run //:gazelle-dotnet

# these should all be cached because gazelle dotnet shouldn't change anything
# even if they aren't fully cached, they should still pass
bazel test //...

# targets that __must__ be run by itself
bazel build //tests/sandboxing/parallel
