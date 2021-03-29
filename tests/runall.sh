#!/usr/bin/env bash

set +e

report=""
exit_status=0
function run() {
  "$@"
  this_exit=$?
  this_str=$*
  printf -v report "%s%s: %s\n" "$report" "$this_str" "$this_exit"
  (( exit_status = exit_status || this_exit))
}

# if these don't build, nothing will
run bazel build //tests/sanity //tests/HelloBazel

run bazel test //tests/HelloBazel:all

# targets that __must__ be run by itself
run bazel build //tests/sandboxing/parallel


echo -e "\n\n============================================ TEST REPORT ================================================="
echo -n "$report"
echo -e "Exiting with status: $exit_status"
exit $exit_status
