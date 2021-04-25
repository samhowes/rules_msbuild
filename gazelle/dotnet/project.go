package dotnet

import (
	"encoding/xml"
	"fmt"
	"io/ioutil"
	"path"
	"strings"
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
	Unsupported []Item `xml:",any"`
}

type Item struct {
	XMLName               xml.Name
	UnsupportedAttrs      []xml.Attr     `xml:",attr,any"`
	UnsupportedProperties []ItemProperty `xml:",any"`
}

type ItemProperty struct {
	XMLName xml.Name
}

func (p *Project) appendFiles(dir directoryInfo, key, rel, ext string) {
	_, exists := dir.exts[ext]
	if exists {
		fileList := p.Files[key]
		if rel != "" {
			rel = fmt.Sprintf("%s/", rel)
		}
		p.Files[key] = append(fileList, fmt.Sprintf("%s*%s", rel, ext))
	}
}

func (p *Project) CollectFiles(dir directoryInfo, rel string) {
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

	for _, c := range dir.children {
		var cRel string
		if rel == "" {
			cRel = c.base
		} else {
			cRel = path.Join(rel, c.base)
		}
		p.CollectFiles(c, cRel)
	}
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

	return &proj, nil
}
