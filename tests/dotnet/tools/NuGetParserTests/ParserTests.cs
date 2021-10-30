#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Moq;
using NuGetParser;
using Xunit;

namespace NuGetParserTests
{
    public class ParserTests
    {
        private List<FrameworkInfo> _frameworks;
        private readonly Mock<Files> _files;
        private Parser _parser;
        private readonly StringBuilder _log;
        private Mock<AssetsReader> _assetsReader;
        private List<List<PackageVersion>> _packages;
        private readonly NuGetContext _context;

        const string PackageName = "CommandLineParser";
        const string ImplicitDepName = "NETStandard.Library";
        const string ImplicitFrameworkName = "Microsoft.AspNetCore.App.Ref";

        public ParserTests()
        {
            _files = new Mock<Files>();
            // for auto-discovering the implicit framework reference
            _files.Setup(f => f.EnumerateFiles(It.IsAny<string>()))
                .Returns<string>(p => new List<string>() {"packageFile.txt"}.Select(s => Path.Combine(p, s)));

            _log = new StringBuilder();

            _assetsReader = new Mock<AssetsReader>();
            var packagesIndex = -1;
            _assetsReader.Setup(a => a.Init(It.IsAny<string>(), It.IsAny<string>()))
                .Callback(() => packagesIndex++);

            IEnumerable<PackageVersion> GetPackages()
            {
                var p = _packages![packagesIndex].ToDictionary(p => p.Id.Name);

                foreach (var dep in p.Values.SelectMany(s => s.Deps.Values.SelectMany(sv => sv)).ToList())
                {
                    p.GetOrAdd(dep.Name, () => new PackageVersion(dep));
                }

                return p.Values;
            }

            _assetsReader.Setup(a => a.GetPackages())
                .Returns(GetPackages);

            _context = new NuGetContext(new Dictionary<string, string>());
            _parser = new Parser(_context, _files.Object, _assetsReader.Object);
        }

        private void SetupMultipleVersions()
        {
            _frameworks = new List<FrameworkInfo>()
            {
                new("net5.0")
                {
                    RestoreGroups = new List<FrameworkRestoreGroup>()
                    {
                        new()
                        {
                            ObjDirectory = "first",
                            Packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                [PackageName] = "2.8.0"
                            }
                        },
                        new()
                        {
                            ObjDirectory = "second",
                            Packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                [PackageName] = "2.9.0-preview1"
                            }
                        }
                    }
                }
            };

