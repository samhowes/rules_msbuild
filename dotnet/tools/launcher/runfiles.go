// The go runfiles functions index the manifest by shortpath. we want workspace_name/shortpath

package main

import (
	"bytes"
	"fmt"
	"github.com/bazelbuild/rules_go/go/tools/bazel"
	"io/ioutil"
	"log"
	"os"
	"path"
	"path/filepath"
	"runtime"
	"strings"
)

const runfilesSuffix = ".runfiles"

type Runfiles struct {
	strategy RunfilesStrategy
}

func (r *Runfiles) Rlocation(p string) string {
	if filepath.IsAbs(p) {
		return p
	}

	for _, prefix := range []string{"../", "external/"} {
		if strings.HasPrefix(p, prefix) {
			p = p[len(prefix):]
			break
		}
	}

	return r.strategy.Rlocation(p)
}

type RunfilesStrategy interface {
	Rlocation(path string) string
}

type ManifestStrategy struct {
	manifestPath string
	data         map[string]string
}

func (s *ManifestStrategy) Rlocation(p string) string {
	return s.data[p]
}

type DirectoryStrategy struct {
	runfileDirectory string
}

func (s *DirectoryStrategy) Rlocation(p string) string {
	return path.Join(s.runfileDirectory, p)
}
func EnsureExe(p string) string {
	if runtime.GOOS != "windows" {
		return p
	}
	if path.Ext(p) != ".exe" {
		return p + ".exe"
	}
	return p
}

func findRunfilesDir() string {
	runfilesDir := os.Getenv(bazel.RUNFILES_DIR)
	if runfilesDir != "" {
		return runfilesDir
	}
	runfilesDir = os.Getenv(bazel.TEST_SRCDIR)
	if runfilesDir != "" {
		return runfilesDir
	}
	thisBin := EnsureExe(os.Args[0])
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
	return runfilesDir
}

func getManifestPath(runfilesDir string) string {
	manifestPath := os.Getenv(bazel.RUNFILES_MANIFEST_FILE)
	if manifestPath != "" {
		return manifestPath
	}
	return filepath.Join(runfilesDir, "MANIFEST")
}

func GetRunfiles() *Runfiles {
	runfilesDir := findRunfilesDir()
	manifestPath := getManifestPath(runfilesDir)

	isManifestOnly := os.Getenv("RUNFILES_MANIFEST_ONLY") == "1"

	runfiles := Runfiles{}
	if isManifestOnly || runtime.GOOS == "windows" {
		s := &ManifestStrategy{manifestPath: manifestPath, data: map[string]string{}}
		runfiles.strategy = s
		contentBytes, err := ioutil.ReadFile(manifestPath)
		if err != nil {
			log.Panicf("failed to read runfiles manifest: %v", err)
		}

		for _, line := range bytes.Split(contentBytes, []byte{'\n'}) {
			ind := bytes.IndexRune(line, ' ')
			if ind >= 0 {
				key := line[0:ind]
				value := line[ind+1:]
				s.data[string(key)] = string(value)
			} else {
				// python doesn't have a space in the line for __init__.py
				s.data[string(line)] = ""
			}
		}
	} else if runfilesDir != "" {
		runfiles.strategy = &DirectoryStrategy{runfileDirectory: runfilesDir}
	}

	_ = os.Setenv(bazel.RUNFILES_DIR, runfilesDir)
	for k, v := range map[string]string{
		bazel.RUNFILES_DIR:           runfilesDir,
		bazel.RUNFILES_MANIFEST_FILE: manifestPath,
	} {
		// just assume we need to set the value for windows
		if err := os.Setenv(k, v); err != nil {
			panic(fmt.Errorf("failed to set the manifest file path: %v", err))
		}
	}

	diag(func() {
		abs, _ := filepath.Abs(manifestPath)
		fmt.Printf("located manifest file: %s\n", abs)
	})
	return &runfiles
}
