// Package launcher is a launcher utility for windows that takes care of locating the dotnet binary and the user's
// executable and starting both with the right arguments.
package main

import (
	"fmt"
	"os"
	"path"
	"path/filepath"
	"strings"
)

func main() {
	diag(func() { fmt.Printf("launcher args: %s\n", strings.Join(os.Args, ",")) })
	launchInfo, err := GetLaunchInfo(os.Args[0])
	if err != nil {
		panic(fmt.Sprintf("failed to get launch info: %s", err))
	}
	binaryType, present := launchInfo.Data["binary_type"]
	if !present {
		panic(fmt.Sprintf("no binary type in launch info: %s", launchInfo))
	}

	switch binaryType {
	case "Dotnet":
		launchInfo.Runfiles = GetRunfiles()
		LaunchDotnet(os.Args, launchInfo)
	case "DotnetPublish":
		// when we're published, our runfiles were made by rules_msbuild, and the directory is guaranteed to be next to
		// the assembly, no monkey business allowed

		binName := path.Base(launchInfo.GetItem("assembly_name"))
		dir, _ := filepath.Split(os.Args[0])
		runfilesDir := filepath.Join(dir, binName) + ".dll.runfiles"
		_ = os.Setenv("RUNFILES_DIR", runfilesDir)
		_ = os.Setenv("RUNFILES_MANIFEST_FILE", filepath.Join(runfilesDir, "MANIFEST"))
		_ = os.Setenv("RUNFILES_MANIFEST_ONLY", "0")
		launchInfo.Runfiles = GetRunfiles()
		LaunchDotnetPublish(os.Args, launchInfo)
	default:
		_, _ = fmt.Fprintf(os.Stderr, "unkown binary_type: %s", binaryType)
	}
}
