#!/usr/bin/env bash

function replace() {
  args=( "$@" )
  if [[ "$(uname)" == *"Darwin"* ]]; then
      args=("-i" '' "${args[@]}")
  fi
  echo "${args[@]}"
}


replace "s|download/.*.tar.gz|download/\$tag/\$tarfile|" "\$f"