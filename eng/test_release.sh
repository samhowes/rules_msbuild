#!/bin/bash

bazel run //eng/release

if [[ -d "tmp" ]]; then rm -rf tmp; fi

mkdir tmp
cp bazel-bin/rules_msbuild.tar.gz tmp
pushd tmp || exit
tar -xzvf rules_msbuild.tar.gz
