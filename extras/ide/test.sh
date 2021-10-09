#!/bin/bash

if [[ ! -z "$1" ]]; then
  cd "$(dirname "$(rlocation rules_msbuild/"$1")")" || exit
fi

expected="
  BazelWorkspace: @@workspace_name@@
  BazelWorkspacePath: $PWD/@@workspace_path@@
  BazelBin: dotnet-bin
  BazelNuGetWorkspace: @@nuget_workspace_name@@
  BazelExecRoot: $PWD/@@workspace_path@@bazel-@@workspace_name@@
  BazelPackage:
  BazelExternal: $PWD/@@workspace_path@@bazel-@@workspace_name@@/external
  OutputPath: $PWD/@@workspace_path@@dotnet-bin/net5.0/
  BaseIntermediateOutputPath: $PWD/@@workspace_path@@dotnet-bin/obj/
  IntermediateOutputPath: $PWD/@@workspace_path@@dotnet-bin/obj/net5.0/
  BuildProjectReferences: true
"
actual=$(dotnet msbuild -t:PrintVars -nologo)

diff -w -B <(echo "$expected" ) <(echo "$actual")
