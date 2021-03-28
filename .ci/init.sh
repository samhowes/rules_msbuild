#!/bin/bash

.ci/make_rc.sh

echo "============================ Environment Info ============================"
bazel --version
env | sort
echo "========================== END Environment Info =========================="
