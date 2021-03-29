#!/usr/bin/env bash

tar -cvzhf bazel-out.tar.gz --exclude=*.runfiles bazel-out
