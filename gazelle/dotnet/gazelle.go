package dotnet

import (
	"flag"
	"github.com/bazelbuild/bazel-gazelle/config"
	"github.com/bazelbuild/bazel-gazelle/label"
	"github.com/bazelbuild/bazel-gazelle/language"
	"github.com/bazelbuild/bazel-gazelle/repo"
	"github.com/bazelbuild/bazel-gazelle/resolve"
	"github.com/bazelbuild/bazel-gazelle/rule"
)

const languageName = "dotnet"

type dotnetLang struct{}

// NewLanguage is called by gazelle to install this language extension in a binary
func NewLanguage() language.Language {
	return &dotnetLang{}
}

// ======== language.Language interface ========
func (d dotnetLang) RegisterFlags(fs *flag.FlagSet, cmd string, c *config.Config) {
	// todo(#84)
}

func (d dotnetLang) CheckFlags(fs *flag.FlagSet, c *config.Config) error {
	// todo(#84)
	return nil
}

func (d dotnetLang) KnownDirectives() []string {
	// todo(#84)
	return []string{}
}

func (d dotnetLang) Configure(c *config.Config, rel string, f *rule.File) {
	// todo(#84)
}

func (d dotnetLang) Name() string {
	return languageName
}

func (d dotnetLang) Imports(c *config.Config, r *rule.Rule, f *rule.File) []resolve.ImportSpec {
	// todo(#84)
	return []resolve.ImportSpec{}
}

func (d dotnetLang) Embeds(r *rule.Rule, from label.Label) []label.Label {
	// todo(#84)
	return []label.Label{}
}

func (d dotnetLang) Resolve(c *config.Config, ix *resolve.RuleIndex, rc *repo.RemoteCache, r *rule.Rule, imports interface{}, from label.Label) {
	// todo(#84)
}

func (d dotnetLang) Kinds() map[string]rule.KindInfo {
	// todo(#84)
	return map[string]rule.KindInfo{}
}

func (d dotnetLang) Loads() []rule.LoadInfo {
	// todo(#84)
	return []rule.LoadInfo{}
}

func (d dotnetLang) GenerateRules(args language.GenerateArgs) language.GenerateResult {
	// todo(#84)
	return language.GenerateResult{}
}

func (d dotnetLang) Fix(c *config.Config, f *rule.File) {
	// todo(#84)
}
