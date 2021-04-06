#!/usr/bin/env bash

set +e

report=""
exit_status=0
function run() {
  "$@"
  this_exit=$?
  this_str=$*
  printf -v report "%s%s: %s\n" "$report" "$this_str" "$this_exit"
  ((exit_status = exit_status || this_exit))
}

# if these don't build, nothing will
run bazel build //tests/sanity //tests/examples/HelloBazel

run bazel test //tests/examples/... \
  //tests/dotnet/...

# targets that __must__ be run by itself
run bazel build //tests/sandboxing/parallel

# todo(#12) remove this call
run dotnet test tests/dotnet/tools/builder
run dotnet test tests/dotnet/tools/runfiles

tree -l bazel-my_rules_dotnet/external/nuget | tee bazel-out/nuget.txt

echo -e "\n\n============================================ TEST REPORT ================================================="
echo -n "$report"
echo -e "Exiting with status: $exit_status"
exit $exit_status
