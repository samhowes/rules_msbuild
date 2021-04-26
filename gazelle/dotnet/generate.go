package dotnet

import (
	"encoding/json"
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/config"
	"io/fs"
	"io/ioutil"
	"log"
	"path"
	"sort"
	"strings"

	"github.com/bazelbuild/bazel-gazelle/label"
	"github.com/bazelbuild/bazel-gazelle/language"
	"github.com/bazelbuild/bazel-gazelle/rule"
	bzl "github.com/bazelbuild/buildtools/build"
	"github.com/samhowes/my_rules_dotnet/gazelle/dotnet/project"
)

// GenerateRules extracts build metadata from source files in a directory.
// GenerateRules is called in each directory where an update is requested
// in depth-first post-order.
//
// args contains the arguments for GenerateRules. This is passed as a
// struct to avoid breaking implementations in the future when new
// fields are added.
//
// A GenerateResult struct is returned. Optional fields may be added to this
// type in the future.
//
// Any non-fatal errors this function encounters should be logged using
// log.Print.
func (d dotnetLang) GenerateRules(args language.GenerateArgs) language.GenerateResult {
	res := language.GenerateResult{}
	info := getInfo(args.Config)
	if info == nil {
		return res
	}
	var projectFile string
	for _, f := range append(args.RegularFiles, args.GenFiles...) {
		if strings.HasSuffix(f, "proj") {
			projectFile = f
			continue
		}

		switch strings.ToLower(path.Base(f)) {
		case "launchsettings.json":
			continue
		}

		info.Exts[path.Ext(f)] = true
	}

	if args.Rel == "" {
		writePackageReport(args.Config)
	}

	if projectFile == "" {
		// must be subdirectory of a project
		return res
	}

	dirtyPath := path.Join(args.Dir, projectFile)

	// squash the error, we know we're under the repo root
	l, _ := project.NormalizePath(dirtyPath, args.Config.RepoRoot)
	proj, err := project.Load(dirtyPath)
	if err != nil {
		log.Printf("%s: failed to parse project file. Skipping. This may result in incomplete build "+
			"definitions. Parsing error: %v", projectFile, err)
		return res
	}
	proj.FileLabel = l
	info.Project = proj
	res.Imports = append(res.Imports, processDeps(args, proj))
	proj.CollectFiles(info, "")

	var kind string
	if proj.IsTest {
		kind = "dotnet_test"
	} else if proj.IsExe {
		kind = "dotnet_binary"
	} else {
		kind = "dotnet_library"
	}

	r := rule.NewRule(kind, proj.Name)
	res.Gen = append(res.Gen, r)
	if proj.IsExe {
		p := rule.NewRule("dotnet_publish", "publish")
		p.SetAttr("target", ":"+proj.Name)
		res.Gen = append(res.Gen, p)
		res.Imports = append(res.Imports, []interface{}{})
	}

	for _, u := range proj.GetUnsupported() {
		r.AddComment(commentErr(u))
	}

	for key, value := range proj.Files {
		sort.Strings(value)
		r.SetAttr(key, makeGlob(value, []string{}))
	}

	r.SetAttr("visibility", []string{"//visibility:public"})
	r.SetAttr("target_framework", proj.TargetFramework)
	if proj.Sdk != "" {
		r.SetAttr("sdk", proj.Sdk)
	}
	if len(proj.Data) > 0 {
		r.SetAttr("data", makeGlob(proj.Data, []string{}))
	}

	return res
}

func writePackageReport(config *config.Config) {
	dc := getConfig(config)
	if dc.packageReportFile == "" {
		return
	}

	c, err := json.MarshalIndent(dc.packages, "", "    ")
	if err == nil {
		err = ioutil.WriteFile(dc.packageReportFile, c, fs.ModePerm)
	}
	if err != nil {
		log.Panicf("failed to write package report: %v", err)
	}

}

func processDeps(args language.GenerateArgs, proj *project.Project) []interface{} {
	dc := getConfig(args.Config)
	var deps []interface{}
	for _, ig := range proj.ItemGroups {
		for _, ref := range ig.ProjectReferences {
			dep := projectDep{}

			dep.Comments = ref.Unsupported.Append(dep.Comments, "")

			l, err := project.NormalizePath(path.Join(args.Dir, ref.Include), args.Config.RepoRoot)
			if err != nil {
				dep.Label = label.NoLabel
				dep.Comments = append(dep.Comments, fmt.Sprintf("could not add project reference: %v", err))
				continue
			}
			dep.Label = l
			deps = append(deps, dep)
		}
		for _, ref := range ig.PackageReferences {
			dep := projectDep{IsPackage: true}
			dep.Comments = ref.Unsupported.Append(dep.Comments, "")

			switch strings.ToLower(ref.Include) {
			case "microsoft.net.test.sdk":
				proj.IsTest = true
			}

			dc.recordPackage(ref, proj.TargetFramework)

			dep.Label = label.Label{
				Repo: "nuget",
				Pkg:  ref.Include,
				Name: ref.Include,
			}
			deps = append(deps, dep)
		}
	}
	return deps
}

// makeGlob returns a `glob([], exclude=[])` expression
// the default ExprFromValue produces a `glob([], "excludes": [])` expression
func makeGlob(include, exclude []string) bzl.Expr {
	patternsValue := rule.ExprFromValue(include)
	globArgs := []bzl.Expr{patternsValue}
	if len(exclude) > 0 {
		excludesValue := rule.ExprFromValue(exclude)
		globArgs = append(globArgs, &bzl.AssignExpr{
			LHS: &bzl.Ident{Name: "exclude"},
			Op:  "=",
			RHS: excludesValue,
		})
	}
	return &bzl.CallExpr{
		X:    &bzl.LiteralExpr{Token: "glob"},
		List: globArgs,
	}
}
