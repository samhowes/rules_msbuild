#!/bin/bash

set -euo pipefail



bazel 
result="$(bazel run //console)"

if [[ "$result" != "Hello World!" ]]; then
  echo "test failed, bad output: $result"
  exit 1
fi

echo "SUCCESS"
