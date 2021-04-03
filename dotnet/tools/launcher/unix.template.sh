#!/usr/bin/env bash
# --- begin runfiles.bash initialization v2 ---
# Copy-pasted from the Bazel Bash runfiles library v2.
set -uo pipefail
#f=bazel_tools/tools/bash/runfiles/runfiles.bash
## did the parent process set the directory?
#source "${RUNFILES_DIR:-/dev/null}/$f" 2>/dev/null ||
#  #(windows) did the parent process set the file?
#  source "$(grep -sm1 "^$f " "${RUNFILES_MANIFEST_FILE:-/dev/null}" | cut -f2- -d' ')" 2>/dev/null ||
#  # are we the primary process?
#  source "$0.runfiles/$f" 2>/dev/null ||
#  # (windows) are we the pimary process?
#  source "$(grep -sm1 "^$f " "$0.runfiles/MANIFEST" | cut -f2- -d' ')" 2>/dev/null ||
#  source "$(grep -sm1 "^$f " "$0.exe.runfiles/MANIFEST" | cut -f2- -d' ')" 2>/dev/null ||
#  {
#    echo >&2 "ERROR: cannot find $f"
#    exit 1
#  }
# derived from: https://github.com/bazelbuild/bazel/blob/master/src/main/java/com/google/devtools/build/lib/bazel/rules/java/java_stub_template.txt

die() {
  printf "%s: $1\n" "$0" "${@:2}" >&2
  exit 1
}

# Windows
function is_windows() {
  [[ "${OSTYPE}" =~ msys* ]] || [[ "${OSTYPE}" =~ cygwin* ]]
}

# macOS
function is_macos() {
  [[ "${OSTYPE}" =~ darwin* ]]
}

# Find our runfiles tree.  We need this to construct the classpath
# (unless --singlejar was passed).
#
# Call this program X.  X was generated by a java_binary or java_test rule.
# X may be invoked in many ways:
#   1a) directly by a user, with $0 in the output tree
#   1b) via 'bazel run' (similar to case 1a)
#   2) directly by a user, with $0 in X's runfiles tree
#   3) by another program Y which has a data dependency on X, with $0 in Y's runfiles tree
#   4) via 'bazel test'
#   5) by a genrule cmd, with $0 in the output tree
#   6) case 3 in the context of a genrule
#
# For case 1, $0 will be a regular file, and the runfiles tree will be
# at $0.runfiles.
# For case 2, $0 will be a symlink to the file seen in case 1.
# For case 3, we use Y's runfiles tree, which will be a superset of X's.
# For case 4, $RUNFILES_DIR and $TEST_SRCDIR should already be set.
# Case 5 is handled like case 1.
# Case 6 is handled like case 3.

# If we are running on Windows, convert the windows style path
# to unix style for detecting runfiles path.
if is_windows; then
  self=$(cygpath --unix "$0")
else
  self="$0"
fi

if [[ "$self" != /* ]]; then
  self="$PWD/$self"
fi

if [[ -z "${RUNFILES_DIR:-}" ]]; then
  while true; do
    if [[ -e "$self.runfiles" ]]; then
      RUNFILES_DIR="$self.runfiles"
      break
    fi
    if [[ $self == *.runfiles/* ]]; then
      RUNFILES_DIR="${self%.runfiles/*}.runfiles"
      break
    fi
    if [[ ! -L "$self" ]]; then
      break
    fi
    readlink="$(readlink "$self")"
    if [[ "$readlink" == /* ]]; then
      self="$readlink"
    else
      # resolve relative symlink
      self="${self%/*}/$readlink"
    fi
  done
  if [[ -z "$RUNFILES_DIR" ]]; then
    die 'Cannot locate runfiles directory. (Set $RUNFILES_DIR to inhibit searching.)'
  fi
fi
export RUNFILES_DIR
export RUNFILES_MANIFEST_FILE="${RUNFILES_DIR}/MANIFEST"

runfiles_tools=bazel_tools/tools/bash/runfiles/runfiles.bash
if is_windows; then
  source "$(grep -sm1 "^$runfiles_tools " "$RUNFILES_MANIFEST_FILE" | cut -f2- -d' ')"
else
  source "$RUNFILES_DIR/$runfiles_tools"
fi
set -e
# --- end runfiles.bash initialization v2 ---

# --- begin my_rules_dotnet code
target_bin_path="$(rlocation %target_bin_path%)"
dotnet_bin_path="$(rlocation %dotnet_bin_path%)"

if [[ "${DOTNET_LAUNCHER_DEBUG:-}" == 1 ]]; then
  echo "INFO[dotnet.launcher]: target_bin=target_bin_path"
  echo "INFO[dotnet.launcher]: dotnet_bin=dotnet_bin_path"
fi

# environment variables for the dotnet executable
%dotnet_env%

$dotnet_bin_path exec "$target_bin_path" "$@"
