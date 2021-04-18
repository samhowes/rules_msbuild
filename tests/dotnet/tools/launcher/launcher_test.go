package main

import (
	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"github.com/samhowes/my_rules_dotnet/tests/tools/lib"
	"github.com/stretchr/testify/assert"
	"github.com/termie/go-shutil"
	"io/ioutil"
	"os"
	"path"
	"runtime"
	"strings"
	"testing"
)

func TestRunGenrule(t *testing.T) {
	contentBytes, err := ioutil.ReadFile("run_greeter.txt")
	if err != nil {
		t.Fatalf("failed to read genrule result: %v", err)
	}
	actual := string(contentBytes)
	expected := "Hello: genrule!\n"
	if actual != expected {
		t.Errorf("Wrong file contents:\nExpected: '%s'\nActual: '%s'", expected, actual)
	}
}

func greeterName() string {
	name := "Greeter"
	if runtime.GOOS == "windows" {
		name = name + ".exe"
	}
	return name
}

func TestRunDataDep(t *testing.T) {
	config := lib.TestConfig{
		Args:           []string{"data dep"},
		ExpectedOutput: "Hello: data dep!\n",
		Target:         greeterName(),
	}
	lib.CheckExecutableOutput(t, &config)
}

// Simulate the user executing the binary directly given that they have built it directly
func TestRunDirect(t *testing.T) {
	tmpDir, err := bazel.NewTmpDir("direct")
	if err != nil {
		t.Fatalf("failed to create tmp dir: %v", err)
	}

	fakeRunfilesDir := path.Join(tmpDir, greeterName()+".runfiles")
	cwd, _ := os.Getwd()
	runfilesIndex := strings.Index(cwd, ".runfiles")
	thisRunfilesDir := cwd[0 : runfilesIndex+len(".runfiles")]

	err = shutil.CopyTree(thisRunfilesDir, fakeRunfilesDir, nil)
	t.Logf("created runfiles tree at: %s\n", fakeRunfilesDir)
	if err != nil {
		t.Fatalf("failed to create new runfiles tree: %v", err)
	}

	binPath := path.Join(path.Dir(fakeRunfilesDir), greeterName())
	newPath, err := shutil.Copy(greeterName(), binPath, true)
	if err != nil {
		t.Fatalf("failed to copy executable: %v", err)
	}
	if newPath != binPath {
		t.Fatalf("incorrect copy:\nexpected: '%s'\nactual: '%s'", binPath, newPath)
	}

	t.Logf("%s\n", newPath)

	config := lib.TestConfig{
		Args:           []string{"direct"},
		ExpectedOutput: "Hello: direct!\n",
		// the user will execute the binary directly in the bazel-bin tree
		Target: binPath,
		Cwd:    path.Dir(binPath),
	}
	lib.CheckExecutableOutput(t, &config)
}

func TestBootstrapEnvVars(t *testing.T) {
	config := lib.TestConfig{
		Args:           []string{},
		ExpectedOutput: "",
		Target:         greeterName(),
	}
	lib.CheckExecutableOutput(t, &config)
	env := make(map[string]string)

	for _, line := range strings.Split(config.Result, "*~*") {
		if line == "" {
			continue
		}
		parts := strings.Split(line, "=")
		env[parts[0]] = parts[1]
	}

	_ = checkEnv(t, env, "RUNFILES_DIR")
	_ = checkEnv(t, env, "DOTNET_MULTILEVEL_LOOKUP")

	actualRunfiles := checkEnv(t, env, "RUNFILES_DIR")
	expectedRunfiles := os.Args[0]
	endIndex := strings.Index(expectedRunfiles, ".runfiles") + len(".runfiles")
	expectedRunfiles = expectedRunfiles[0:endIndex]
	assert.Equal(t, expectedRunfiles, actualRunfiles)

	if runtime.GOOS == "windows" {
		manifestFile := checkEnv(t, env, "RUNFILES_MANIFEST_FILE")
		assert.Equal(t, actualRunfiles+"/MANIFEST", manifestFile)
	}
}

func checkEnv(t *testing.T, env map[string]string, key string) string {
	value, ok := env[key]
	assert.True(t, ok, "missing required environment key: '%s'", value)
	return value
}
