package project

import (
	"encoding/xml"
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/label"
	bzl "github.com/bazelbuild/buildtools/build"
	"github.com/bmatcuk/doublestar"
	"io/ioutil"
	"path"
	"regexp"
	"strings"
)

var variableRegex = regexp.MustCompile(`\$\((\w+)\)`)

type DirectoryInfo struct {
	Children map[string]*DirectoryInfo
	Exts     map[string][]string
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
	proj.srcsModes = make(map[string]SrcsMode)

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

func (i *ProjectReference) Evaluate(p *Project) {
	i.Include = p.Evaluate(Forward(i.Include))
}

func (i *Item) Evaluate(p *Project) {
	i.Include = p.Evaluate(Forward(i.Include))
	i.Exclude = p.Evaluate(Forward(i.Exclude))
	i.Remove = p.Evaluate(Forward(i.Remove))
}

func (r *PackageReference) Evaluate(proj *Project) {
	r.Include = proj.Evaluate(r.Include)
	r.Version = proj.Evaluate(r.Version)
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

const externalPrefix = "$(BazelExternal)"

// GetLabel takes an unclean absolute path to a project file and constructs a bazel label for it
// The path may contain any combination of `.`, `..`, `/` and `\`
// Constructing a label is not strictly necessary, but this is bazel, and a label is a convenient notation
func GetLabel(dir, referencePath, repoRoot string) (label.Label, error) {
	if strings.HasPrefix(referencePath, externalPrefix) {
		parts := strings.Split(referencePath, "/")
		if len(parts) < 3 {
			err := fmt.Errorf("invalid external reference: %s", referencePath)
			return label.NoLabel, err
		}
		workspaceName := parts[1]
		pkg := strings.Join(parts[2:len(parts)-1], "/")
		// $(External)/rules_msbuild/dotnet/tools/Runfiles/Runfiles.csproj => @rules_msbuild//dotnet/tools/Runfiles
		// for other external repositories, we'll need to add repository rules, but we'll do that when there is actually
		// demand for it
		return label.Label{
			Repo: workspaceName,
			Pkg:  pkg,
			Name: path.Base(pkg),
		}, nil
	}

	referencePath = path.Join(dir, referencePath)
	if !strings.HasPrefix(referencePath, repoRoot) {
		err := fmt.Errorf("project path is not rooted in the repository: %s", referencePath)
		return label.NoLabel, err
	}
	pkgAndName := referencePath[len(repoRoot)+1:]

	lastSlash := strings.LastIndex(pkgAndName, "/")
	l := label.Label{
		// this works even for root because lastSlash will be -1, and we'll want the whole string
		Name: pkgAndName[lastSlash+1:],
	}

	if lastSlash > -1 {
		l.Pkg = pkgAndName[:lastSlash]
	}
	return l, nil
}

func Forward(p string) string {
	return strings.Replace(p, "\\", "/", -1)
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
	files, exists := dir.Exts[ext]
	if !exists {
		return
	}
	fg := p.GetFileGroup(key)
	if rel != "" {
		rel = forceSlash(rel) + "/"
	}
	mode := p.srcsModes[key]
	if mode == Folders {
		testFile := fmt.Sprintf("%sfoo%s", rel, ext)
		if fg.IsExcluded(testFile) {
			return
		}
		fg.IncludeGlob(fmt.Sprintf("%s*%s", rel, ext))
	} else if mode == Explicit {
		for _, f := range files {
			if fg.IsExcluded(f) {
				continue
			}
			fg.Explicit = append(fg.Explicit, &bzl.StringExpr{Value: rel + f})
		}
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

	key := "Compile"
	if p.srcsModes[key] != Implicit {
		// make sure we have an entry so we send `srcs = []` when empty to the macro
		// to prevent it from implicitly globbing
		_ = p.GetFileGroup(key)

		p.appendFiles(dir, key, rel, p.LangExt)
		if p.LangExt == ".cs" {
			p.appendFiles(dir, key, rel, ".cshtml")
		}
	}

	if p.IsWeb {
		key = "Content"
		originalMode, exists := p.srcsModes[key]
		if !exists || originalMode == Implicit {
			// do this so we don't have to write an ugly glob that excludes bin, obj, and Properties
			p.srcsModes[key] = Folders
		}
		for _, ext := range []string{".json", ".config"} {
			p.appendFiles(dir, key, rel, ext)
		}
		p.srcsModes[key] = originalMode
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
