package dotnet

import (
	"fmt"
	"github.com/bazelbuild/bazel-gazelle/rule"
	"log"
	"path"
	"path/filepath"
	"strings"

	"github.com/bazelbuild/bazel-gazelle/label"
	"github.com/bazelbuild/bazel-gazelle/language"
	"github.com/samhowes/rules_msbuild/gazelle/dotnet/project"
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
func (d *dotnetLang) GenerateRules(args language.GenerateArgs) language.GenerateResult {
	res := language.GenerateResult{}
	info := getInfo(args.Config)
	if info == nil {
		return res
	}
	for _, f := range append(args.RegularFiles, args.GenFiles...) {
		if strings.HasSuffix(f, "proj") {
			info.Project = loadProject(args, f)
			info.Project.Directory = info
			continue
		}

		switch strings.ToLower(path.Base(f)) {
		case "launchsettings.json":
			continue
		}

		ext := path.Ext(f)
		info.Exts[ext] = append(info.Exts[ext], f)
	}

	dc := getConfig(args.Config)
	generateDirectoryDefaults(args, info, &res)
	if args.Rel == "" {

		if dc.macroFileName != "" {
			// we've collected all the package information by now, we can store it in the macro
			d.customUpdateRepos(args)
		}
	}

	for _, r := range args.OtherGen {
		if r.Kind() == "proto_library" {
			info.Protos = append(info.Protos, r)
		}
	}

	if info.Project == nil {
		return res
	}

	r := info.Project.GenerateRule(args.File)
	res.Gen = append(res.Gen, r)

	res.Imports = append(res.Imports, info.Project.Deps)
	if info.Project.TargetFramework != "" {
		dc.frameworks[info.Project.TargetFramework] = true
	}

	return res
}

func generateDirectoryDefaults(args language.GenerateArgs, info *project.DirectoryInfo, res *language.GenerateResult) {
	props := append(info.Exts[".props"], info.Exts[".targets"]...)
	var projects []*project.Project
	deps := map[string]*projectDep{}
	for _, p := range props {
		lower := strings.ToLower(p)
		switch lower {
		case "directory.build.targets":
		case "directory.build.props":
			fallthrough
		default:
			proj := loadProject(args, p)
			if proj == nil {
				continue
			}
			log.Printf(proj.Rel)
			projects = append(projects, proj)
			for _, d := range proj.Deps {
				dep := d.(*projectDep)
				if dep.Label.Pkg != proj.FileLabel.Pkg {
					deps[dep.Label.String()] = dep
				}
			}
		}
	}

	if len(projects) > 0 {
		r := rule.NewRule("msbuild_directory", "msbuild_defaults")
		r.SetAttr("srcs", props)
		res.Gen = append(res.Gen, r)
		var imports []interface{}
		for _, d := range deps {
			imports = append(imports, d)
		}
		res.Imports = append(res.Imports, imports)
	}
}

func loadProject(args language.GenerateArgs, projectFile string) *project.Project {
	// squash the error, we know we're under the repo root
	l, _ := project.GetLabel(project.Forward(args.Dir), projectFile, project.Forward(args.Config.RepoRoot))
	proj, err := project.Load(filepath.Join(args.Dir, projectFile))
	if err != nil {
		log.Printf("%s: failed to parse project file. Skipping. This may result in incomplete build "+
			"definitions. Parsing error: %v", projectFile, err)
		return nil
	}
	proj.FileLabel = &l

	processDeps(args, proj)
	return proj
}

func processDeps(args language.GenerateArgs, proj *project.Project) {
	dir := project.Forward(args.Dir)
	repoRoot := project.Forward(args.Config.RepoRoot)

	addDep := func(unsupported project.Unsupported, str string, isImport bool) {
		dep := projectDep{IsImport: isImport}
		dep.Comments = unsupported.Append(dep.Comments, "")

		l, err := project.GetLabel(dir, str, repoRoot)
		proj.Deps = append(proj.Deps, &dep)
		if err != nil {
			dep.Label = label.NoLabel
			dep.Comments = append(dep.Comments, fmt.Sprintf("could not add project reference: %v", err))
			return
		}
		dep.Label = l
	}

	dc := getConfig(args.Config)
	for _, i := range proj.Imports {
		i.Evaluate(proj)
		addDep(i.Unsupported, i.Project, true)
	}
	for _, ig := range proj.ItemGroups {
		for _, ref := range ig.ProjectReferences {
			ref.Evaluate(proj)
			addDep(ref.Unsupported, ref.Include, false)
		}
		for _, ref := range ig.PackageReferences {
			dep := projectDep{IsPackage: true}
			dep.Comments = ref.Unsupported.Append(dep.Comments, "")

			ref.Evaluate(proj)
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
			proj.Deps = append(proj.Deps, &dep)
		}
	}
}
