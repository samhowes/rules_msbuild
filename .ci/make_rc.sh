#!/bin/bash

./.buildbuddy/make_rc.sh

cat >.bazelrc <<EOF
build --announce_rc
build --verbose_failures
test --test_output=all
test --test_env=GO_TEST_WRAP_TESTV=1 # otherwise go doesn't report tests that pass
try-import %workspace%/buildbuddy.bazelrc
EOF
