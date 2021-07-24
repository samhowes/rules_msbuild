package dotnet

import (
	"bytes"
	"fmt"
	"io/ioutil"
	"os"
	"os/exec"
	"path"
	"path/filepath"
	"strings"
	"testing"

	"github.com/bazelbuild/bazel-gazelle/testtools"
	"github.com/bazelbuild/rules_go/go/tools/bazel"
)

var gazellePath = findGazelle()

const testDataPath = "gazelle/dotnet/testdata/"

// TestGazelleBinary runs a gazelle binary with dotnet installed on each
// directory in `testdata/*`. Please see `testdata/README.md` for more
// information on each test.
func TestGazelleBinary(t *testing.T) {
	tests := map[string][]bazel.RunfileEntry{}
	repo_tests := map[string][]bazel.RunfileEntry{}

	testTypeMap := map[string]map[string][]bazel.RunfileEntry{}

	files, err := bazel.ListRunfiles()
	if err != nil {
		t.Fatalf("bazel.ListRunfiles() error: %v", err)
	}
	for _, f := range files {
		if !strings.HasPrefix(f.ShortPath, testDataPath) {
			continue
		}

		relativePath := strings.TrimPrefix(f.ShortPath, testDataPath)
		parts := strings.SplitN(relativePath, "/", 2)
		if len(parts) < 2 {
			// This file is not a part of a testcase since it must be in a dir that
			// is the test case and then have a path inside of that.
			continue
		}

		d := parts[0]

		if strings.Index(d, ".") > 0 {
			parts = strings.SplitN(parts[1], "/", 2)
			d = path.Join(d, parts[0])
		}

		tMap, exists := testTypeMap[d]
		if !exists {
			testTypeMap[d] = tests
			tMap = tests
		}

		if strings.HasSuffix(f.Path, "WORKSPACE.out") || strings.HasSuffix(f.Path, ".bzl.out") {
			testTypeMap[d] = repo_tests
			tMap = repo_tests
			// unless all the files happen to be after "W" or the iteration isn't in alphabetical order, then we will
			// have accumulated test files in tests, move them to repo_tests
			tArray := tests[d]
			delete(tests, d)
			repo_tests[d] = tArray
		}

		tMap[d] = append(tMap[d], f)
	}

	if len(tests) == 0 || len(repo_tests) == 0 {
		t.Fatal("one of tests or repo tests not found")
	}

	for testName, files := range tests {
		testPath(t, testName, false, files)
	}

	for testName, files := range repo_tests {
		testPath(t, testName, true, files)
	}
}

func testPath(t *testing.T, name string, repos bool, files []bazel.RunfileEntry) {
	t.Run(name, func(t *testing.T) {
		var args []string
		baseName := strings.SplitN(name, "/", 2)[0]
		parts := strings.Split(baseName, ".")
		if len(parts) > 1 {
			args = append(args, fmt.Sprintf("--%s=%s", parts[0], parts[1]))
		}

		var inputs []testtools.FileSpec
		var goldens []testtools.FileSpec

		for _, f := range files {
			path := f.Path
			trim := testDataPath + name + "/"
			shortPath := strings.TrimPrefix(f.ShortPath, trim)
			info, err := os.Stat(path)
			if err != nil {
				t.Fatalf("os.Stat(%q) error: %v", path, err)
			}

			// Skip dirs.
			if info.IsDir() {
				continue
			}

			content, err := ioutil.ReadFile(path)
			if err != nil {
				t.Errorf("ioutil.ReadFile(%q) error: %v", path, err)
			}
			// Now trim the common prefix off.
			if strings.HasSuffix(shortPath, ".in") {
				inputs = append(inputs, testtools.FileSpec{
					Path:    strings.TrimSuffix(shortPath, ".in"),
					Content: string(content),
				})
			} else if strings.HasSuffix(shortPath, ".out") {
				goldens = append(goldens, testtools.FileSpec{
					Path:    strings.TrimSuffix(shortPath, ".out"),
					Content: string(content),
				})
			} else {
				inputs = append(inputs, testtools.FileSpec{
					Path:    shortPath,
					Content: string(content),
				})
				goldens = append(goldens, testtools.FileSpec{
					Path:    shortPath,
					Content: string(content),
				})
			}
		}

		dir, cleanup := testtools.CreateFiles(t, inputs)
		if false {
			defer cleanup()
		}

		if !repos {
			runCommand(t, dir, args...)
		} else {
			args = append(args, "-deps_macro=deps/nuget.bzl%nuget_deps")
			runCommand(t, dir, args...)
		}

		testtools.CheckFiles(t, dir, goldens)
		if t.Failed() {
			filepath.Walk(dir, func(path string, info os.FileInfo, err error) error {
				if err != nil {
					return err
				}
				t.Logf("%q exists", path)
				return nil
			})
		}
	})
}

func runCommand(t *testing.T, dir string, args ...string) {
	args = append(args, "-build_file_name=BUILD")
	cmd := exec.Command(gazellePath, args...)
	var stdout bytes.Buffer
	cmd.Stdout = os.Stdout
	var stderr bytes.Buffer
	cmd.Stderr = &stderr
	cmd.Dir = dir

	err := cmd.Run()

	t.Log(stdout.String())
	t.Log(stderr.String())
	if err != nil {
		t.Fatal(err)
	}
}

func findGazelle() string {
	gazellePath, ok := bazel.FindBinary("gazelle/dotnet", "gazelle-dotnet")
	if !ok {
		panic("could not find gazelle binary")
	}
	return gazellePath
}
