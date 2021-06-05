package build_test

import (
	"encoding/json"
	"fmt"
	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"github.com/samhowes/my_rules_dotnet/tests/tools/executable"
	"github.com/samhowes/my_rules_dotnet/tests/tools/files"
	"github.com/stretchr/testify/assert"
	"io/fs"
	"os"
	"path"
	"path/filepath"
	"strings"
	"testing"
)

var config = lib.TestConfig{}

func initConfig(t *testing.T, config *lib.TestConfig) {
	config.Once.Do(func() {
		var data interface{}

		config.JsonText = files.Endings(`%config_json%`)
		config.ExpectedOutput = files.Endings(`%expected_output%`)

		err := json.Unmarshal([]byte(config.JsonText), &data)
		if err != nil {
			t.Fatalf("failed to get test config: %v", err)
		}

		config.Data = data.(map[string]interface{})

		var args []string
		argText := `%args%`
		err = json.Unmarshal([]byte(argText), &args)
		if err != nil {
			t.Fatalf("failed to parse executable args: %v", err)
		}

		config.Args = args

		for k := range config.Data {
			fmt.Println(k)
		}
		config.Package = `%package%`
		if config.ExpectedOutput != "" {
			config.Target = path.Base("%target%")
			config.Target, err = bazel.Runfile(path.Join(config.Package, config.Target))
			if err != nil {
				t.Fatalf("failed to find target: %v", err)
			}
		}

		config.RunLocation = `%run_location%`
		config.Debug = strings.ToLower(`%compilation_mode%`) == "dbg"
		config.Diag = strings.ToLower(`%diag%`) == "1"

		fmt.Println(config.Target)
	})
}

func TestBuildOutput(t *testing.T) {
	initConfig(t, &config)
	cwd, _ := os.Getwd()
	fmt.Printf("Current working directory: \n%s\n", cwd)
	fmt.Printf("target: %s\n", config.Target)

	exitingFiles := map[string]bool{}
	runfiles, _ := bazel.ListRunfiles()

	relpath := func(p string) string {
		if os.PathSeparator == '\\' {
			p = strings.Replace(p, "\\", "/", -1)
		}

		for _, prefix := range []string{"/bin/", "/my_rules_dotnet/", config.Package + "/"} {
			index := strings.Index(p, prefix)
			if index >= 0 {
				p = p[index+len(prefix):]
			}
		}
		return p
	}

	for _, f := range runfiles {
		if exitingFiles[relpath(f.ShortPath)] {
			continue
		}
		_ = filepath.WalkDir(f.Path, func(p string, info fs.DirEntry, err error) error {

			p = relpath(p)
			exitingFiles[p] = true
			t.Logf(p)

			return nil
		})
	}

	// go_test starts us in our runfiles_tree (on unix) so we can base our assertions off of the current directory
	for dir, filesA := range config.Data["expectedFiles"].(map[string]interface{}) {
		t.Log(dir)
		expectedFiles := filesA.([]interface{})
		for _, fA := range expectedFiles {
			f := fA.(string)

			ext := path.Ext(f)
			switch ext {
			case ".pdb":
				if !config.Debug {
					continue
				}
				break
			case ".binlog":
			case ".dot":
				if !config.Diag {
					continue
				}
			}

			shouldExist := true
			if f[0] == '!' {
				shouldExist = false
				f = f[1:]
			}

			fullPath := path.Join(dir, f)
			fmt.Printf(fullPath + "\n")
			_, exists := exitingFiles[fullPath]
			if shouldExist {
				assert.Equal(t, true, exists, "expected file to exist: %s", fullPath)
			} else {
				assert.Equal(t, false, exists, "expected file not to exist: %s", fullPath)
			}
		}
	}
}

func TestExecutableOutput(t *testing.T) {
	initConfig(t, &config)
	if config.ExpectedOutput == "" {
		t.Skip("No output requested")
	}

	if config.RunLocation == "standard" {
		config.Target = lib.SetupFakeRunfiles(t, path.Base(config.Target))
		config.Cwd = files.ComputeStartingDir(config.Target)
		t.Logf("Computed starting dir to: \n%s", config.Cwd)
	}

	lib.CheckExecutableOutput(t, &config)
}
