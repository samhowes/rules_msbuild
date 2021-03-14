echo "bazel query 'rdeps(attr(tags, \"dotnet_pre_restore\", //...), @nuget//...) except @nuget//...'  --output xml | bazel run @nuget//:bootstrapper"

