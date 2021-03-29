#!/usr/bin/env bash

set +e

# if these don't build, nothing will
bazel build //tests/sanity //tests/HelloBazel

bazel test //tests/HelloBazel:all

# targets that __must__ be run by itself
bazel build //tests/sandboxing/parallel
