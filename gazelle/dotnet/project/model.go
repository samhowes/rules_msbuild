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
	XMLName        xml.Name         `xml:"Project"`
	Sdk            string           `xml:"Sdk,attr"`
	PropertyGroups []*PropertyGroup `xml:"PropertyGroup"`
	ItemGroups     []*ItemGroup     `xml:"ItemGroup"`
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
	FileLabel *label.Label
	Rule      *rule.Rule
	Deps      []interface{}
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
	Compile           []*Item             `xml:"Compile"`
	Content           []*Item             `xml:"Content"`
	None              []*Item             `xml:"None"`
	ProjectReferences []*ProjectReference `xml:"ProjectReference"`
	PackageReferences []*PackageReference `xml:"PackageReference"`
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
	// Remove is not directly output to starlark, but is used to filter globbed files
	Remove string `xml:"Remove,attr"`
	Unsupported
}

type Unsupported struct {
	UnsupportedAttrs    []xml.Attr   `xml:",attr,any"`
	UnsupportedElements []AnyElement `xml:",any"`
}

type AnyElement struct {
	XMLName xml.Name
}
