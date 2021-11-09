using System;
using System.IO;

namespace RulesMSBuild.Tools.Builder.Launcher
{
    /// <summary>
    /// https://docs.google.com/document/d/1z6Xv95CJYNYNYylcRklA6xBeesNLc54dqXfri0z0e14/edit#heading=h.ehp217t4xp3w
    /// </summary>
    public class LauncherFactory
    {
        public int Create(string[] args)
        {
            using var writer = CreateWriter(args[0], args[1]);

            writer.Add("binary_type", "Dotnet");
            for (int i = 2; i + 1 < args.Length; i += 2)
            {
                writer.Add(args[i], args[i + 1]);
            }

            writer.Save();
            return 0;
        }

        public int CreatePublish(string launcherTemplate, string outputPath, BuildContext context)
        {
            using var writer = CreateWriter(launcherTemplate, outputPath + ".exe");
            writer.Add("assembly_name", context.Command.assembly_name);
            writer.Add("binary_type", "DotnetPublish");
            writer.Save();

            using var script = new StreamWriter(File.Create(outputPath));
            script.WriteLine(@"#!/bin/bash
dotnet_path=''
if [[ ! -z '$DOTNET_CLI_HOME' ]]; then 
    dotnet_path='$DOTNET_CLI_HOME/dotnet'
else
    dotnet_path='$(which dotnet)'
    if [[ '$?' != '0' ]]; then
        echo 'Could not find dotnet on PATH. Set the environment variable DOTNET_CLI_HOME or install a dotnet runtime. https://dotnet.microsoft.com/download'
        exit 1 
    fi;
fi;

this='$0'

$dotnet_path exec '$this.dll' '${@:1}'
".Replace('\'', '"'));

            return 0;
        }

        private static LaunchDataWriter CreateWriter(string launcherTemplatePath, string outputPath)
        {
            var launcherTemplate = new FileInfo(launcherTemplatePath);
            if (!launcherTemplate.Exists)
                throw new Exception($"Launcher template does not exist at '{launcherTemplate.FullName}'");

            var output = new FileInfo(outputPath).OpenWrite();
            using (var input = launcherTemplate.OpenRead())
            {
                input.CopyTo(output);
            }

            var writer = new LaunchDataWriter(output);
            return writer;
        }
    }
}