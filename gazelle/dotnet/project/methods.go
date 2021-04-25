package project

import (
	"encoding/xml"
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/label"
	"io/ioutil"
	"path"
	"path/filepath"
	"strings"
)

type DirectoryInfo struct {
	Children map[string]*DirectoryInfo
	Exts     map[string]bool
	Base     string
	Project  *Project
}

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
	proj.Files = make(map[string][]string)

	outputType, exists := proj.Properties["OutputType"]
	if exists && strings.EqualFold(outputType, "exe") || proj.IsWeb {
		proj.Executable = true
	}

	proj.TargetFramework, _ = proj.Properties["TargetFramework"]

	baseName := path.Base(projectFile)
	projExt := path.Ext(baseName)
	proj.Name = baseName[0 : len(baseName)-len(projExt)]

	return &proj, nil
}

// NormalizePath takes an unclean absolute path to a project file and constructs a bazel label for it
// The path may contain any combination of `.`, `..`, `/` and `\`
// Constructing a label is not strictly necessary, but this is bazel, and a label is a convenient notation
func NormalizePath(dirtyProjectPath, repoRoot string) (label.Label, error) {
	var old string
	switch filepath.Separator {
	case '/':
		old = "\\"
	case '\\':
		old = "/"
	}
	// even on non-windows, dotnet still uses paths with backslashes in project files
	dirtyProjectPath = strings.Replace(dirtyProjectPath, old, string(filepath.Separator), -1)

	// project files use relative paths, clean them to get the absolute path
	cleaned := path.Clean(dirtyProjectPath)
	if !strings.HasPrefix(cleaned, repoRoot) {
		err := fmt.Errorf("project path is not rooted in the repository: %s", cleaned)
		return label.NoLabel, err
	}

	rPath := strings.TrimPrefix(cleaned, repoRoot)

	if filepath.Separator == '\\' {
		rPath = strings.Replace(rPath, "\\", "/", -1)
	}

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

func (p *Project) appendFiles(dir *DirectoryInfo, key, rel, ext string) {
	_, exists := dir.Exts[ext]
	if exists {
		fileList := p.Files[key]
		if rel != "" {
			rel = fmt.Sprintf("%s/", rel)
		}
		p.Files[key] = append(fileList, fmt.Sprintf("%s*%s", rel, ext))
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

	p.appendFiles(dir, "srcs", rel, p.LangExt)
	if p.IsWeb {
		for _, ext := range []string{".json", ".config"} {
			p.appendFiles(dir, "content", rel, ext)
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
