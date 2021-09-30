#!/bin/bash
set -e

pkg="dotnet/tools/Bzl"

bazel build "//$pkg:SamHowes.Bzl_nuget"

dotnet tool update -g Bzl --add-source "bazel-bin/$pkg"
