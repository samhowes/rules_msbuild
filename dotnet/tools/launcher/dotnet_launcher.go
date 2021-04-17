package main

import (
	"fmt"
	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"os"
	"path"
	"runtime"
	"strings"
	"sync"
	"syscall"
)

const runfilesSuffix = ".runfiles"

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

func getItem(l LaunchInfo, key string) string {
	value, present := l[key]
	if !present {
		panic(fmt.Sprintf("missing required launch data key: %s; %s", key, l))
	}
	return value
}

func getPathItem(l LaunchInfo, key string) string {
	value := getItem(l, key)
	fPath, err := bazel.Runfile(value)
	if err != nil {
		panic(fmt.Sprintf("missing required runfile path item %s, %v", value, err))
	}
	return fPath
}

func getListItem(l LaunchInfo, key string) []string {
	value := getItem(l, key)
	if value == "" {
		return []string{}
	}
	return strings.Split(value, "*~*")
}

func LaunchDotnet(args []string, info LaunchInfo) {
	runfilesDir := os.Getenv(bazel.RUNFILES_DIR)
	manifest := os.Getenv(bazel.RUNFILES_MANIFEST_FILE)
	testSrcDir := os.Getenv(bazel.TEST_SRCDIR)

	if runfilesDir == "" && manifest == "" && testSrcDir == "" {
		// scenarios for the runfiles locations on windows:
		// 1) user executed this binary directly => args[0].runfiles
		// 2) we are in another binary's runfiles directory, and they were not courteous enough to set our
		//		variables for us, _finger wag_ => search for .runfiles in the cwd

		thisBin := args[0]
		if runtime.GOOS == "windows" {
			thisBin = EnsureExe(thisBin)
		}
		runfilesDir = thisBin + runfilesSuffix
		dir, err := os.Stat(runfilesDir)
		if err != nil {
			cwd, err := os.Getwd()
			if err != nil {
				panic(fmt.Errorf("failed to find runfiles: %v", err))
			}
			index := strings.Index(cwd, runfilesSuffix)
			if index < 0 {
				panic(fmt.Errorf("no runfiles directories are a prefix of the current directory: %s", cwd))
			}
			runfilesDir = cwd[:index+len(runfilesSuffix)]
			dir, _ = os.Stat(runfilesDir)
		}

		if !dir.Mode().IsDir() {
			panic(fmt.Errorf("runfiles path is not a directory: %s", runfilesDir))
		}

		_ = os.Setenv(bazel.RUNFILES_DIR, runfilesDir)
		if runtime.GOOS == "windows" {
			manifestPath := path.Join(runfilesDir, "MANIFEST")
			_, err := os.Stat(manifestPath)
			if err != nil {
				panic(fmt.Errorf("failed to find manifest file path: %v", err))
			}
			// just assume we need to set the value for windows
			if err := os.Setenv(bazel.RUNFILES_MANIFEST_FILE, manifestPath); err != nil {
				panic(fmt.Errorf("failed to set the manifest file path: %v", err))
			}
			diag(func() { fmt.Printf("located manifest file: %s\n", manifestPath) })
		}
	}

	dotnetEnv := getItem(info, "dotnet_env")

	for _, line := range strings.Split(dotnetEnv, ";") {
		equals := strings.IndexRune(line, '=')
		if equals <= 0 {
			panic(fmt.Errorf("malformed dotnet environment line: %s", line))
		}
		_ = os.Setenv(line[0:equals], line[equals+1:])
	}

	dotnetBinPath := getPathItem(info, "dotnet_bin_path")
	dotnetCmd := getItem(info, "dotnet_cmd")
	dotnetArgs := append([]string{dotnetBinPath, dotnetCmd}, getListItem(info, "dotnet_args")...)
	targetBinPath := getPathItem(info, "target_bin_path")
	assemblyArgs := append([]string{targetBinPath}, getListItem(info, "assembly_args")...)
	assemblyArgs = append(assemblyArgs, args[1:]...)

	if dotnetCmd == "test" {
		xmlFile := os.Getenv("XML_OUTPUT_FILE")
		if xmlFile == "" {
			xmlFile = "test.xml"
		}
		loggerArg := fmt.Sprintf("%s;%s=%s",
			getItem(info, "dotnet_logger"),
			getItem(info, "log_path_arg_name"),
			xmlFile,
		)
		assemblyArgs = append(assemblyArgs, "--logger", loggerArg)
	}

	newArgs := append(dotnetArgs, assemblyArgs...)

	diag(func() { fmt.Printf("==> launching: \"%s\"\n", strings.Join(newArgs, "\" \"")) })

	newEnv := os.Environ()
	if err := syscall.Exec(newArgs[0], newArgs, newEnv); err != nil {
		panic(fmt.Errorf("failed to exec: %w", err))
	}
}
