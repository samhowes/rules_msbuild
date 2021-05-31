package project

import (
	"fmt"
	"strings"

	"github.com/bazelbuild/bazel-gazelle/rule"
	bzl "github.com/bazelbuild/buildtools/build"
	"github.com/samhowes/my_rules_dotnet/gazelle/dotnet/util"
)

func (p *Project) GenerateRules(f *rule.File) []*rule.Rule {
	var kind string
	if p.IsTest {
		kind = "msbuild_test"
	} else if p.IsExe {
		kind = "msbuild_binary"
	} else {
		kind = "msbuild_library"
	}

	p.Rule = rule.NewRule(kind, p.Name)
	rules := []*rule.Rule{p.Rule}

	p.ProcessItemGroup("Compile", func(ig *ItemGroup) []*Item { return ig.Compile })
	p.ProcessItemGroup("Content", func(ig *ItemGroup) []*Item { return ig.Content })

	p.CollectFiles(p.Directory, "")

	p.SetFileAttributes()

	for _, u := range p.GetUnsupported() {
		p.Rule.AddComment(util.CommentErr(u))
	}

	if (f == nil || !f.HasDefaultVisibility()) && !p.IsTest {
		p.Rule.SetAttr("visibility", []string{"//visibility:public"})
	}

	p.Rule.SetAttr("target_framework", p.TargetFramework)
	if len(p.Data) > 0 {
		p.Rule.SetAttr("data", util.MakeGlob(util.MakeStringExprs(p.Data), nil))
	}

	return rules
}

// todo: delete this when I decide to not re-introduce msbuild_properties
func (p *Project) SetProperties() {
	var exprs []*bzl.KeyValueExpr
	for _, pg := range p.PropertyGroups {
		for _, prop := range pg.Properties {
			name := prop.XMLName.Local
			if SpecialProperties[name] {
				continue
			}
			e := bzl.KeyValueExpr{
				Comments: bzl.Comments{Before: util.CommentErrs(prop.Unsupported.Messages("property"))},
				Key:      &bzl.StringExpr{Value: name},
				Value:    &bzl.StringExpr{Value: prop.Value},
			}
			exprs = append(exprs, &e)
		}
	}
	if len(exprs) > 0 {
		p.Rule.SetAttr("msbuild_properties", &bzl.DictExpr{List: exprs})
	}
}

func (p *Project) ProcessItemGroup(fgKey string, getItems func(ig *ItemGroup) []*Item) {
	for _, ig := range p.ItemGroups {
		for _, i := range getItems(ig) {
			p.EvaluateItem(i)
			itemType := i.XMLName.Local
			fg := p.GetFileGroup(itemType)
			comments := util.CommentErrs(i.Unsupported.Messages(itemType))
			if i.Remove != "" {
				fg.Filters = append(fg.Filters, forceSlash(i.Remove))
			}

			if i.Include == "" {
				fg.Comments = append(fg.Comments, comments...)
				continue
			}

			include := forceSlash(i.Include)

			if strings.Contains(include, "*") {
				if i.Exclude != "" {
					// Exclude attributes only apply to include attributes on the same element, Exclude on its own
					// element produces the following error:
					// MSB4232: items outside Target elements must have one of the following operations: Include, Update, or Remove
					g := util.MakeGlob(util.MakeStringExprs([]string{include}), util.MakeStringExprs([]string{forceSlash(i.Exclude)}))
					g.Comment().Before = comments
					fg.Globs = append(fg.Globs, g)
				} else {
					e := &bzl.StringExpr{Value: include}
					e.Comment().Before = comments
					fg.IncludeGlobs = append(fg.IncludeGlobs, e)
				}
			} else {
				e := &bzl.StringExpr{Value: include}
				e.Comment().Before = comments
				fg.Explicit = append(fg.Explicit, e)
			}
		}
	}
	p.srcsModes[fgKey] = p.Directory.SrcsMode
	if _, exists := p.Files[fgKey]; exists {
		// the user explicitly specified some files for these items, rather than doing some complicated globbing logic
		// later, we'll just upgrade the srcs_mode for the directory tree
		if p.Directory.SrcsMode == Implicit {
			p.srcsModes[fgKey] = Folders
		}
	}
}

func forceSlash(p string) string {
	return strings.ReplaceAll(p, "\\", "/")
}

func (p *Project) SetFileAttributes() {
	for _, fg := range p.Files {
		var exprs []bzl.Expr
		if len(fg.IncludeGlobs) > 0 {
			exprs = append(exprs, util.MakeGlob(fg.IncludeGlobs, nil))
		}
		exprs = append(exprs, fg.Globs...)
		if expr := util.ListWithComments(fg.Explicit, fg.Comments); expr != nil {
			exprs = append(exprs, expr)
		}

		if len(exprs) <= 0 {
			// print `srcs = []` to prevent the macro from implicitly globbing
			exprs = append(exprs, &bzl.ListExpr{List: []bzl.Expr{}})
		}

		expr := exprs[0]
		if len(exprs) > 1 {
			for _, e := range exprs[1:] {
				expr = &bzl.BinaryExpr{
					X:  expr,
					Op: "+",
					Y:  e,
				}
			}
		}
		var key string
		switch fg.ItemType {
		case "Compile":
			key = "srcs"
		case "Content":
			key = "content"
		default:
			// should not happen
			p.Rule.AddComment(util.CommentErr(fmt.Sprintf("unkown item type %s please file an issue", fg.ItemType)))
			continue
		}
		p.Rule.SetAttr(key, expr)
	}

}
