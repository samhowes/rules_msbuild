#!/usr/bin/env bash

set -e


export MSYS2_ARG_CONV_EXCL="*"

# if these don't build, nothing will
bazel build //tests/sanity //tests/examples/HelloBazel

eng/tar.sh

bazel test //... --test_tag_filters=-e2e

bazel run //:gazelle-dotnet

# these should all be cached because gazelle dotnet shouldn't change anything
# even if they aren't fully cached, they should still pass
bazel test //... --test_tag_filters=-e2e

# targets that __must__ be run by themselves
bazel build //tests/sandboxing/parallel

bazel --host_jvm_args=-Xms256m --host_jvm_args=-Xmx1280m test --test_tag_filters=e2e --local_ram_resources=792 --test_arg=--local_ram_resources=13288
