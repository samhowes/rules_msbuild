#!/bin/bash

./.buildbuddy/make_rc.sh

cat >.bazelrc <<EOF
build --announce_rc
build --sandbox_debug
build --verbose_failures
test --test_output=all
try-import %workspace%/buildbuddy.bazelrc
EOF
