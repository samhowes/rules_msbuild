package project

import (
	"encoding/xml"
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/label"
)

type Project struct {
	XMLName        xml.Name        `xml:"Project"`
	Sdk            string          `xml:"Sdk,attr"`
	PropertyGroups []PropertyGroup `xml:"PropertyGroup"`
	ItemGroups     []ItemGroup     `xml:"ItemGroup"`
	Unsupported

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

func (p *Project) GetUnsupported() []string {
	var messages []string
	messages = p.Unsupported.Append(messages, "project")
	for _, pg := range p.PropertyGroups {
		messages = pg.Unsupported.Append(messages, "property")
		for _, prop := range pg.Properties {
			messages = prop.Unsupported.Append(messages, "property")
		}
	}
	for _, ig := range p.ItemGroups {
		messages = ig.Unsupported.Append(messages, "item")
	}
	return messages
}

func (u Unsupported) Append(messages []string, prefix string) []string {
	if prefix != "" {
		prefix = fmt.Sprintf(" %s ", prefix)
	}
	apnd := func(t, value string) {
		msg := fmt.Sprintf("unsupported%s%s: %s", prefix, t, value)
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
	Include string `xml:",attr"`
	Unsupported
}

type Item struct {
	XMLName xml.Name
	Unsupported
}

type Unsupported struct {
	UnsupportedAttrs    []xml.Attr   `xml:",attr,any"`
	UnsupportedElements []AnyElement `xml:",any"`
}

type AnyElement struct {
	XMLName xml.Name
}
