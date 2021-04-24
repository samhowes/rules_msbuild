package dotnet

import (
	"encoding/xml"
	"io/ioutil"
)

type Project struct {
	XMLName         xml.Name `xml:"Project"`
	Sdk             string   `xml:"Sdk,attr"`
	PropertyGroup   []PropertyGroup
	Properties      map[string]string
	TargetFramework string
}

type PropertyGroup struct {
	Properties []Property `xml:",any"`
}

type Property struct {
	XMLName xml.Name
	Value   string `xml:",chardata"`
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

	proj.Properties = make(map[string]string)

	for _, pg := range proj.PropertyGroup {
		for _, p := range pg.Properties {
			proj.Properties[p.XMLName.Local] = p.Value
		}
	}

	proj.TargetFramework, _ = proj.Properties["TargetFramework"]

	return &proj, nil
}
