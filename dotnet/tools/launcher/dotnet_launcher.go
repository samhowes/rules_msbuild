package main

import (
	"fmt"
	"log"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"sync"

	"golang.org/x/sys/execabs"
)

var ctx = struct {
	once  sync.Once
	debug bool
}{}

func diag(msg func()) {
	ctx.once.Do(initDiag)
	if ctx.debug {
		msg()
	}
}

func initDiag() {
	ctx.debug = os.Getenv("DOTNET_LAUNCHER_DEBUG") != ""
}

func LaunchDotnet(args []string, info *LaunchInfo) {
	dotnetEnv := info.GetItem("dotnet_env")

	for _, line := range strings.Split(dotnetEnv, ";") {
		equals := strings.IndexRune(line, '=')
		if equals <= 0 {
			panic(fmt.Errorf("malformed dotnet environment line: %s", line))
		}
		_ = os.Setenv(line[0:equals], line[equals+1:])
	}

	workspace := info.GetItem("workspace_name")
	pkg := info.GetItem("package")
	_ = os.Setenv("DOTNET_RUNFILES_WORKSPACE", workspace)
	_ = os.Setenv("DOTNET_RUNFILES_PACKAGE", pkg)

	dotnetBinPath := info.GetPathItem("dotnet_bin_path")
	dotnetCmd := info.GetItem("dotnet_cmd")
	dotnetArgs := append([]string{dotnetBinPath, dotnetCmd}, info.GetListItem("dotnet_args")...)
	targetBinPath := info.GetBuiltPath("target_bin_path")
	assemblyArgs := append([]string{targetBinPath}, info.GetListItem("assembly_args")...)
	assemblyArgs = append(assemblyArgs, args[1:]...)

	if dotnetCmd == "test" {
		xmlFile := os.Getenv("XML_OUTPUT_FILE")
		if xmlFile == "" {
			xmlFile = "test.xml"
		}
		loggerArg := fmt.Sprintf("%s;%s=%s",
			info.GetItem("dotnet_logger"),
			info.GetItem("log_path_arg_name"),
			xmlFile,
		)
		assemblyArgs = append(assemblyArgs, "--logger", loggerArg)
	}

	newArgs := append(dotnetArgs, assemblyArgs...)

	diag(func() { fmt.Printf("==> launching: \"%s\"\n", strings.Join(newArgs, "\" \"")) })
	launch(info, newArgs)
}

func LaunchDotnetPublish(args []string, info *LaunchInfo) {
	assembly := args[0]
	if strings.HasSuffix(assembly, ".exe") {
		assembly = assembly[:len(assembly)-len(".exe")]
	}
	assembly = assembly + ".dll"

	dotnetHome := os.Getenv("DOTNET_CLI_HOME")
	var dotnet string
	if dotnetHome != "" {
		dotnet = filepath.Join(dotnetHome, "dotnet")
	} else {
		dotnetPath, err := exec.LookPath("dotnet")
		dotnet = dotnetPath
		if err != nil {
			log.Panicf("Could not find 'dotnet' on PATH. Set the environment variable DOTNET_CLI_HOME or install a dotnet runtime. https://dotnet.microsoft.com/download")
		}
	}

	newArgs := append([]string{
		dotnet,
		"exec",
		assembly,
	}, args[1:]...)
	launch(info, newArgs)
}

func launch(info *LaunchInfo, args []string) {
	launchMode, ok := info.Data["launch_mode"]
	if !ok {
		launchMode = "wait"
	}
	cmd := execabs.Command(args[0], args[1:]...)

	cmd.Env = os.Environ()
	cmd.Stdin = os.Stdin
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr

	if err := cmd.Start(); err != nil {
		panic(fmt.Errorf("failed to launch command: %s\n%v", cmd.String(), err))
	}

	var code int
	diag(func() { fmt.Printf("Started PID %d\n", cmd.Process.Pid) })
	if launchMode == "wait" {
		// when bazel runs a command, it will only pay attention to the parent process, not the child, so we need to
		// wait on the cmd for bazel to report out on it
		diag(func() { fmt.Printf("waiting...\n") })
		state, err := cmd.Process.Wait()
		if err != nil {
			panic(fmt.Errorf("failed to wait on cmd %s\n%v", cmd.String(), err))
		}
		diag(func() { fmt.Printf("cmd completed: %s\n", state.String()) })
		code = state.ExitCode()
		os.Exit(code)
	} else {
		if err := cmd.Process.Release(); err != nil {
			panic(fmt.Errorf("failed to detach from launched command %s\n%v", cmd.String(), err))
		}
		diag(func() { fmt.Printf("released\n") })
	}
}
