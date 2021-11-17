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
	Imports        []*Import        `xml:"Import"`
	Unsupported

	Properties      map[string]string
	AssemblyName    string
	TargetFramework string
	PackageId       string
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
	Directory *DirectoryInfo
	srcsModes map[string]SrcsMode
	Ext       string
	Protos    []string
}

type Import struct {
	XMLName xml.Name `xml:"Import"`
	Project string   `xml:"Project,attr"`
	Unsupported
}

func (i Import) Evaluate(proj *Project) {
	i.Project = proj.Evaluate(i.Project)
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

func (fg *FileGroup) IncludeGlob(g string) {
	fg.IncludeGlobs = append(fg.IncludeGlobs, &bzl.StringExpr{Value: g})
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
	ProjectReferences []*ProjectReference `xml:"ProjectReference"`
	PackageReferences []*PackageReference `xml:"PackageReference"`
	Protobuf          []*Protobuf         `xml:"Protobuf"`
	// None items are completely ignored
	None []*Item `xml:"None"`
	Unsupported
}

type ProjectReference struct {
	XMLName xml.Name
	Include string `xml:",attr"`
	Unsupported
}

type PackageReference struct {
	XMLName   xml.Name
	Include   string   `xml:"Include,attr"`
	Version   string   `xml:"Version,attr"`
	VersionEl *Version `xml:"Version"`
	Unsupported
}

type Version struct {
	XMLName xml.Name
	Value   string `xml:",chardata"`
}

type Item struct {
	XMLName xml.Name
	Include string `xml:"Include,attr"`
	Exclude string `xml:"Exclude,attr"`
	// Remove is not directly output to starlark, but is used to filter globbed files
	Remove    string `xml:"Remove,attr"`
	Condition string `xml:"Condition,attr"`
	Unsupported
}

type Protobuf struct {
	XMLName xml.Name
	Item
	GrpcServices string `xml:"GrpcServices,attr"`
}

type Unsupported struct {
	UnsupportedAttrs    []xml.Attr   `xml:",attr,any"`
	UnsupportedElements []AnyElement `xml:",any"`
}

type AnyElement struct {
	XMLName xml.Name
}
