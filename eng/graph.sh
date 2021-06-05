#!/bin/bash
set -e
if [[ ! -f WORKSPACE ]]; then echo >&2 "not at root"; exit 1; fi

tmp="$(pwd)/tmp"

if [[ -d "$tmp" ]]; then rm -rf "$tmp"; fi

mkdir "$tmp"
pushd bazel-bin

input=($(find * -name "*.dot" -not \( -path '*runfiles/*' \)))
for i in "${input[@]}"
do
  echo "$i"
  mkdir -p "$tmp/$(dirname "$i")"
  dot -Tsvg -o "$tmp/$i.svg" "$i"
done
