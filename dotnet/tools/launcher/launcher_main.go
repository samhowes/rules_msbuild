// Package launcher is a launcher utility for windows that takes care of locating the dotnet binary and the user's
// executable and starting both with the right arguments.
package main

import (
	"fmt"
	"os"
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
		LaunchDotnet(os.Args, launchInfo)
	case "DotnetPublish":
		LaunchDotnetPublish(os.Args, launchInfo)
	default:
		_, _ = fmt.Fprintf(os.Stderr, "unkown binary_type: %s", binaryType)
	}
}
