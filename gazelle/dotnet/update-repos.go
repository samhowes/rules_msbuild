package dotnet

import (
	"encoding/json"
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/label"
	bzl "github.com/bazelbuild/buildtools/build"
	"io/ioutil"
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

	res, _ = importReposImpl(packages, nil, nil, language.GenerateArgs{})
	return res

}

func (d *dotnetLang) customUpdateRepos(args language.GenerateArgs) {
	dc := getConfig(args.Config)
	var macroPath string
	if dc.macroFileName != "" {
		macroPath = strings.Replace(dc.macroFileName, ":", "/", -1)
		macroPath = filepath.Join(args.Config.RepoRoot, filepath.Clean(macroPath))
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

	res, loads := importReposImpl(dc.packages, dc.frameworks, f, args)
	match, err := merger.Match(f.Rules, res.Gen[0], kinds["nuget_fetch"])
	if match != nil {
		// apparently the default code doesn't like merging our dictionary of lists
		// we'll do this manually
		//mergePackages(res.Gen[0], match)
	}

	// merge just in case we missed something we didn't know about before
	merger.MergeFile(f, res.Empty, res.Gen, merger.PreResolve, kinds)
	fixLoads(f, loads)

	f.Sync()
	f.SortMacro()
	if err := f.Save(f.Path); err != nil {
		log.Fatalf("error saving %s: %v", f.Path, err)
	}

	workspace, err := rule.LoadWorkspaceFile(filepath.Join(args.Config.RepoRoot, "WORKSPACE"), "")
	if err != nil {
		log.Fatalf("error loading WORKSPACE: %v", err)
	}

	workspaceIndex := len(workspace.Loads) + len(workspace.Rules)
	if ensureMacroInWorkspace(dc, workspace, workspaceIndex) {
		workspace.Sync()

		if err := workspace.Save(workspace.Path); err != nil {
			log.Fatalf("error saving %s: %v", workspace.Path, err)
		}
	}
}

func fixLoads(f *rule.File, loads []*rule.Load) {
	files := map[string]*rule.Load{}
	var names []string
	symbols := map[*rule.Load]map[string]bool{}
	for _, l := range f.Loads {
		files[l.Name()] = l
		names = append(names, l.Name())
		l.Delete()
		ls := map[string]bool{}
		for _, s := range l.Symbols() {
			ls[s] = true
		}
		symbols[l] = ls
	}

	for _, l := range loads {
		names = append(names, l.Name())
		e, ok := files[l.Name()]
		files[l.Name()] = l
		if ok {
			for _, s := range l.Symbols() {
				e.Add(s)
			}
		}
	}
	sort.Strings(names)
	for _, n := range names {
		l := files[n]
		l.Insert(f, 0)
	}
}

// ensureMacroInWorkspace adds a call to the repository macro if the -to_macro
// flag was used, and the macro was not called or declared with a
// '# gazelle:nuget_macro' directive.
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
		if d.Key == "nuget_macro" && d.Value == macroValue {
			return false
		}
	}

	// Try to find a load and a call.
	var load *rule.Load
	var call *rule.Rule
	var loadedDefName string
	for _, l := range workspace.Loads {
		switch l.Name() {
		case ":" + uc.macroFileName, "//:" + uc.macroFileName, "@//:" + uc.macroFileName, "//" + uc.macroFileName:
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
			loadValue := uc.macroFileName
			if strings.IndexRune(loadValue, ':') < 0 {
				loadValue = ":" + loadValue
			}
			load = rule.NewLoad("//" + loadValue)
			load.Insert(workspace, insertIndex)
			insertIndex++
		}
		if loadedDefName == "" {
			load.Add(uc.macroDefName)
		}

		call = rule.NewRule(uc.macroDefName, "")
		call.InsertAt(workspace, insertIndex)
	}

	// Add the directive to the call.
	call.AddComment("# gazelle:nuget_macro " + macroValue)

	return true
}

