package dotnet

import (
	"flag"
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/config"
	gzflag "github.com/bazelbuild/bazel-gazelle/flag"
	"github.com/bazelbuild/bazel-gazelle/rule"
	"github.com/samhowes/my_rules_dotnet/gazelle/dotnet/project"
	"log"
	"os"
	"path"
	"strings"
)

type dotnetConfig struct {
	macroFileName     string
	macroDefName      string
	packageReportFile string
	packages          map[string]*project.NugetSpec
	srcsMode          project.SrcsMode
	srcsModeString    string
	debug             bool
}

func (dc *dotnetConfig) recordPackage(ref *project.PackageReference, tfm string) {
	if dc.macroFileName == "" {
		return
	}
	spec, exists := dc.packages[ref.Include]
	v := project.ParseVersion(ref.Version)
	if !exists {
		if dc.debug {
			log.Printf("adding package %s/%s", ref.Include, v.Raw)
		}
		dc.packages[ref.Include] = &project.NugetSpec{
			Name:    ref.Include,
			Version: v,
			Tfms:    map[string]bool{tfm: true},
		}
		return
	}

	spec.Version = project.Best(spec.Version, v)
	spec.Tfms[tfm] = true
}

type macroFlag struct {
	macroFileName *string
	macroDefName  *string
}

func (f macroFlag) Set(value string) error {
	args := strings.Split(value, "%")
	if len(args) != 2 {
		return fmt.Errorf("Failure parsing to_macro: %s, expected format is macroFile%%defName", value)
	}
	if strings.HasPrefix(args[0], "..") {
		return fmt.Errorf("Failure parsing to_macro: %s, macro file path %s should not start with \"..\"", value, args[0])
	}
	*f.macroFileName = args[0]
	*f.macroDefName = args[1]
	return nil
}

func (f macroFlag) String() string {
	return ""
}

func (d *dotnetLang) RegisterFlags(fs *flag.FlagSet, cmd string, c *config.Config) {
	dc := &dotnetConfig{packages: map[string]*project.NugetSpec{}}
	c.Exts[dotnetName] = dc
	switch cmd {
	case "fix", "update", "update-repos":
		fs.Var(macroFlag{
			macroFileName: &dc.macroFileName,
			macroDefName:  &dc.macroDefName,
		},
			"deps_macro",
			"Record nuget package versions and tfms in a macro after parsing all the project files. "+
				"Strongly recommended for managing nuget packages.")
		fs.Var(
			&gzflag.AllowedStringFlag{Value: &dc.srcsModeString, Allowed: []string{"implicit", "folders", "explicit"}},
			"srcs_mode",
			"controls how `srcs` attributes are generated. One of (implicit, folders, explicit), defaults to implicit",
		)

	}
}

func (d *dotnetLang) CheckFlags(fs *flag.FlagSet, c *config.Config) error {
	dc := getConfig(c)
	if os.Getenv("DOTNET_GAZELLE_DEBUG") != "" {
		dc.debug = true
	}
	if dc.srcsModeString != "" {
		mode, err := getSrcsMode(dc.srcsModeString, project.Implicit)
		dc.srcsMode = mode
		if err != nil {
			log.Print(err)
		}
	}

	return nil
}

func (d *dotnetLang) KnownDirectives() []string {
	return []string{
		"srcs_mode",
	}
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
func (d *dotnetLang) Configure(c *config.Config, rel string, f *rule.File) {
	base := path.Base(rel)
	if base == "node_modules" {
		delete(c.Exts, dotnetDirName)
		return
	}
	parent := getInfo(c)
	if parent == nil && rel != "" {
		// we explicitly decided to ignore this subtree
		return
	}
	dc := getConfig(c)
	if dc.debug {
		log.Printf(rel)
	}
	self := project.DirectoryInfo{
		Base:     base,
		Children: map[string]*project.DirectoryInfo{},
		Exts:     map[string][]string{},
		SrcsMode: dc.srcsMode,
	}
	if parent != nil {
		self.SrcsMode = parent.SrcsMode
		parent.Children[base] = &self
	}
	c.Exts[dotnetDirName] = &self

	if f == nil {
		return
	}

	for _, d := range f.Directives {
		switch d.Key {
		case "srcs_mode":
			mode, err := getSrcsMode(d.Value, dc.srcsMode)
			self.SrcsMode = mode
			if err != nil {
				log.Print(err)
			}
		}
	}
}

func getSrcsMode(stringValue string, defaultValue project.SrcsMode) (project.SrcsMode, error) {
	value := defaultValue
	var err error
	switch stringValue {
	case "implicit":
		value = project.Implicit
	case "folders":
		value = project.Folders
	case "explicit":
		value = project.Explicit
	default:
		err = fmt.Errorf("invalid value %s for 'srcs_mode'", stringValue)
	}
	return value, err
}
