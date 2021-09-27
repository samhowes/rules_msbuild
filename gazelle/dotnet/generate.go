package dotnet

import (
	"fmt"
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
			if info.Project != nil {
				res.Imports = append(res.Imports, info.Project.Deps)
			}
			continue
		}

		switch strings.ToLower(path.Base(f)) {
		case "launchsettings.json":
			continue
		}

		ext := path.Ext(f)
		if info.SrcsMode == project.Explicit {
			info.Exts[ext] = append(info.Exts[ext], f)
		} else {
			info.Exts[ext] = nil
		}
	}

	dc := getConfig(args.Config)
	if args.Rel == "" && dc.macroFileName != "" {
		// we've collected all the package information by now, we can store it in the macro
		d.customUpdateRepos(args.Config)
	}
	if info.Project == nil {
		return res
	}

	dc.frameworks[info.Project.TargetFramework] = true
	res.Gen = append(res.Gen, info.Project.GenerateRules(args.File)...)

	return res
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

	dc := getConfig(args.Config)
	for _, ig := range proj.ItemGroups {
		for _, ref := range ig.ProjectReferences {
			ref.Evaluate(proj)

			dep := projectDep{}

			dep.Comments = ref.Unsupported.Append(dep.Comments, "")

			l, err := project.GetLabel(dir, ref.Include, repoRoot)
			proj.Deps = append(proj.Deps, &dep)
			if err != nil {
				dep.Label = label.NoLabel
				dep.Comments = append(dep.Comments, fmt.Sprintf("could not add project reference: %v", err))
				continue
			}
			dep.Label = l
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
