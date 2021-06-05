package build_test

import (
	"encoding/json"
	"fmt"
	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"github.com/samhowes/my_rules_dotnet/tests/tools/executable"
	"github.com/samhowes/my_rules_dotnet/tests/tools/files"
	"os"
	"path"
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
		config.Target = files.Path(path.Base("%target%"))
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
	// go_test starts us in our runfiles_tree (on unix) so we can base our assertions off of the current directory
	for dir, filesA := range config.Data["expectedFiles"].(map[string]interface{}) {
		if len(dir) > 0 && dir[0] == '@' {
			t.Fatalf("external?")
			parts := strings.SplitN(dir[1:], "/", 2)
			//workspace = parts[0]
			if len(parts) == 2 {
				dir = parts[1]
			} else {
				dir = ""
			}
		}

		fmt.Printf("%s\n", dir)
		expectedFiles := filesA.([]interface{})
		for _, fA := range expectedFiles {
			f := fA.(string)

			shouldExist := true
			if f[0] == '!' {
				shouldExist = false
				f = f[1:]
			}

			fullPath := path.Join(dir, f)

			ext := path.Ext(f)
			if !config.Debug && ext == ".pdb" || !config.Diag && ext == ".binlog" {
				continue
			}

			fullPath, err := bazel.Runfile(path.Join(dir, f))
			if shouldExist && err != nil {
				t.Errorf("Failed to find runfile %s: %v", fullPath, err)
				continue
			}

			fmt.Printf(fullPath + "\n")
			_, err = os.Stat(fullPath)
			if !shouldExist && err == nil {
				t.Errorf("expected file to not exist: \n%s", fullPath)
			} else if shouldExist && err != nil {
				t.Errorf("error finding expected file '%s':\n%v", fullPath, err)
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
