package main

import (
	"encoding/binary"
	"fmt"
	"io"
	"os"
	"path"
	"reflect"
	"strings"
)

type LaunchInfo struct {
	Data     map[string]string
	Runfiles *Runfiles
}

func (l *LaunchInfo) GetItem(key string) string {
	value, present := l.Data[key]
	if !present {
		panic(fmt.Sprintf("missing required launch data key: %s; %s", key, l))
	}
	return value
}

func (l *LaunchInfo) GetListItem(key string) []string {
	value := l.GetItem(key)
	if value == "" {
		return []string{}
	}
	return strings.Split(value, "*~*")
}

func (l *LaunchInfo) GetPathItem(key string) string {
	value := l.GetItem(key)
	return l.GetRunfile(value)
}

func (l *LaunchInfo) GetRunfile(p string) string {
	fPath := l.Runfiles.Rlocation(p)
	if fPath == "" {
		panic(fmt.Sprintf("missing required runfile path item %s", p))
	}
	return fPath
}

// GetBuiltPath assumes that key is a short_path to the output directory of an assembly built by rules_msbuild
// this means that the output directory is listed in the runfiles manifest, and since the output directory is a prefix
// of all the items in the output directory, the actual output items are not listed explicitly in the manifest
func (l *LaunchInfo) GetBuiltPath(key string) string {
	outputDir := l.GetItem("output_dir")
	value := l.GetItem(key)
	diag(func() { fmt.Printf("findng built path: %s using prefix %s\n", value, outputDir) })
	value = value[len(outputDir)+1:]
	outputDirPath := l.GetRunfile(outputDir)
	return path.Join(outputDirPath, value)
}

func GetLaunchInfo(binaryPath string) (*LaunchInfo, error) {
	f, err := os.Open(binaryPath)
	if err != nil {
		return nil, fmt.Errorf("failed to open %s: %w", binaryPath, err)
	}

	var dataSize int64
	offsetSize := int64(reflect.TypeOf(dataSize).Size())

	_, err = f.Seek(-1*offsetSize, io.SeekEnd)
	if err != nil {
		return nil, fmt.Errorf("failed to seek in file: %w", err)
	}

	data := make([]byte, offsetSize)
	bytesRead, err := f.Read(data)
	if err != nil || bytesRead != int(offsetSize) {
		return nil, fmt.Errorf("failed to read data size, got %d bytes (expected %d) and error: %w",
			bytesRead, offsetSize, err)
	}

	dataSize = int64(binary.LittleEndian.Uint64(data[:]))
	_, err = f.Seek(-1*(dataSize+offsetSize), io.SeekEnd)
	if err != nil {
		return nil, fmt.Errorf("failed to seek beginning of launchData: %w", err)
	}

	launchBytes := make([]byte, dataSize)
	_, err = f.Read(launchBytes)
	if err != nil {
		return nil, fmt.Errorf("failed to read launch data: %w", err)
	}

	launchInfo := &LaunchInfo{Data: map[string]string{}}
	start := 0

	diag(func() { fmt.Println("==> launch data") })

	var key string
	for start < len(launchBytes) {
		end := start
		for end < len(launchBytes) {
			c := launchBytes[end]
			if key == "" && c == '=' {
				break
			} else if c == '\x00' {
				break
			}

			end++
		}

		// supposedly strings are utf8 by default
		value := string(launchBytes[start:end])
		start = end + 1 // skip the separator character
		if key == "" {
			diag(func() { fmt.Printf("  %s=", value) })

			key = value
		} else {
			launchInfo.Data[key] = value
			diag(func() { fmt.Println(value) })
			key = ""
		}
	}

	err = f.Close()
	if err != nil {
		return nil, fmt.Errorf("failed to close file: %w", err)
	}
	return launchInfo, nil
}
