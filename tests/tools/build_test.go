package main

import (
	"encoding/json"
	"fmt"
	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"github.com/samhowes/my_rules_dotnet/tests/tools/lib"
	"os"
	"path"
	"strings"
	"testing"
)

var config = lib.TestConfig{}

func initConfig(t *testing.T, config *lib.TestConfig) {
	config.Once.Do(func() {
		var data interface{}

		jsonText := `%config_json%`
		err := json.Unmarshal([]byte(jsonText), &data)
		if err != nil {
			t.Fatalf("failed to get test config: %v", err)
		}
		config.JsonText = jsonText
		config.Data = data.(map[string]interface{})
		config.ExpectedOutput = `%expected_output%`

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
		config.JsonText = jsonText
		config.Target = "%target%"
	})
}

func TestBuildOutput(t *testing.T) {
	initConfig(t, &config)

	fmt.Printf("target: %s\n", config.Target)
	// go_test starts us in our runfiles_tree (on unix) so we can base our assertions off of the current directory
	for dir, filesA := range config.Data["expectedFiles"].(map[string]interface{}) {
		useRunfiles := false
		//workspace := "my_rules_dotnet"

		if len(dir) > 0 && dir[0] == '@' {
			useRunfiles = true
			parts := strings.SplitN(dir[1:], "/", 2)
			//workspace = parts[0]
			if len(parts) == 2 {
				dir = parts[1]
			} else {
				dir = ""
			}
		}

		fmt.Printf("%s\n", dir)
		files := filesA.([]interface{})
		for _, fA := range files {
			f := fA.(string)

			shouldExist := true
			if f[0] == '!' {
				shouldExist = false
				f = f[1:]
			}

			fullPath := path.Join(dir, f)
			if useRunfiles {
				fullPath, err := bazel.Runfile(path.Join(dir, f))
				if shouldExist && err != nil {
					t.Errorf("Failed to find runfile %s: %v", fullPath, err)
					continue
				}
			}

			fmt.Printf(fullPath + "\n")
			_, err := os.Stat(fullPath)
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

	lib.CheckExecutableOutput(t, &config)
}
