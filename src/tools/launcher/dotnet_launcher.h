
#ifndef DOTNET_SRC_TOOLS_LAUNCHER_DOTNET_LAUNCHER_H_
#define DOTNET_SRC_TOOLS_LAUNCHER_DOTNET_LAUNCHER_H_

#include "external/bazel_tools_public/src/tools/launcher/launcher.h"

using bazel::launcher::BinaryLauncherBase;
using bazel::launcher::LaunchDataParser;
using bazel::launcher::ExitCode;

namespace my_rules_dotnet {
    namespace launcher {

        class DotnetBinaryLauncher : public BinaryLauncherBase {
        public:
            DotnetBinaryLauncher(const LaunchDataParser::LaunchInfo &launch_info,
                                 int argc, wchar_t *argv[])
                    : BinaryLauncherBase(launch_info, argc, argv) {}

            ~DotnetBinaryLauncher() override = default;

            ExitCode Launch() override;
        };

    }  // namespace launcher
}  // namespace my_rules_dotnet

#endif  // DOTNET_SRC_TOOLS_LAUNCHER_DOTNET_LAUNCHER_H_