            _packages = new List<List<PackageVersion>>()
            {
                new()
                {
                    new PackageVersion("CommandLineParser/2.8.0")
                    {
                        AllFiles = new List<string>()
                        {
                            "foo",
                            "bar"
                        },
                        Deps = new Dictionary<string, List<PackageId>>()
                        {
                            ["net5.0"] = new()
                            {
                                new PackageId("NETStandard.Library/1.0.0")
                            }
                        }
                    }
                },
                new()
                {
                    new PackageVersion("CommandLineParser/2.9.0-preview1")
                    {
                        AllFiles = new List<string>()
                        {
                            "foo",
                        },
                        Deps = new Dictionary<string, List<PackageId>>()
                        {
                            ["net5.0"] = new()
                            {
                                new PackageId("NETStandard.Library/2.0.0")
                            }
                        }
                    }
                }
            };
        }

        [Fact]
        public void LoadRequestedPackages_Works()
        {
            SetupMultipleVersions();
            _parser.Parse();
            var packages = _context.AllPackages;
            packages.Keys.Should().Equal(PackageName, ImplicitDepName);

            AssertPackages(packages, false);
        }

        private static void AssertPackages(Dictionary<string, Package> packages, bool shouldHaveImplicit)
        {
            packages.Count.Should().Be(shouldHaveImplicit ? 3 : 2);
            var package = packages[PackageName];
            package.RequestedName.Should().Be(PackageName);
            package.Versions.Keys.Should().Equal("2.8.0", "2.9.0-preview1");
        }

        [Fact]
        public void ProcessPackages_DoesntDuplicateCommonPackages()
        {
            SetupMultipleVersions();
            _parser.Parse();
            var packages = _context.AllPackages;

            var package = packages[PackageName];
            var preview = package.Versions["2.9.0-preview1"];
            preview.AllFiles.Count.Should().Be(1);

            var standard = package.Versions["2.8.0"];
            standard.AllFiles.Count.Should().Be(2);

            var imp = packages[ImplicitDepName];
            imp.Versions.Should().ContainKey("1.0.0");

            // var framework = packages[ImplicitFrameworkName];
            // framework.Versions.Keys.Should().Equal("3.1.10");
            // var version = framework.Versions.Values.Single();
            // version.AllFiles.Count.Should().Be(1);
            // version.Id.Should().Be("Microsoft.AspNetCore.App.Ref/3.1.10");
        }

        [Fact]
        public void ProcessPackages_RegistersDepsCorrectly()
        {
            SetupMultipleVersions();
            _parser.Parse();

            _context.Tfms.Values.Count.Should().Be(1);

            _context.AllPackages.Keys.Should().Equal(
                PackageName,
                ImplicitDepName);

            var commandLine = _context.AllPackages[PackageName];
            commandLine.Versions.Keys.Should().Equal("2.8.0", "2.9.0-preview1");

            var standard = commandLine.Versions["2.8.0"];
            standard.Deps["net5.0"].Select(d => d.String).Should().Equal("NETStandard.Library/1.0.0");

            var preview = commandLine.Versions["2.9.0-preview1"];
            preview.Deps["net5.0"].Select(d => d.String).Should().Equal("NETStandard.Library/2.0.0");
        }

        [Fact]
        public void VersionUpgrade_Works()
        {
            SetupVersionUpgrade();
            
            _parser.Parse();

            _context.AllPackages.Keys.Should().Equal(PackageName, ImplicitDepName);

            var imp = _context.AllPackages[ImplicitDepName];

            imp.Versions.Count.Should().Be(1);
            var version = imp.Versions.Values.Single();

            version.Id.Version.Should().Be("2.0.0");

            var package = _context.AllPackages[PackageName];
            var packageVersion = package.Versions.Values.Single();
            var dep = packageVersion.Deps.Values.Single().Single();
            
            // this package dep should be auto-upgraded to the actually downloaded version
            dep.Version.Should().Be(version.Id.Version);
        }

        [Fact]
        public void ImplicitDeps_GetFrameworkVersions()
        {
            SetupFrameworks();
            _packages = new List<List<PackageVersion>>() {new()
            {
                DefaultPackageVersion(false)
            }};
            _assetsReader.Setup(a => a.GetImplicitDependencies()).Returns(new List<PackageId>()
            {
                new PackageId(ImplicitFrameworkName, "3.1.0")
            });

            _parser.Parse();
            var packages = _context.AllPackages;

            packages.Keys.Should().Equal(PackageName, ImplicitFrameworkName);

            
            // both should have a TFM in their deps to explicitly say they don't have any deps for that tfm
            var package = packages[ImplicitFrameworkName];
            package.Versions.Keys.Should().Equal("3.1.0");
            var version = package.Versions.Values.Single();
            version.Deps.Keys.Should().Equal("net5.0");


            package = packages[PackageName];
            package.Versions.Keys.Should().Equal("2.8.0");
            version = package.Versions.Values.Single();
            version.Deps.Keys.Should().Equal("net5.0");

        }

        private void SetupVersionUpgrade()
        {
            SetupFrameworks();

            _packages = new List<List<PackageVersion>>()
            {
                new()
                {
                    DefaultPackageVersion(),
                    new PackageVersion("NETStandard.Library/2.0.0")
                    {
                        AllFiles = new List<string>()
                        {
                            "foo",
                        },
                        Deps = new Dictionary<string, List<PackageId>>()
                        {
                            ["net5.0"] = new()
                        }
                    }
                }
            };
        }

        private static PackageVersion DefaultPackageVersion(bool addDeps=true)
        {
            var tfm = "net5.0";
            var v = new PackageVersion("CommandLineParser/2.8.0")
            {
                AllFiles = new List<string>() {"foo"},
                Deps = new Dictionary<string, List<PackageId>>(){[tfm] = new()}
            };
            
            if (addDeps)
            {
                v.Deps[tfm] = new List<PackageId>()
                {
                    new("NETStandard.Library/1.0.0")
                };
            }

            return v;
        }

        private void SetupFrameworks()
        {
            _frameworks = new List<FrameworkInfo>()
            {
                new("net5.0")
                {
                    RestoreGroups = new List<FrameworkRestoreGroup>()
                    {
                        new()
                        {
                            ObjDirectory = "first",
                            Packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                [PackageName] = "2.8.0"
                            }
                        }
                    }
                }
            };
        }


        private void SetupMultipleFrameworks()
        {
            _frameworks = new List<FrameworkInfo>()
            {
                new("netcoreapp3.1")
                {
                    RestoreGroups = new List<FrameworkRestoreGroup>()
                    {
                        new()
                        {
                            ObjDirectory = "first",
                            Packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                [PackageName] = "2.8.0"
                            }
                        }
                    }
                },
                new("net5.0")
                {
                    RestoreGroups = new List<FrameworkRestoreGroup>()
                    {
                        new()
                        {
                            ObjDirectory = "second",
                            Packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                [PackageName] = "2.8.0"
                            }
                        }
                    }
                }
            };
            _files.Setup(f => f.GetContents(Path.Combine("first", "project.assets.json")))
                .Returns(AssetsFactory.Make("netcoreapp3.1", "2.8.0", "1.0.0"));
            _files.Setup(f => f.GetContents(Path.Combine("second", "project.assets.json")))
                .Returns(AssetsFactory.Make("net5.0", "2.8.0", null));
        }
    }

    public class AssetsFactory
    {
        private static readonly Regex Regex = new(@"@@(\w+)@@",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static string? _template;

        private static string Template()
        {
            if (_template != null) return _template;
            _template = File.ReadAllText("project.assets.template.json");
            return _template;
        }

        public static string Make(string targetFramework, string packageVersion, string? depVersion)
        {
            var variables = new Dictionary<string, string?>()
            {
                [nameof(targetFramework)] = targetFramework,
                [nameof(packageVersion)] = packageVersion,
                [nameof(depVersion)] = depVersion,
            };

            return Regex.Replace(Template(), (match) => variables[match.Groups[1].Value] ?? "");
        }
    }
}