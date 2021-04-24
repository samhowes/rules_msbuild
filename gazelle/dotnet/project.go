package dotnet

import (
	"encoding/xml"
	"fmt"
	"io/ioutil"
	"path"
	"strings"
)

type Project struct {
	XMLName         xml.Name `xml:"Project"`
	Sdk             string   `xml:"Sdk,attr"`
	PropertyGroup   []PropertyGroup
	Properties      map[string]string
	TargetFramework string
	IsWeb           bool
	Executable      bool
	LangExt         string
	Files           map[string][]string
}

type PropertyGroup struct {
	Properties []Property `xml:",any"`
}

type Property struct {
	XMLName xml.Name
	Value   string `xml:",chardata"`
}

type ProjectFileInfo struct {
	Ext   string
	IsWeb bool
}

func (p *Project) appendFiles(dir directoryInfo, key, prefix, ext string) {
	_, exists := dir.extensions[ext]
	if exists {
		fileList := p.Files[key]
		p.Files[key] = append(fileList, fmt.Sprintf("%s*%s", prefix, ext))
	}
}

func (p *Project) CollectFiles(dir directoryInfo, prefix string) {
	p.appendFiles(dir, "srcs", prefix, p.LangExt)
	if p.IsWeb {
		for _, ext := range []string{".json", ".config"} {
			p.appendFiles(dir, "content", prefix, ext)
		}
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

	for _, pg := range proj.PropertyGroup {
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
