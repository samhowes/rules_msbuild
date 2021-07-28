#!/usr/bin/env bash

tar -cvzhf bazel-out.tar.gz --exclude=*.runfiles bazel-out
tar -czf sandbox.tar.gz --exclude=external "$(bazel info output_base)/sandbox/linux-sandbox"
