#!/usr/bin/env bash

set +e

echo "$TMPDIR"

report=""
exit_status=0
function run() {
  "$@"
  this_exit=$?
  this_str=$*
  printf -v report "%s%s: %s\n" "$report" "$this_str" "$this_exit"
  (( exit_status = exit_status || this_exit))
}

function fail() {
  return $1
}

# if these don't build, nothing will
run bazel build //tests/sanity //tests/HelloBazel

run bazel test //tests/HelloBazel:all

# targets that __must__ be run by itself
run bazel build //tests/sandboxing/parallel

echo -n "$report"
exit $exit_status
