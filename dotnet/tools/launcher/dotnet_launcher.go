package main

import (
	"fmt"
	"os"
	"path"
	"path/filepath"
	"runtime"
	"strings"
	"sync"

	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"golang.org/x/sys/execabs"
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

func trimWorkspaceName(value string) string {
	// trim the workspace: go runfiles indexes the manifest by shortpath, which go defines as not including the
	// workspace
	ind := strings.IndexByte(value, '/')
	value = value[ind+1:]
	return value
}

func getPathItem(l LaunchInfo, key string) string {
	value := trimWorkspaceName(getItem(l, key))
	return getRunfile(value)
}

func getRunfile(p string) string {
	fPath, err := bazel.Runfile(p)
	if err != nil {
		panic(fmt.Sprintf("missing required runfile path item %s, %v", p, err))
	}
	return fPath
}

// getBuiltPath assumes that key is a short_path to the output directory of an assembly built by rules_msbuild
// this means that the output directory is listed in the runfiles manifest, and since the output directory is a prefix
// of all the items in the output directory, the actual output items are not listed explicitly in the manifest
func getBuiltPath(l LaunchInfo, key string) string {
	outputDir := getItem(l, "output_dir")
	value := trimWorkspaceName(getItem(l, key))
	diag(func() { fmt.Printf("findng built path: %s using prefix %s\n", value, outputDir) })
	value = value[len(outputDir)+1:]
	outputDirPath := getRunfile(outputDir)
	return path.Join(outputDirPath, value)
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
			diag(func() {
				abs, _ := filepath.Abs(manifestPath)
				fmt.Printf("located manifest file: %s\n", abs)
			})
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

	workspace := getItem(info, "workspace_name")
	pkg := getItem(info, "package")
	_ = os.Setenv("DOTNET_RUNFILES_WORKSPACE", workspace)
	_ = os.Setenv("DOTNET_RUNFILES_PACKAGE", pkg)

	dotnetBinPath := getPathItem(info, "dotnet_bin_path")
	dotnetCmd := getItem(info, "dotnet_cmd")
	dotnetArgs := append([]string{dotnetBinPath, dotnetCmd}, getListItem(info, "dotnet_args")...)
	targetBinPath := getBuiltPath(info, "target_bin_path")
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
	launch(info, newArgs)
}

func launch(info LaunchInfo, args []string) {
	launchMode, ok := info["launch_mode"]
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
