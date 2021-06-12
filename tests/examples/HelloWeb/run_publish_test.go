package main

import (
	"github.com/samhowes/rules_msbuild/tests/tools/executable"
	"github.com/samhowes/rules_msbuild/tests/tools/files"
	"os"
	"path"
	"testing"
)

func TestRunPublishOutput(t *testing.T) {
	dotnetPath := files.BinPath("@dotnet")
	publishDir := files.Path("publish/netcoreapp3.1")
	if publishDir == "" {
		t.Fatalf("can't locate publish directory")
	}
	assemblyPath := path.Join(publishDir, "HelloWeb.dll")

	config := lib.TestConfig{
		Args:           []string{assemblyPath},
		ExpectedOutput: "The special value is: 42!\n",
		Target:         dotnetPath,
	}
	os.Setenv("DOTNET_CLI_HOME", path.Dir(dotnetPath))
	os.Setenv("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1")
	os.Setenv("DOTNET_MULTILEVEL_LOOKUP", "0")
	os.Setenv("DOTNET_NOLOGO", "0")
	t.Logf("dotnet path: %s\n", config.Target)
	lib.CheckExecutableOutput(t, &config)
}
