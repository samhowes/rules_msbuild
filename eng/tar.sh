#!/usr/bin/env bash
set -e

ins=()
dest=()
tarfile=""
for ((i=1; i <= $#; i++))
do
  a="${!i}"
  if [[ $a == "--" ]]; then
    ins=("${dest[@]}")
    dest=()
    i=$((i+1));
    tarfile="${!i}"
    continue
  fi
  dest+=("$a")
done
outs=("${dest[@]}")

tag="0.0.1"
base_out="$(dirname "$tarfile")"
git ls-files > "$tarfile".files

tar -czf "$tarfile" -T "$tarfile.files"

sha=$(shasum -a 256 "$tarfile" | cut -d " " -f1)

function replace() {
  args=( "$@" )
  if [[ "$(uname)" == *"Darwin"* ]]; then
      args=("-i" '' "${args[@]}")
  else
      args=("-i" "${args[@]}")
  fi
  sed "${args[@]}"
}
n=${#ins[@]}
for ((i=0; i < n; i++))
do

  f="${ins[i]}"
  o="${outs[i]}"
  sed "s|download/.*.tar.gz|download/$tag/$tarfile|" "$f" > "$o"
  replace -E "s|sha256 = \"[0-9a-f]+\"|sha256 = \"$sha\"|g" "$o"
done
