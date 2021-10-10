#!/bin/bash

# --- begin runfiles.bash initialization v2 ---
# Copy-pasted from the Bazel Bash runfiles library v2.
set -uo pipefail; f=bazel_tools/tools/bash/runfiles/runfiles.bash
source "${RUNFILES_DIR:-/dev/null}/$f" 2>/dev/null || \
 source "$(grep -sm1 "^$f " "${RUNFILES_MANIFEST_FILE:-/dev/null}" | cut -f2- -d' ')" 2>/dev/null || \
 source "$0.runfiles/$f" 2>/dev/null || \
 source "$(grep -sm1 "^$f " "$0.runfiles_manifest" | cut -f2- -d' ')" 2>/dev/null || \
 source "$(grep -sm1 "^$f " "$0.exe.runfiles_manifest" | cut -f2- -d' ')" 2>/dev/null || \
 { echo>&2 "ERROR: cannot find $f"; exit 1; }; f=; set -e
# --- end runfiles.bash initialization v2 ---

echo "##[group]Artifacts"
status=0

suffix="$(uname -m)"
if [[ "$suffix" == "x86_64" ]]; then suffix="amd64"; fi;
suffix="$(uname -s)-$suffix"
suffix=$(echo "$suffix" | tr '[:upper:]' '[:lower:]')

for ((i=1; i <= $#; i++))
do
  target="${!i}"
  ((i=i+1))
  artifact_name="${target:2}"
  artifact_name="${artifact_name//\//.}"
  artifact_name="${artifact_name//:/.}"
  artifact="rules_msbuild/${!i}"
  artifact_path="$(rlocation $artifact)"
  echo "$target => $artifact_name"
  echo "  $artifact"
  echo "  $artifact_path"
  if [[ ! -f "$artifact_path" ]]; then
    echo "##[error]Artifact path does not exist"
    status=1
  fi;
  echo "##vso[artifact.upload containerfolder=binaries;artifactname=$suffix/$artifact_name]$artifact_path"
  echo
done
echo "##[endgroup]"

exit $status
