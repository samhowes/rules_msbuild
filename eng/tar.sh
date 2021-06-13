#!/usr/bin/env bash

if [[ ! -d "tmp" ]]; then mkdir tmp; fi

tag="0.0.1"
tarfile="rules_msbuild-$tag.tar.gz"
tmp_tar="tmp/$tarfile"

git ls-files > tmp/tar.files

tar -czvf "$tmp_tar" -T tmp/tar.files

sha=$(shasum -a 256 "$tmp_tar" | cut -d " " -f1)

for f in ReleaseNotes.md dotnet/tools/Bzl/WORKSPACE.tpl
do
  sed -i '' "s|download/.*.tar.gz|download/$tag/$tarfile|" "$f"
  sed -i '' -E "s|sha256 = \"[0-9a-f]+\"|sha256 = \"$sha\"|g" "$f"
done

