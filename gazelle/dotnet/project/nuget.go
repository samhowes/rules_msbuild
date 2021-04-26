package project

import (
	"regexp"
	"strconv"
	"strings"
)

type NugetSpec struct {
	Name    string
	Version *NugetVer
	Tfms    map[string]bool
}

type NugetVer struct {
	numberParts []int
	suffix      string
	Raw         string
}

var versionRegex = regexp.MustCompile(`(\d+\.?)+`)

// parseVersion makes a lazy attempt at parsing a probably semver version
// any number parts are extracted for precise comparison
// any non-alpha suffix is stored as a raw string and will be alphabetically sorted.
// suffix comparison is not intended to be accurate, only deterministic
func ParseVersion(s string) *NugetVer {
	v := NugetVer{Raw: s}

	match := versionRegex.FindStringIndex(s)
	if match == nil {
		v.suffix = s
		return &v
	}

	start := match[0]
	end := match[1] // don't care about other matches, only the leading numbers
	if start != 0 {
		v.suffix = s
		return &v
	}
	numberParts := strings.Split(s[:end], ".")
	v.numberParts = make([]int, len(numberParts))
	v.suffix = s[end:]

	for i, d := range numberParts {
		n, _ := strconv.Atoi(d)
		v.numberParts[i] = n
	}
	return &v
}

func Best(a *NugetVer, b *NugetVer) *NugetVer {
	var n int
	aLen := len(a.numberParts)
	bLen := len(b.numberParts)

	if aLen < bLen {
		n = aLen
	} else {
		n = bLen
	}

	for i := 0; i < n; i++ {
		ap := a.numberParts[i]
		bp := b.numberParts[i]
		if ap == bp {
			continue
		}
		if ap > bp {
			return a
		} else {
			return b
		}
	}

	if aLen != bLen {
		// longest wins
		if aLen < bLen {
			return b
		} else {
			return a
		}
	}
	// i'm not going to try to parse the suffix
	r := strings.Compare(a.suffix, b.suffix)
	if r == 0 {
		return a
	}
	// completely arbitrary
	if r < 0 {
		return a
	} else {
		return b
	}
}
