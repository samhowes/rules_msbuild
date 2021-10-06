package dotnet

import (
	"github.com/bazelbuild/bazel-gazelle/config"
	"github.com/bazelbuild/bazel-gazelle/label"
	"github.com/bazelbuild/bazel-gazelle/language"
	"github.com/bazelbuild/bazel-gazelle/rule"
	"github.com/samhowes/rules_msbuild/gazelle/dotnet/project"
)

type dotnetLang struct{}

// NewLanguage is called by gazelle to install this language extension in a binary
func NewLanguage() language.Language {
	return &dotnetLang{}
}

const dotnetName = "msbuild"
const dotnetDirName = "dotnet_dir"

// Name returns the name of the language. This should be a prefix of the
// kinds of rules generated by the language, e.g., "go" for the Go extension
// since it generates "go_library" rules.
func (d *dotnetLang) Name() string { return dotnetName }

func (d *dotnetLang) Embeds(r *rule.Rule, from label.Label) []label.Label {
	return []label.Label{}
}

// Kinds returns a map of maps rule names (kinds) and information on how to
// match and merge attributes that may be found in rules of those kinds. All
// kinds of rules generated for this language may be found here.
func (d *dotnetLang) Kinds() map[string]rule.KindInfo {
	return kinds
}

// Loads returns .bzl files and symbols they define. Every rule generated by
// GenerateRules, now or in the past, should be loadable from one of these
// files.
func (d *dotnetLang) Loads() []rule.LoadInfo {
	var symbols []string
	for k, _ := range kinds {
		symbols = append(symbols, k)
	}
	return []rule.LoadInfo{{
		Name:    "@rules_msbuild//dotnet:defs.bzl",
		Symbols: symbols,
	}, {
		Name:    "@rules_msbuild//deps:public_nuget.bzl",
		Symbols: []string{"PACKAGES", "FRAMEWORKS"},
	}}
}

var commonInfo = rule.KindInfo{
	MergeableAttrs: map[string]bool{
		"srcs":             true,
		"target_framework": true,
	},
	ResolveAttrs: map[string]bool{"deps": true},
}

var kinds = map[string]rule.KindInfo{
	"msbuild_library": commonInfo,
	"msbuild_binary":  commonInfo,
	"msbuild_test":    commonInfo,
	"nuget_fetch":     {},
	"nuget_deps_helper": {MergeableAttrs: map[string]bool{
		"target_frameworks": true,
	}},
}

func (d *dotnetLang) Fix(c *config.Config, f *rule.File) {
}

func getInfo(c *config.Config) *project.DirectoryInfo {
	i, exists := c.Exts[dotnetDirName]
	if !exists {
		return nil
	}
	return i.(*project.DirectoryInfo)
}

func getConfig(c *config.Config) *dotnetConfig {
	return c.Exts[dotnetName].(*dotnetConfig)
}
