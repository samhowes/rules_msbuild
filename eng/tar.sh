#!/usr/bin/env bash
set -euo pipefail

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
base_out="$(basename "$tarfile")"
base_out="${base_out%.*}"
base_out="${base_out%.*}"
git ls-files > "$tarfile".files

# -h to not have the files be symlinks
tar -czf "$tarfile" -h -T "$tarfile.files"

case "$(uname)" in
  MSYS*)
    shautil="sha256sum"
    ;;
  *)
    shautil="shasum -a 256"
    ;;
esac

sha=$($shautil "$tarfile" | cut -d " " -f1)

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
  sed "s|download/.*.tar.gz|download/$tag/$base_out-$tag.tar.gz|" "$f" > "$o"
  replace -E "s|sha256 = \"[0-9a-f]+\"|sha256 = \"$sha\"|g" "$o"
done
