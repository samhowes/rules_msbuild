package main

import (
	"github.com/samhowes/my_rules_dotnet/tests/tools/executable"
	"github.com/samhowes/my_rules_dotnet/tests/tools/files"
	"github.com/stretchr/testify/assert"
	"io/ioutil"
	"testing"
)

const expected = "Hello Runfiles!\n"

func TestRunGenrule(t *testing.T) {
	contentBytes, err := ioutil.ReadFile(files.Path("run_dotnet_cat_result.txt"))
	if err != nil {
		t.Fatalf("failed to read genrule result: %v", err)
	}

	assert.Equal(t, expected, files.EndingsB(contentBytes))
}

func TestRunDataDep(t *testing.T) {
	config := lib.TestConfig{
		ExpectedOutput: expected,
		Target:         files.BinPath("DotnetCat"),
	}
	lib.CheckExecutableOutput(t, &config)
}
