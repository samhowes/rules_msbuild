#!/usr/bin/env bash

sed -i.bak "/--deleted_packages/s#=.*#=$(find e2e/*/* \( -name BUILD -or -name BUILD.bazel \) | xargs -n 1 dirname | paste -sd, -)#" .bazelrc && rm .bazelrc.bak