package main

import (
	"encoding/binary"
	"fmt"
	"io"
	"os"
	"path"
	"reflect"
	"runtime"
)

type LaunchInfo = map[string]string

func EnsureExe(p string) string {
	if runtime.GOOS != "windows" {
		return p
	}
	if path.Ext(p) != ".exe" {
		return p + ".exe"
	}
	return p
}

func GetLaunchInfo(binaryPath string) (LaunchInfo, error) {
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

	launchInfo := make(LaunchInfo)
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
			launchInfo[key] = value
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
