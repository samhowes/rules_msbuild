// The go runfiles functions index the manifest by shortpath. we want workspace_name/shortpath

package main

import (
	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"github.com/stretchr/testify/assert"
	"io/ioutil"
	"os"
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
	runfiles := &Runfiles{strategy: &DirectoryStrategy{runfileDirectory: "foo.runfiles"}}

	rlocation := runfiles.Rlocation("my_workspace/foo/bar")
	assert.Equal(t, "foo.runfiles/my_workspace/foo/bar", rlocation)

	rlocation = runfiles.Rlocation("../external_workspace")
	assert.Equal(t, "foo.runfiles/external_workspace", rlocation)

	rlocation = runfiles.Rlocation("external/external_workspace")
	assert.Equal(t, "foo.runfiles/external_workspace", rlocation)
}
