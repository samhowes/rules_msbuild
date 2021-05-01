package project

import (
	"encoding/xml"
	"github.com/bazelbuild/bazel-gazelle/label"
	"github.com/bazelbuild/bazel-gazelle/rule"
	bzl "github.com/bazelbuild/buildtools/build"
)

var SpecialProperties = map[string]bool{
	"TargetFramework": true,
	"OutputType":      true,
}

type Project struct {
	XMLName        xml.Name        `xml:"Project"`
	Sdk            string          `xml:"Sdk,attr"`
	PropertyGroups []PropertyGroup `xml:"PropertyGroup"`
	ItemGroups     []ItemGroup     `xml:"ItemGroup"`
	Unsupported

	Properties      map[string]string
	TargetFramework string
	IsExe           bool
	IsWeb           bool
	IsTest          bool
	LangExt         string
	Files           map[string]*FileGroup
	Data            []string

	// Rel is the workspace relative path to the csproj file
	// Other projects will import this project with this path
	Rel       string
	Name      string
	FileLabel label.Label
	Rule      *rule.Rule
	Deps      []interface{}
}

func (p *Project) GetFileGroup(key string) *FileGroup {
	fg, exists := p.Files[key]
	if !exists {
		fg = &FileGroup{ItemType: key}
		p.Files[key] = fg
	}
	return fg
}

type FileGroup struct {
	ItemType       string
	BazelAttribute string
	Explicit       []bzl.Expr
	Globs          []bzl.Expr
	IncludeGlobs   []bzl.Expr
	Filters        []string
	Comments       []bzl.Comment
}

type PropertyGroup struct {
	Properties []Property `xml:",any"`
	Unsupported
}

type Property struct {
	XMLName xml.Name
	Value   string `xml:",chardata"`
	Unsupported
}

type ItemGroup struct {
	Compile           []Item             `xml:"Compile"`
	Content           []Item             `xml:"Content"`
	ProjectReferences []ProjectReference `xml:"ProjectReference"`
	PackageReferences []PackageReference `xml:"PackageReference"`
	Unsupported
}

type ProjectReference struct {
	XMLName xml.Name
	Include string `xml:",attr"`
	Unsupported
}

type PackageReference struct {
	XMLName xml.Name
	Include string `xml:"Include,attr"`
	Version string `xml:"Version,attr"`
	Unsupported
}

type Item struct {
	XMLName xml.Name
	Include string `xml:"Include,attr"`
	Exclude string `xml:"Exclude,attr"`
	Ignored
	Unsupported
}

// Ignored contains explicitly ignored attributes and elements.
// Properties of this struct are not translated to Starlark code. They may be used when collecting files for a project
type Ignored struct {
	// Remove is ignored because the project template disables all default item includes
	Remove string `xml:"Remove,attr"`
}

type Unsupported struct {
	UnsupportedAttrs    []xml.Attr   `xml:",attr,any"`
	UnsupportedElements []AnyElement `xml:",any"`
}

type AnyElement struct {
	XMLName xml.Name
}
