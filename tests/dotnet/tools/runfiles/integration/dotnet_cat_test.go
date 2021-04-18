package main

import (
	"github.com/samhowes/my_rules_dotnet/tests/tools/lib"
	"io/ioutil"
	"testing"
)

const expected = "Hello Runfiles!\n"

func TestRunGenrule(t *testing.T) {
	contentBytes, err := ioutil.ReadFile("run_dotnet_cat_result.txt")
	if err != nil {
		t.Fatalf("failed to read genrule result: %v", err)
	}
	actual := string(contentBytes)
	if actual != expected {
		t.Errorf("Wrong file contents:\nExpected: '%s'\nActual: '%s'", expected, actual)
	}
}

func TestRunDataDep(t *testing.T) {
	config := lib.TestConfig{
		ExpectedOutput: expected,
		Target:         "dotnet_cat",
	}
	lib.CheckExecutableOutput(t, &config)
}
