package project

import (
	"encoding/xml"
	"github.com/bazelbuild/bazel-gazelle/label"
)

type Project struct {
	XMLName        xml.Name        `xml:"Project"`
	Sdk            string          `xml:"Sdk,attr"`
	PropertyGroups []PropertyGroup `xml:"PropertyGroup"`
	ItemGroups     []ItemGroup     `xml:"ItemGroup"`
	Unsupported    []ProjectItem   `xml:",any"`

	Properties      map[string]string
	TargetFramework string
	IsWeb           bool
	Executable      bool
	LangExt         string
	Files           map[string][]string
	Data            []string

	// Rel is the workspace relative path to the csproj file
	// Other projects will import this project with this path
	Rel       string
	Name      string
	FileLabel label.Label
}

type ProjectItem struct {
	XMLName xml.Name
}

type PropertyGroup struct {
	Properties []Property `xml:",any"`
}

type Property struct {
	XMLName xml.Name
	Value   string `xml:",chardata"`
}

type ItemGroup struct {
	ProjectReferences []ProjectReference `xml:"ProjectReference"`
	Unsupported       []Item             `xml:",any"`
}

type ProjectReference struct {
	XMLName xml.Name
	Include string `xml:",attr"`
}

type Item struct {
	XMLName               xml.Name
	UnsupportedAttrs      []xml.Attr     `xml:",attr,any"`
	UnsupportedProperties []ItemProperty `xml:",any"`
}

type ItemProperty struct {
	XMLName xml.Name
}
