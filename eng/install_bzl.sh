#!/bin/bash
set -e

pkg="dotnet/tools/Bzl"

bazel build "//$pkg:SamHowes.Bzl.nupkg"

dotnet tool update -g Bzl --add-source "bazel-bin/$pkg"
