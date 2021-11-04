// The go runfiles functions index the manifest by shortpath. we want workspace_name/shortpath

package main

import (
	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"github.com/stretchr/testify/assert"
	"io/fs"
	"io/ioutil"
	"os"
	"path"
	"path/filepath"
	"strings"
	"testing"
)

func TestRunfilesManifest(t *testing.T) {
	dataFile, err := ioutil.TempFile(bazel.TestTmpDir(), "TEST_MANIFEST.*.txt")
	assert.NoError(t, err)
	defer os.Remove(dataFile.Name())

	_, err = dataFile.WriteString("my_workspace/foo/bar /what/wow")
	assert.NoError(t, err)

	assert.NoError(t, os.Setenv(bazel.RUNFILES_MANIFEST_FILE, dataFile.Name()))
	assert.NoError(t, os.Setenv("RUNFILES_MANIFEST_ONLY", "1"))

	runfiles := GetRunfiles()

	rlocation := runfiles.Rlocation("my_workspace/foo/bar")
	assert.Equal(t, "/what/wow", rlocation)

	rlocation = runfiles.Rlocation("../my_workspace/foo/bar")
	assert.Equal(t, "/what/wow", rlocation)

	rlocation = runfiles.Rlocation("doesnotexist")
	assert.Equal(t, "", rlocation)
}

func TestRunfilesDirectory(t *testing.T) {
	dir := filepath.Join(bazel.TestTmpDir(), "foo.runfiles")
	dir, _ = filepath.Abs(dir)
	assert.NoError(t, os.MkdirAll(dir, fs.ModePerm))
	assert.NoError(t, os.Chdir(dir))
	os.Args[0] = "foo"

	assert.NoError(t, os.Setenv(bazel.RUNFILES_DIR, ""))
	assert.NoError(t, os.Setenv("RUNFILES_MANIFEST_ONLY", ""))
	assert.NoError(t, os.Setenv("RUNFILES_MANIFEST", ""))
	assert.NoError(t, os.Setenv("TEST_SRCDIR", ""))

	runfiles := GetRunfiles()

	rdir := strings.ReplaceAll(dir, "\\", "/")

	rlocation := runfiles.Rlocation("my_workspace/foo/bar")
	assert.Equal(t, path.Join(rdir, "my_workspace/foo/bar"), rlocation)

	rlocation = runfiles.Rlocation("../external_workspace")
	assert.Equal(t, path.Join(rdir, "external_workspace"), rlocation)

	rlocation = runfiles.Rlocation("external/external_workspace")
	assert.Equal(t, path.Join(rdir, "external_workspace"), rlocation)
}
