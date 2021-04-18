package main

import (
	"fmt"
	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"github.com/samhowes/my_rules_dotnet/tests/tools/executable"
	"github.com/samhowes/my_rules_dotnet/tests/tools/files"
	"github.com/stretchr/testify/assert"
	"github.com/termie/go-shutil"
	"io/ioutil"
	"os"
	"path"
	"runtime"
	"strings"
	"testing"
)

const GREETER = "Greeter"

func TestRunGenrule(t *testing.T) {
	es := os.Environ()
	for _, e := range es {
		fmt.Println(e)
	}

	contentBytes, err := ioutil.ReadFile(files.Path("run_greeter.txt"))
	if err != nil {
		t.Fatalf("failed to read genrule result: %v", err)
	}
	actual := files.Endings(string(contentBytes))
	assert.Equal(t, "Hello: genrule!\n", actual)
}

func greeterName() string {
	return files.BinName("Greeter")
}

func TestRunDataDep(t *testing.T) {
	config := lib.TestConfig{
		Args:           []string{"data dep"},
		ExpectedOutput: "Hello: data dep!\n",
		Target:         files.BinPath(GREETER),
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

	binPath := path.Join(path.Dir(fakeRunfilesDir), files.BinName(GREETER))
	newPath, err := shutil.Copy(files.BinPath(GREETER), binPath, true)
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
		Target:         files.BinPath(GREETER),
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

	expectedRunfiles := files.ComputeRunfilesDir(os.Args[0])

	fmt.Println(expectedRunfiles)

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
