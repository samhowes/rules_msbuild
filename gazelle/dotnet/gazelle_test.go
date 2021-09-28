package dotnet

import (
	"bytes"
	"io/ioutil"
	"os"
	"os/exec"
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
	filemap := map[string][]bazel.RunfileEntry{}
	//var tests []string
	//var repoTests []string

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

		testName := parts[0]

		fileList, exists := filemap[testName]
		if !exists {
			fileList = []bazel.RunfileEntry{}
			filemap[testName] = fileList
		}

		filemap[testName] = append(filemap[testName], f)
	}

	for testName, files := range filemap {
		testPath(t, testName, true, files)
	}
}

func testPath(t *testing.T, testName string, repos bool, files []bazel.RunfileEntry) {
	t.Run(testName, func(t *testing.T) {
		var args []string
		var inputs []testtools.FileSpec
		var goldens []testtools.FileSpec

		for _, f := range files {
			trim := testDataPath + testName + "/"
			shortPath := strings.TrimPrefix(f.ShortPath, trim)
			info, err := os.Stat(f.Path)
			if err != nil {
				t.Fatalf("os.Stat(%q) error: %v", f.Path, err)
			}

			// Skip dirs.
			if info.IsDir() {
				continue
			}

			content, err := ioutil.ReadFile(f.Path)
			if err != nil {
				t.Errorf("ioutil.ReadFile(%q) error: %v", f.Path, err)
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

		t.Logf("^^^^^^^^^^^%s^^^^^^^^^^", testName)
		for _, f := range inputs {
			t.Log(f.Path)
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

	t.Log(dir)
	t.Logf("%s", strings.Join(args, ","))
	t.Log("======================================")
	err := cmd.Run()

	t.Logf("stdout: %s", stdout.String())
	t.Logf("stderr: %s", stderr.String())
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