func importReposImpl(packages map[string]*project.NugetSpec, frameworks map[string]bool, f *rule.File, args language.GenerateArgs) (language.ImportReposResult, []*rule.Load) {
	var r *rule.Rule
	for _, e := range f.Rules {
		if e.Kind() == "nuget_fetch" {
			r = e
			break
		}
	}

	if r == nil {
		r = rule.NewRule("nuget_fetch", "nuget")
	}

	loaded := map[string]string{}
	for _, l := range f.Loads {
		for _, s := range l.Symbols() {
			loaded[s] = l.Name()
		}
	}

	pkg := filepath.Dir(f.Path[len(args.Dir)+1:])
	if filepath.Separator == '\\' {
		pkg = strings.ReplaceAll(pkg, "\\", "/")
	}
	deps := r.Attr("deps")
	var publicDeps map[string]map[string]bool
	if deps != nil {
		publicDeps = loadReferencedPackages(deps, loaded, args, pkg)
	} else {
		publicDeps = map[string]map[string]bool{}
	}

	packagesMap := map[string]map[string]bool{}
	var pkgIds []string
	for pkgName, nuspec := range packages {
		pkgId := fmt.Sprintf("%s/%s", pkgName, nuspec.Version.Raw)
		var tfms map[string]bool
		tfms, ok := publicDeps[pkgId]
		if !ok {
			tfms, ok = packagesMap[pkgId]
			pkgIds = append(pkgIds, pkgId)
			if !ok {
				tfms = map[string]bool{}
				packagesMap[pkgId] = tfms
			}
		}

		for tfm, used := range nuspec.Tfms {
			if used {
				tfms[tfm] = true
			}
		}
	}
	sort.Strings(pkgIds)

	var packagesExpr []*bzl.KeyValueExpr
	for _, pkgId := range pkgIds {
		tfmMap := packagesMap[pkgId]
		var tfms []string
		for tfm, used := range tfmMap {
			if used {
				tfms = append(tfms, tfm)
			}
		}
		sort.Strings(tfms)
		var tfmsExpr []bzl.Expr
		for _, tfm := range tfms {
			tfmsExpr = append(tfmsExpr, &bzl.StringExpr{Value: tfm})
		}
		packagesExpr = append(packagesExpr, &bzl.KeyValueExpr{
			Key:   &bzl.StringExpr{Value: pkgId},
			Value: &bzl.ListExpr{List: tfmsExpr},
		})
	}
	r.SetAttr("packages", &bzl.DictExpr{List: packagesExpr, ForceMultiLine: true})

	r.SetAttr("use_host", true)
	frameworksList := make([]string, 0, len(frameworks))
	for k := range frameworks {
		frameworksList = append(frameworksList, k)
	}
	sort.Strings(frameworksList)
	r.SetAttr("target_frameworks", frameworksList)

	r.SetAttr("deps", &bzl.CallExpr{
		X: &bzl.Ident{Name: "nuget_deps_helper"},
		List: []bzl.Expr{
			&bzl.Ident{Name: "FRAMEWORKS"},
			&bzl.Ident{Name: "PACKAGES"},
		},
	})

	var loads []*rule.Load
	l := rule.NewLoad("@rules_msbuild//deps:public_nuget.bzl")
	l.Add("FRAMEWORKS")
	l.Add("PACKAGES")
	loads = append(loads, l)

	l = rule.NewLoad("@rules_msbuild//dotnet:defs.bzl")

	l.Add("nuget_deps_helper")
	l.Add("nuget_fetch")
	loads = append(loads, l)

	res := language.ImportReposResult{
		Gen: []*rule.Rule{r},
	}
	return res, loads
}

func loadReferencedPackages(deps bzl.Expr, loaded map[string]string, args language.GenerateArgs, pkg string) map[string]map[string]bool {
	var calls []*bzl.CallExpr
	switch t := deps.(type) {
	default:
		log.Printf("unknown deps type for `nuget_fetch` %T", t)
		return nil
	case *bzl.CallExpr:
		calls = append(calls, deps.(*bzl.CallExpr))
	}

	for _, call := range calls {
		ident := call.X.(*bzl.Ident)
		if ident.Name != "nuget_deps_helper" {
			log.Printf("unkown macro call %s", ident.Name)
			continue
		}

		packagesIdent := call.List[1].(*bzl.Ident)

		name, ok := loaded[packagesIdent.Name]
		if !ok {
			continue
		}

		l, err := label.Parse(name)
		if err != nil {
			log.Printf("failed to parse load of %s as label: %v", name, err)
			continue
		}

		if l.Repo != args.Config.RepoName {
			continue
		}

		if l.Relative {
			l.Pkg = pkg
		}

		fPath := filepath.Join(args.Config.RepoRoot, l.Pkg, l.Name)
		content, err := ioutil.ReadFile(fPath)
		if err != nil {
			log.Printf("error loading %s: %v", l, err)
			continue
		}
		ast, err := bzl.ParseBuild(fPath, content)
		if err != nil {
			log.Printf("error loading %s: %v", l, err)
			continue
		}
		var loadedPackagesExpr *bzl.DictExpr
		for _, s := range ast.Stmt {
			switch a := s.(type) {
			case *bzl.AssignExpr:
				i := a.LHS.(*bzl.Ident)

				if i != nil && i.Name == packagesIdent.Name {
					loadedPackagesExpr = a.RHS.(*bzl.DictExpr)
					break
				}
			}
		}
		if loadedPackagesExpr == nil {
			return nil
		}

		loadedPackages := map[string]map[string]bool{}
		for _, i := range loadedPackagesExpr.List {
			pkgId := i.Key.(*bzl.StringExpr).Value
			tfms, ok := loadedPackages[pkgId]
			if !ok {
				tfms = map[string]bool{}
				loadedPackages[pkgId] = tfms
			}

			tfmsExpr := i.Value.(*bzl.ListExpr)
			if tfmsExpr == nil {
				continue
			}
			for _, tfm := range tfmsExpr.List {
				str := tfm.(*bzl.StringExpr)
				if str == nil {
					continue
				}
				tfms[str.Value] = true
			}
		}

		return loadedPackages
	}
	return nil
}

func (d *dotnetLang) CanImport(p string) bool {
	return path.Base(p) == "package_report.json"
}
