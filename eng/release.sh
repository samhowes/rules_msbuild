#!/usr/bin/env bash

set -e
if [[ ! -f WORKSPACE ]]; then echo >&2 "not at root"; exit 1; fi

tag="0.0.1"

if [[ "${tag:-}" == "" ]]; then
  echo "provide a tag as an argument"
  exit 1
elif [[ "${2:-}" == "clean" ]]; then
  set +e
  gh release delete "$tag" -y
  git push --delete origin "$tag"
  exit 0
fi

bazel build //:tar

tarfile="bazel-bin/rules_msbuild-$tag.tar.gz"
rm -f $tarfile
cp bazel-bin/rules_msbuild.tar.gz $tarfile
cp bazel-bin/WORKSPACE.tpl "dotnet/tools/Bzl/WORKSPACE.tpl"

bazel build //dotnet/tools/Bzl:SamHowes.Bzl_nuget
nuget="bazel-bin/dotnet/tools/Bzl/SamHowes.Bzl.$tag.nupkg"

echo "Checking for existing release..."
function get_url() {
  gh release view "$tag" --json tarballUrl --jq .tarballUrl 2> /dev/null || echo ""
}
url=$(get_url)

if [[ "$url" == "" ]]; then
  echo "Creating release $tag"
  gh release create "$tag" -F ReleaseNotes.md $tarfile $nuget
  url=$(get_url)
else
  echo "Release exists"
fi

