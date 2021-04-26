package dotnet

import (
	"encoding/json"
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/config"
	"log"
	"os"
	"path"
	"path/filepath"
	"sort"

	"github.com/bazelbuild/bazel-gazelle/language"
	"github.com/bazelbuild/bazel-gazelle/merger"
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
	var packages map[string]*project.NugetSpec
	res := language.ImportReposResult{}
	c, err := os.ReadFile(args.Path)
	res.Error = err
	if res.Error == nil {
		res.Error = json.Unmarshal(c, &packages)
	}
	if res.Error != nil {
		return res
	}

	return importReposImpl(packages)

}

func (d dotnetLang) customUpdateRepos(c *config.Config) {
	dc := getConfig(c)
	res := importReposImpl(dc.packages)

	var macroPath string
	if dc.macroFileName != "" {
		macroPath = filepath.Join(c.RepoRoot, filepath.Clean(dc.macroFileName))
	}

	f, err := rule.LoadMacroFile(macroPath, "", dc.macroDefName)
	if os.IsNotExist(err) {
		f, err = rule.EmptyMacroFile(macroPath, "", dc.macroDefName)
		if err != nil {
			log.Fatalf("error creating %q: %v", macroPath, err)
		}
	} else if err != nil {
		log.Fatalf("error loading %q: %v", macroPath, err)
	}
	merger.MergeFile(f, res.Empty, res.Gen, merger.PostResolve, kinds)
	merger.FixLoads(f, d.Loads())
	f.Sync()
	f.SortMacro()
	if err := f.Save(f.Path); err != nil {
		log.Fatalf("error safing %s: %v", f.Path, err)
	}
}

func importReposImpl(packages map[string]*project.NugetSpec) language.ImportReposResult {
	r := rule.NewRule("nuget_fetch", "nuget")

	rValue := map[string][]string{}
	for _, p := range packages {
		k := fmt.Sprintf("%s:%s", p.Name, p.Version.Raw)
		tfms := make([]string, len(p.Tfms))
		i := 0
		for tfm, _ := range p.Tfms {
			tfms[i] = tfm
			i++
		}
		sort.Strings(tfms)
		rValue[k] = tfms
	}
	r.SetAttr("packages", rValue)

	res := language.ImportReposResult{
		Gen: []*rule.Rule{r},
	}
	return res
}

func (d *dotnetLang) CanImport(p string) bool {
	return path.Base(p) == "package_report.json"
}
