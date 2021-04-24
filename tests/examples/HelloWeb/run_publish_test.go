package main

import (
	"github.com/samhowes/my_rules_dotnet/tests/tools/executable"
	"github.com/samhowes/my_rules_dotnet/tests/tools/files"
	"os"
	"path"
	"testing"
)

func TestRunPublishOutput(t *testing.T) {
	dotnetPath := files.BinPath("@dotnet")
	config := lib.TestConfig{
		Args:           []string{files.Path("publish/netcoreapp3.1/HelloWeb.dll")},
		ExpectedOutput: "The special value is: 42!\n",
		Target:         dotnetPath,
	}
	os.Setenv("DOTNET_CLI_HOME", path.Dir(dotnetPath))
	t.Logf("dotnet path: %s\n", config.Target)
	lib.CheckExecutableOutput(t, &config)
}
