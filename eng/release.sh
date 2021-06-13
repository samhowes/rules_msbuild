#!/usr/bin/env bash

set -e
if [[ ! -f WORKSPACE ]]; then echo >&2 "not at root"; exit 1; fi

tag="$1"

if [[ "${tag:-}" == "" ]]; then
  echo "provide a tag as an argument"
  exit 1
elif [[ "${2:-}" == "clean" ]]; then
  set +e
  gh release delete "$tag" -y
  git push --delete origin "$tag"
  exit 0
fi

echo "Checking for existing release..."
function get_url() {
  gh release view 0.0.1 --json tarballUrl --jq .tarballUrl 2> /dev/null || echo ""
}
url=$(get_url)

if [[ "$url" == "" ]]; then
  echo "Creating release $tag"
  gh release create "$tag" -F ReleaseNotes.md
  url=$(get_url)
else
  echo "Release exists"
fi

# definitely a hack to download and re-upload, but that way I don't have to deal with
# excluding git and bazel files
echo "Downloading $url"
tarfile="rules_msbuild-$tag.tar.gz"
curl -L "$url" > "$tarfile"

echo "Re-uploading as $tarfile"
gh release upload "$tag" "$tarfile" --clobber
download_url=$(gh release view "$tag" --json assets --jq ".assets.[0].url")

