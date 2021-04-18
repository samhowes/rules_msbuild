package lib

import (
	"bytes"
	"github.com/samhowes/my_rules_dotnet/tests/tools/files"
	"github.com/stretchr/testify/assert"
	"golang.org/x/sys/execabs"
	"os"
	"path"
	"path/filepath"
	"regexp"
	"sync"
	"testing"
)

type TestConfig struct {
	Data           map[string]interface{}
	Args           []string
	ExpectedOutput string
	JsonText       string
	Target         string
	Once           sync.Once
	Cwd            string
	Result         string
}

func CheckExecutableOutput(t *testing.T, config *TestConfig) {
	abspath, err := filepath.Abs(config.Target)
	if err != nil {
		t.Fatalf("bad executable path %s:%v", path.Base(config.Target), err)
	}

	cmd := execabs.Command(abspath, config.Args...)

	if config.Cwd != "" {
		cmd.Dir = config.Cwd
	}
	cmd.Env = os.Environ()
	cmd.Stdin = nil
	var stdout bytes.Buffer
	cmd.Stdout = &stdout
	var stderr bytes.Buffer
	cmd.Stderr = &stderr

	if err := cmd.Start(); err != nil {
		t.Fatalf("failed to launch command: %s\n%v", cmd.String(), err)
	}

	state, err := cmd.Process.Wait()
	if err != nil {
		t.Errorf("failed to wait on cmd %s\n%v", cmd.String(), err)
	}

	if !state.Success() {
		t.Errorf("command did not complete successfully: %s\n", state.String())
	}

	actualOut := files.Endings(stdout.String())
	config.Result = actualOut
	if config.ExpectedOutput == "" {
		return
	}

	if config.ExpectedOutput[0] == '%' {
		expr := config.ExpectedOutput[1:]
		matched, err := regexp.Match(expr, []byte(actualOut))
		assert.NoError(t, err, "error matching output")
		assert.True(t, matched, "failed to match command output")
	} else {
		assert.Equal(t, config.ExpectedOutput, actualOut, "incorrect command output")
	}

	actualErr := files.Endings(stderr.String())
	assert.Empty(t, actualErr, "command had errors")

	t.Logf("Stdout: \n'%s'", actualOut)
}
