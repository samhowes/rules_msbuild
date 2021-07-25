#!/bin/bash
set -e
if [[ ! -f WORKSPACE ]]; then echo >&2 "not at root"; exit 1; fi

arg_string=" $* "
target="${1/\/\//}"
echo "using target '$target'"
# shellcheck disable=SC2199
if [[ -n "${target:-}" && ! "$arg_string" =~ "--no_build" ]]; then
  set +e
  bazel build --config=diag "//$target"
  set -e
fi

tmp="$(pwd)/tmp"

if [[ -d "$tmp" ]]; then rm -rf "$tmp"; fi

mkdir "$tmp"
pushd bazel-bin

to_open=""
if [[ "$target" == *":"* ]]; then
  target_as_path="${target/://}"
  pkg="$(dirname "$target_as_path")"
  to_open="$tmp/$target_as_path.dot.svg"
else
  pkg="$target"
fi

echo "graphing package: '$pkg'"
# shellcheck disable=SC2207
input=($(find "$pkg"/* -name "*.dot" -not \( -path '*runfiles/*' \)))
for i in "${input[@]}"
do
  src="$i"
  dest="$tmp/$src.svg"
  echo "$src"
  mkdir -p "$(dirname "$dest")"
  dot -Tsvg -o "$dest" "$(pwd)/$src"
done

if [[ -n "${to_open:-}" ]]; then
  open -a "Google Chrome" "$to_open"
fi
