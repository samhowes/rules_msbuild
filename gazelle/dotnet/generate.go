package dotnet

import (
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/label"
	"github.com/bazelbuild/bazel-gazelle/language"
	"github.com/bazelbuild/bazel-gazelle/rule"
	bzl "github.com/bazelbuild/buildtools/build"
	"github.com/samhowes/my_rules_dotnet/gazelle/dotnet/project"
	"log"
	"path"
	"sort"
	"strings"
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
	info := getInfo(args.Config)
	if info == nil {
		return language.GenerateResult{}
	}
	var rules []*rule.Rule
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

	if projectFile == "" {
		// must be subdirectory of a project
		return language.GenerateResult{}
	}

	dirtyPath := path.Join(args.Dir, projectFile)

	// squash the error, we know we're under the repo root
	l, _ := project.NormalizePath(dirtyPath, args.Config.RepoRoot)
	proj, err := project.Load(dirtyPath)
	if err != nil {
		log.Printf("%s: failed to parse project file. Skipping. This may result in incomplete build definitions. Parsing error: %v", projectFile, err)
		return language.GenerateResult{}
	}
	proj.FileLabel = l
	info.Project = proj

	var kind string
	if proj.Executable {
		kind = "dotnet_binary"
	} else {
		kind = "dotnet_library"
	}

	r := rule.NewRule(kind, proj.Name)

	for _, u := range proj.GetUnsupported() {
		r.AddComment(fmt.Sprintf("# gazelle-err: %s", u))
	}

	deps := processDeps(args, proj)
	proj.CollectFiles(info, "")

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

	rules = append(rules, r)

	log.Println(r.Name())
	return language.GenerateResult{
		Gen:     rules,
		Imports: []interface{}{deps},
	}
}

func processDeps(args language.GenerateArgs, proj *project.Project) []interface{} {
	var deps []interface{}
	for _, ig := range proj.ItemGroups {
		for _, ref := range ig.ProjectReferences {
			dep := projectDep{}

			dep.Comments = ig.Unsupported.Append(dep.Comments, "")

			l, err := project.NormalizePath(path.Join(args.Dir, ref.Include), args.Config.RepoRoot)
			if err != nil {
				dep.Label = label.NoLabel
				dep.Comments = append(dep.Comments, fmt.Sprintf("# gazelle: could not add project reference: %v", err))
				continue
			}
			dep.Label = l
			deps = append(deps, dep)
		}
		for _, ref := range ig.PackageReferences {
			dep := projectDep{IsPackage: true}
			dep.Comments = ig.Unsupported.Append(dep.Comments, "")
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

func ensureAttr(r *rule.Rule, name string, defaultValue interface{}) bzl.Expr {
	attr := r.Attr(name)
	if attr == nil {
		r.SetAttr(name, defaultValue)
		attr = r.Attr(name)
	}
	return attr
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
