package dotnet

import (
	"flag"
	"github.com/bazelbuild/bazel-gazelle/config"
	"github.com/bazelbuild/bazel-gazelle/rule"
	"github.com/samhowes/my_rules_dotnet/gazelle/dotnet/project"
	"path"
)

func (d dotnetLang) RegisterFlags(fs *flag.FlagSet, cmd string, c *config.Config) {
	// todo(#84)
}

func (d dotnetLang) CheckFlags(fs *flag.FlagSet, c *config.Config) error {
	// todo(#84)
	return nil
}

func (d dotnetLang) KnownDirectives() []string {
	// todo(#84)
	return []string{}
}

// Configure modifies the configuration using directives and other information
// extracted from a build file. Configure is called in each directory.
//
// c is the configuration for the current directory. It starts out as a copy
// of the configuration for the parent directory.
//
// rel is the slash-separated relative path from the repository root to
// the current directory. It is "" for the root directory itself.
//
// f is the build file for the current directory or nil if there is no
// existing build file.
func (d dotnetLang) Configure(c *config.Config, rel string, f *rule.File) {
	base := path.Base(rel)
	if base == "node_modules" {
		delete(c.Exts, languageName)
		return
	}
	parent := getInfo(c)
	if parent == nil && rel != "" {
		return
	}

	self := project.DirectoryInfo{
		Base:     base,
		Children: map[string]*project.DirectoryInfo{},
		Exts:     map[string]bool{},
	}
	if parent != nil {
		parent.Children[base] = &self
	}
	c.Exts[languageName] = &self
}
