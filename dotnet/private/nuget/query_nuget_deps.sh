echo "bazel query 'rdeps(//..., @nuget//...) except @nuget//...'  --output xml > nuget_targets.xml; bazel run @my_rules_dotnet//:bootstrap_nuget"

