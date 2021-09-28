package dotnet

import (
	"encoding/json"
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/config"
	bzl "github.com/bazelbuild/buildtools/build"
	"log"
	"os"
	"path"
	"path/filepath"
	"sort"
	"strings"

	"github.com/bazelbuild/bazel-gazelle/language"
	"github.com/bazelbuild/bazel-gazelle/merger"
	"github.com/bazelbuild/bazel-gazelle/rule"
	"github.com/samhowes/rules_msbuild/gazelle/dotnet/project"
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

	return importReposImpl(packages, nil)

}

func (d *dotnetLang) customUpdateRepos(c *config.Config) {
	dc := getConfig(c)
	res := importReposImpl(dc.packages, dc.frameworks)

	var macroPath string
	if dc.macroFileName != "" {
		macroPath = filepath.Join(c.RepoRoot, filepath.Clean(dc.macroFileName))
	}

	f, err := rule.LoadMacroFile(macroPath, "", dc.macroDefName)
	if os.IsNotExist(err) {
		directory := filepath.Dir(macroPath)
		if err = os.MkdirAll(directory, os.ModePerm); err != nil {
			log.Fatalf("error creating directory %s: %v", directory, err)
		}

		f, err = rule.EmptyMacroFile(macroPath, "", dc.macroDefName)
		if err != nil {
			log.Fatalf("error creating %s: %v", macroPath, err)
		}
	} else if err != nil {
		log.Fatalf("error loading %q: %v", macroPath, err)
	}

	match, err := merger.Match(f.Rules, res.Gen[0], kinds["nuget_fetch"])
	if match != nil {
		// apparently the default code doesn't like merging our dictionary of lists
		// we'll do this manually
		mergePackages(res.Gen[0], match)
	}

	// merge just in case we missed something we didn't know about before
	merger.MergeFile(f, res.Empty, res.Gen, merger.PreResolve, kinds)
	merger.FixLoads(f, d.Loads())
	f.Sync()
	f.SortMacro()
	if err := f.Save(f.Path); err != nil {
		log.Fatalf("error saving %s: %v", f.Path, err)
	}

	workspace, err := rule.LoadWorkspaceFile("WORKSPACE", "")
	if err != nil {
		log.Fatalf("error loading WORKSPACE: %v", err)
	}

	if ensureMacroInWorkspace(dc, workspace, len(workspace.Loads)-1) {
		workspace.Sync()

		if err := workspace.Save(workspace.Path); err != nil {
			log.Fatalf("error saving %s: %v", workspace.Path, err)
		}
	}
}

// ensureMacroInWorkspace adds a call to the repository macro if the -to_macro
// flag was used, and the macro was not called or declared with a
// '# gazelle:repository_macro' directive.
//
// ensureMacroInWorkspace returns true if the WORKSPACE file was updated
// and should be saved.
func ensureMacroInWorkspace(uc *dotnetConfig, workspace *rule.File, insertIndex int) (updated bool) {
	if uc.macroFileName == "" {
		return false
	}

	// Check whether the macro is already declared.
	// We won't add a call if the macro is declared but not called. It might
	// be called somewhere else.
	macroValue := uc.macroFileName + "%" + uc.macroDefName
	for _, d := range workspace.Directives {
		if d.Key == "repository_macro" && d.Value == macroValue {
			return false
		}
	}

	// Try to find a load and a call.
	var load *rule.Load
	var call *rule.Rule
	var loadedDefName string
	for _, l := range workspace.Loads {
		switch l.Name() {
		case ":" + uc.macroFileName, "//:" + uc.macroFileName, "@//:" + uc.macroFileName:
			load = l
			pairs := l.SymbolPairs()
			for _, pair := range pairs {
				if pair.From == uc.macroDefName {
					loadedDefName = pair.To
				}
			}
		}
	}

	for _, r := range workspace.Rules {
		if r.Kind() == loadedDefName {
			call = r
		}
	}

	// Add the load and call if they're missing.
	if call == nil {
		if load == nil {
			load = rule.NewLoad("//:" + uc.macroFileName)
			load.Insert(workspace, insertIndex)
		}
		if loadedDefName == "" {
			load.Add(uc.macroDefName)
		}

		call = rule.NewRule(uc.macroDefName, "")
		call.InsertAt(workspace, insertIndex)
	}

	// Add the directive to the call.
	call.AddComment("# gazelle:repository_macro " + macroValue)

	return true
}

func mergePackages(gen *rule.Rule, old *rule.Rule) {
	allKeys := map[string]bool{}
	var allKeysOrder []string

	type pkgSpec struct {
		name    string
		version string
		expr    *bzl.KeyValueExpr
	}

	getPackageMap := func(r *rule.Rule) (map[string]*pkgSpec, *bzl.DictExpr) {
		// assume it has the required attribute
		de := r.Attr("packages").(*bzl.DictExpr)
		pl := de.List
		m := make(map[string]*pkgSpec, len(pl))
		for _, kv := range pl {
			k := kv.Key.(*bzl.StringExpr).Value

			parts := strings.Split(k, ":")
			spec := pkgSpec{
				name: parts[0],
				expr: kv,
			}
			if len(parts) == 2 {
				spec.version = parts[1]
			}

			lower := strings.ToLower(spec.name)
			if !allKeys[lower] {
				allKeys[lower] = true
				allKeysOrder = append(allKeysOrder, lower)
			}

			m[lower] = &spec
		}
		return m, de
	}

	gp, _ := getPackageMap(gen)
	op, expr := getPackageMap(old)

	// discard the old list, we'll recompose it
	expr.List = []*bzl.KeyValueExpr{}

	sort.Strings(allKeysOrder)
	for _, k := range allKeysOrder {
		oldSpec, exists := op[k]
		if !exists {
			// this value didn't exist before
			expr.List = append(expr.List, gp[k].expr)
			continue
		}

		genSpec, exists := gp[k]
		if !exists {
			// this value was removed
			if rule.ShouldKeep(oldSpec.expr) {
				expr.List = append(expr.List, oldSpec.expr)
			}
			continue
		}

		// the value was updated
		ol := oldSpec.expr.Value.(*bzl.ListExpr)
		gl := genSpec.expr.Value.(*bzl.ListExpr)

		// make fake rules so we can get rule to merge the lists for us
		fakeSrc := rule.NewRule("foo", "foo")
		fakeSrc.SetAttr("list", gl)
		fakeDest := rule.NewRule("foo", "foo")
		fakeDest.SetAttr("list", ol)
		rule.MergeRules(fakeSrc, fakeDest, map[string]bool{"list": true}, "")

		oldSpec.expr.Value = fakeDest.Attr("list")
		if oldSpec.version == "" {
			// maybe the user edited it incorrectly
			oldSpec.version = genSpec.version
		} else if oldSpec.version != genSpec.version {
			log.Printf(`<PackageReference Include="%s" Version="%s" />`,
				oldSpec.name, oldSpec.version)
		}
		expr.List = append(expr.List, oldSpec.expr)
	}
}

func importReposImpl(packages map[string]*project.NugetSpec, frameworks map[string]bool) language.ImportReposResult {
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

	r.SetAttr("use_host", true)
	r.SetAttr("packages", rValue)
	frameworksList := make([]string, 0, len(frameworks))
	for k := range frameworks {
		frameworksList = append(frameworksList, k)
	}
	sort.Strings(frameworksList)
	r.SetAttr("target_frameworks", frameworksList)

	res := language.ImportReposResult{
		Gen: []*rule.Rule{r},
	}
	return res
}

func (d *dotnetLang) CanImport(p string) bool {
	return path.Base(p) == "package_report.json"
}
