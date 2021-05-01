package project

import (
	"sort"
	"strings"

	"github.com/bazelbuild/bazel-gazelle/rule"
	bzl "github.com/bazelbuild/buildtools/build"
	"github.com/samhowes/my_rules_dotnet/gazelle/dotnet/util"
)

func (p *Project) GenerateRules() []*rule.Rule {
	var kind string
	if p.IsTest {
		kind = "dotnet_test"
	} else if p.IsExe {
		kind = "dotnet_binary"
	} else {
		kind = "dotnet_library"
	}

	p.Rule = rule.NewRule(kind, p.Name)
	rules := []*rule.Rule{p.Rule}
	if p.IsExe {
		pub := rule.NewRule("dotnet_publish", "publish")
		pub.SetAttr("target", ":"+p.Name)
		rules = append(rules, pub)
	}

	p.ProcessItemGroup()
	p.SetProperties()

	for _, u := range p.GetUnsupported() {
		p.Rule.AddComment(util.CommentErr(u))
	}

	for key, value := range p.Files {
		sort.Strings(value)
		p.Rule.SetAttr(key, util.MakeGlob(util.MakeStringExprs(value), nil))
	}

	p.Rule.SetAttr("visibility", []string{"//visibility:public"})
	p.Rule.SetAttr("target_framework", p.TargetFramework)
	if p.Sdk != "" {
		p.Rule.SetAttr("sdk", p.Sdk)
	}
	if len(p.Data) > 0 {
		p.Rule.SetAttr("data", util.MakeGlob(util.MakeStringExprs(p.Data), nil))
	}

	return rules
}

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

func (p *Project) ProcessItemGroup() {
	var invalidComments []bzl.Comment
	var contentItems []bzl.Expr
	var includeGlobs []bzl.Expr
	var globs []bzl.Expr
	for _, ig := range p.ItemGroups {
		for _, i := range ig.Content {
			comments := util.CommentErrs(i.Unsupported.Messages("Content"))
			if i.Include == "" {
				invalidComments = append(invalidComments, comments...)
				continue
			}

			if strings.Contains(i.Include, "*") {
				if i.Exclude != "" {
					// Exclude attributes only apply to include attributes on the same element, Exclude on its own
					// element produces the following error:
					// MSB4232: items outside Target elements must have one of the following operations: Include, Update, or Remove
					g := util.MakeGlob(util.MakeStringExprs([]string{i.Include}), util.MakeStringExprs([]string{i.Exclude}))
					g.Comment().Before = comments
					globs = append(globs, g)
				} else {
					e := &bzl.StringExpr{Value: i.Include}
					e.Comment().Before = comments
					includeGlobs = append(includeGlobs, e)
				}
			} else {
				e := &bzl.StringExpr{Value: i.Include}
				e.Comment().Before = comments
				contentItems = append(contentItems, e)
			}
		}
	}

	var exprs []bzl.Expr
	if len(includeGlobs) > 0 {
		exprs = append(exprs, util.MakeGlob(includeGlobs, nil))
	}
	exprs = append(exprs, globs...)
	if expr := util.ListWithComments(contentItems, invalidComments); expr != nil {
		exprs = append(exprs, expr)
	}

	if len(exprs) <= 0 {
		return
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
	p.Rule.SetAttr("content", expr)
}
