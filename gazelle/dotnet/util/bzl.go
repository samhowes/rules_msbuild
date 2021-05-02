package util

import (
	"fmt"
	bzl "github.com/bazelbuild/buildtools/build"
	"sort"
	"strings"
)

func CommentErr(c string) string {
	return fmt.Sprintf("# gazelle-err: %s", c)
}

func CommentErrs(messages []string) []bzl.Comment {
	comments := make([]bzl.Comment, len(messages))
	for i, m := range messages {
		comments[i] = bzl.Comment{Token: CommentErr(m)}
	}
	return comments
}

func MakeStringExprs(values []string) []bzl.Expr {
	list := make([]bzl.Expr, len(values))
	for i, v := range values {
		list[i] = &bzl.StringExpr{Value: v}
	}
	return list
}

// MakeGlob returns a `glob([], exclude=[])` expression
// the default ExprFromValue produces a `glob([], "excludes": [])` expression
func MakeGlob(include, exclude []bzl.Expr) bzl.Expr {
	globArgs := []bzl.Expr{&bzl.ListExpr{List: SortExprs(include)}}
	if len(exclude) > 0 {
		globArgs = append(globArgs, &bzl.AssignExpr{
			LHS: &bzl.Ident{Name: "exclude"},
			Op:  "=",
			RHS: &bzl.ListExpr{List: SortExprs(exclude)},
		})
	}
	return &bzl.CallExpr{
		X:    &bzl.LiteralExpr{Token: "glob"},
		List: globArgs,
	}
}

func SortExprs(exprs []bzl.Expr) []bzl.Expr {
	sort.Slice(exprs, func(i, j int) bool {
		return strings.Compare(exprs[i].(*bzl.StringExpr).Value, exprs[j].(*bzl.StringExpr).Value) < 0
	})
	return exprs
}

// ListWithComments creates a bzl.ListExpr with value of list
// if both list and comments are empty, nil is returned
// if list is empty, an empty list is rendered that contains comments
// if list is non-empty, comments are placed at the beginning of the list
func ListWithComments(list []bzl.Expr, comments []bzl.Comment) *bzl.ListExpr {
	if len(list) == 0 && len(comments) == 0 {
		return nil
	}
	expr := bzl.ListExpr{List: SortExprs(list)}
	if len(comments) > 0 {
		var commented *bzl.Comments
		if len(list) > 0 {
			commented = list[0].Comment()
			commented.Before = append(comments, commented.Before...)
		} else {
			commented = expr.End.Comment()
			commented.Before = append(commented.Before, comments...)
		}
	}
	return &expr
}
