package project

import (
	"encoding/xml"
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/label"
	bzl "github.com/bazelbuild/buildtools/build"
	"github.com/bmatcuk/doublestar"
	"io/ioutil"
	"os"
	"path"
	"regexp"
	"strings"
)

var variableRegex = regexp.MustCompile(`\$\((\w+)\)`)

type DirectoryInfo struct {
	Children map[string]*DirectoryInfo
	Exts     map[string]bool
	Base     string
	Project  *Project
	SrcsMode SrcsMode
}

type SrcsMode int

const (
	Implicit SrcsMode = iota
	Folders
	Explicit
)

func Load(projectFile string) (*Project, error) {
	var proj Project
	contents, err := ioutil.ReadFile(projectFile)
	if err != nil {
		return nil, err
	}
	err = xml.Unmarshal(contents, &proj)
	if err != nil {
		return nil, err
	}

	proj.LangExt = strings.TrimSuffix(path.Ext(projectFile), "proj")
	proj.Properties = make(map[string]string)

	for _, pg := range proj.PropertyGroups {
		for _, p := range pg.Properties {
			proj.Properties[p.XMLName.Local] = p.Value
		}
	}

	proj.IsWeb = strings.EqualFold(proj.Sdk, "Microsoft.NET.Sdk.Web")
	proj.Files = make(map[string]*FileGroup)

	outputType, exists := proj.Properties["OutputType"]
	if exists && strings.EqualFold(outputType, "exe") || proj.IsWeb {
		proj.IsExe = true
	}

	proj.TargetFramework, _ = proj.Properties["TargetFramework"]

	baseName := path.Base(projectFile)
	projExt := path.Ext(baseName)
	proj.Name = baseName[0 : len(baseName)-len(projExt)]

	return &proj, nil
}

func (p *Project) GetFileGroup(key string) *FileGroup {
	fg, exists := p.Files[key]
	if !exists {
		fg = &FileGroup{ItemType: key}
		p.Files[key] = fg
	}
	return fg
}

func (p *Project) EvaluateItem(i *Item) {
	i.Include = p.Evaluate(i.Include)
	i.Exclude = p.Evaluate(i.Exclude)
	i.Remove = p.Evaluate(i.Remove)
}

func (p *Project) Evaluate(s string) string {
	if s == "" || len(s) < 4 {
		return s
	}

	replaced := variableRegex.ReplaceAllStringFunc(s, func(match string) string {
		// there has to be a better way, but oh well
		variableName := match[len("$(") : len(match)-len(")")]
		variableValue, exists := p.Properties[variableName]
		if exists {
			return variableValue
		}
		return match
	})
	return replaced
}

// NormalizePath takes an unclean absolute path to a project file and constructs a bazel label for it
// The path may contain any combination of `.`, `..`, `/` and `\`
// Constructing a label is not strictly necessary, but this is bazel, and a label is a convenient notation
func NormalizePath(dirtyProjectPath, repoRoot string) (label.Label, error) {
	// even on non-windows, dotnet still uses paths with backslashes in project files
	// even on windows, go uses '/' for path cleaning
	dirtyProjectPath = strings.Replace(dirtyProjectPath, "\\", "/", -1)

	// project files use relative paths, clean them to get the absolute path
	cleaned := path.Clean(dirtyProjectPath)
	// path.Clean will exclusively work with forward slashes
	// repoRoot will be an actual windows path with backslashes on windows though
	if os.PathSeparator == '\\' {
		repoRoot = strings.Replace(repoRoot, "\\", "/", -1)
	}
	if !strings.HasPrefix(cleaned, repoRoot) {
		err := fmt.Errorf("project path is not rooted in the repository: %s", cleaned)
		return label.NoLabel, err
	}

	rPath := cleaned[len(repoRoot)+1:]

	lastSlash := strings.LastIndex(rPath, "/")

	l := label.Label{
		// this works even for root because lastSlash will be -1, and we'll want the whole string
		Name: rPath[lastSlash+1:],
	}

	if lastSlash > -1 {
		l.Pkg = rPath[:lastSlash]
	}
	return l, nil
}

func (fg *FileGroup) IsExcluded(file string) bool {
	for _, f := range fg.Filters {
		if matched, _ := doublestar.Match(f, file); matched {
			return true
		}
	}
	return false
}

func (p *Project) appendFiles(dir *DirectoryInfo, key, rel, ext string) {
	_, exists := dir.Exts[ext]
	if !exists {
		return
	}
	fg := p.GetFileGroup(key)
	if rel != "" {
		rel = fmt.Sprintf("%s/", forceSlash(rel))
	}
	if dir.SrcsMode == Folders {
		testFile := fmt.Sprintf("%sfoo%s", rel, ext)
		if fg.IsExcluded(testFile) {
			return
		}
		fg.IncludeGlobs = append(fg.IncludeGlobs, &bzl.StringExpr{Value: fmt.Sprintf("%s*%s", rel, ext)})
	}
}

func (p *Project) CollectFiles(dir *DirectoryInfo, rel string) {
	// https://docs.microsoft.com/en-us/dotnet/core/project-sdk/overview#default-includes-and-excludes
	// https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/host-and-deploy/visual-studio-publish-profiles.md#compute-project-items
	switch rel {
	case "wwwroot":
		p.Data = append(p.Data, "wwwroot/**")
		return
	case "bin":
		return
	case "obj":
		return
	}

	if dir.SrcsMode != Implicit {
		p.appendFiles(dir, "Compile", rel, p.LangExt)
		if p.LangExt == ".cs" {
			p.appendFiles(dir, "Compile", rel, ".cshtml")
		}
	}
	if p.IsWeb {
		for _, ext := range []string{".json", ".config"} {
			p.appendFiles(dir, "Content", rel, ext)
		}
	}

	for _, c := range dir.Children {
		var cRel string
		if rel == "" {
			cRel = c.Base
		} else {
			cRel = path.Join(rel, c.Base)
		}
		p.CollectFiles(c, cRel)
	}
}

func (p *Project) GetUnsupported() []string {
	var messages []string
	messages = p.Unsupported.Append(messages, "project")
	for _, pg := range p.PropertyGroups {
		messages = pg.Unsupported.Append(messages, "property group")
		for _, prop := range pg.Properties {
			if SpecialProperties[prop.XMLName.Local] {
				continue
			}
			messages = prop.Unsupported.Append(messages, "property")
		}
	}
	for _, ig := range p.ItemGroups {
		messages = ig.Unsupported.Append(messages, "item group")
	}
	return messages
}

func (u *Unsupported) Append(messages []string, prefix string) []string {
	messages = append(messages, u.Messages(prefix)...)
	return messages
}

func (u *Unsupported) Messages(prefix string) []string {
	var messages []string
	if prefix != "" {
		prefix = fmt.Sprintf(" %s", prefix)
	}
	apnd := func(t, value string) {
		msg := fmt.Sprintf("unsupported%s %s: %s", prefix, t, value)
		messages = append(messages, msg)
	}
	for _, a := range u.UnsupportedAttrs {
		apnd("attribute", a.Name.Local)
	}
	for _, a := range u.UnsupportedElements {
		apnd("element", a.XMLName.Local)
	}
	return messages
}
