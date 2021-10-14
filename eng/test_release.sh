#!/bin/bash

set -euo pipefail

version="$(cat version.bzl | cut -d\" -f 2)"
nupkg="$(pwd)/bazel-bin/dotnet/tools/Bzl/SamHowes.Bzl.$version.nupkg"

export NUGET_API_KEY="foo"
bazel run //eng/release

if [[ -d "tmp" ]]; then rm -rf tmp; fi

mkdir tmp
cp bazel-bin/rules_msbuild.tar.gz tmp
cp bazel-bin/rules_msbuild.tar.gz.sha256 tmp

pushd tmp || exit
tarfile="$(pwd)/rules_msbuild.tar.gz"

unzip "$nupkg" -d nupkg
chmod -R 755 nupkg
tool="$(pwd)/nupkg/tools/netcoreapp3.1/any/SamHowes.Bzl.dll"

mkdir test && pushd test
dotnet new console -o console --no-restore
dotnet exec "$tool" _test "$tarfile"

bazel run //:gazelle
result="$(bazel run //console)"

if [[ "$result" != "Hello World!" ]]; then
  echo "test failed, bad output: $result"
  exit 1
fi

echo "SUCCESS"
