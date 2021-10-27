#!/bin/bash

# run via shell script directly instead of through bazel so we don't have to deal with bazel's environment variables

set -euo pipefail
bazel build //eng/release
export BUILD_WORKSPACE_DIRECTORY="$(pwd)"
tool="$(pwd)/bazel-bin/eng/release/net5.0/release.dll"
if [[ -d tmp ]]; then rm -rf tmp; fi
mkdir -p tmp
pushd tmp
dotnet exec "$tool" test
