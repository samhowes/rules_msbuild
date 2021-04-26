package dotnet

import (
	"encoding/json"
	"fmt"
	"log"
	"os"
	"path"
	"sort"

	"github.com/bazelbuild/bazel-gazelle/language"
	"github.com/bazelbuild/bazel-gazelle/rule"
	"github.com/samhowes/my_rules_dotnet/gazelle/dotnet/project"
)

// RepoUpdater may be implemented by languages that support updating
// repository rules that provide named libraries.
//
// EXPERIMENTAL: this may change or be removed.
func (d *dotnetLang) UpdateRepos(args language.UpdateReposArgs) language.UpdateReposResult {
	log.Panicf("update repo!")
	return language.UpdateReposResult{}
}

// ImportRepos generates a list of repository rules by reading a
// configuration file from another build system.
func (d *dotnetLang) ImportRepos(args language.ImportReposArgs) language.ImportReposResult {
	var packages map[string]project.NugetSpec
	res := language.ImportReposResult{}
	c, err := os.ReadFile(args.Path)
	res.Error = err
	if res.Error == nil {
		res.Error = json.Unmarshal(c, &packages)
	}
	if res.Error != nil {
		return res
	}

	r := rule.NewRule("nuget_fetch", "nuget")

	rValue := map[string][]string{}
	for _, p := range packages {
		k := fmt.Sprintf("%s:%s", p.Name, p.Version.Raw)
		tfms := make([]string, len(p.Tfms))
		if false {
			log.Printf("foo")
		}
		i := 0
		for tfm, _ := range p.Tfms {
			tfms[i] = tfm
			i++
		}
		sort.Strings(tfms)
		rValue[k] = tfms
	}
	r.SetAttr("packages", rValue)

	res.Gen = []*rule.Rule{r}
	return res
}

func (d *dotnetLang) CanImport(p string) bool {
	return path.Base(p) == "package_report.json"
}
