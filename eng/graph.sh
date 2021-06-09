#!/bin/bash
set -e
if [[ ! -f WORKSPACE ]]; then echo >&2 "not at root"; exit 1; fi

target="${1/\/\//}"
if [[ -n "${target:-}" ]]; then
  set +e
  bazel build "//$target"
  set -e
fi

tmp="$(pwd)/tmp"

if [[ -d "$tmp" ]]; then rm -rf "$tmp"; fi

mkdir "$tmp"
pushd bazel-bin

pkg="$(dirname "${target/://}")"

# shellcheck disable=SC2207
input=($(find "$pkg"/* -name "*.dot" -not \( -path '*runfiles/*' \)))
for i in "${input[@]}"
do
  src="$i"
  echo "$src"
  mkdir -p "$tmp/$pkg"
  dot -Tsvg -o "$tmp/$src.svg" "$(pwd)/$src"
done
