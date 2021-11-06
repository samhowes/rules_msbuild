package build_test

import (
	"encoding/json"
	"errors"
	"fmt"
	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"github.com/samhowes/rules_msbuild/tests/tools/executable"
	"github.com/samhowes/rules_msbuild/tests/tools/files"
	"github.com/stretchr/testify/assert"
	"io/fs"
	"os"
	"path"
	"path/filepath"
	"runtime"
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
		config.Target, err = bazel.Runfile("%target%")
		if err != nil {
			t.Fatalf("failed to find target: %v", err)
		}

		config.RunLocation = `%run_location%`
		config.Debug = strings.ToLower(`%compilation_mode%`) == "dbg"
		config.Diag = strings.ToLower(`%diag%`) == "1"
	})
}

func TestBuildOutput(t *testing.T) {
	initConfig(t, &config)
	cwd, _ := os.Getwd()
	fmt.Printf("Current working directory: \n%s\n", cwd)
	fmt.Printf("target: %s\n", config.Target)

	ind := strings.Index(config.Target, config.Package)
	packageBin := config.Target[0 : ind+len(config.Package)]
	t.Logf("packageBin: %s", packageBin)
	err := os.Chdir(packageBin)
	assert.NoError(t, err)

	_ = filepath.WalkDir(packageBin, func(p string, info fs.DirEntry, err error) error {

		p = p[len(packageBin):]
		t.Logf(p)

		return nil
	})

	// go_test starts us in our runfiles_tree (on unix) so we can base our assertions off of the current directory
	for dir, filesA := range config.Data["expectedFiles"].(map[string]interface{}) {
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
			case ".dot", ".binlog":
				if !config.Diag {
					continue
				}
			case ".exe":
				if runtime.GOOS != "windows" {
					f = f[:len(f)-len(".exe")]
				}
			}

			shouldExist := true
			if f[0] == '!' {
				shouldExist = false
				f = f[1:]
			}

			fullPath := filepath.Join(dir, f)
			t.Logf(fullPath)
			_, err := os.Stat(fullPath)
			exists := !errors.Is(err, os.ErrNotExist)

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

	f, err := os.Stat(config.Target)
	assert.NoError(t, err)
	if f.IsDir() {
		config.Target = path.Join(config.Target, strings.TrimSuffix("%assembly_name%", ".dll"))
		if runtime.GOOS == "windows" {
			config.Target = config.Target + ".exe"
		}
	}

	lib.CheckExecutableOutput(t, &config)
}
