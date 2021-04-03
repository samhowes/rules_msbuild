#include <memory>

#include "dotnet/tools/launcher/dotnet_launcher.h"
#include "external/bazel_tools_public/src/tools/launcher/launcher.h"
#include "external/bazel_tools_public/src/tools/launcher/util/data_parser.h"
#include "external/bazel_tools_public/src/tools/launcher/util/launcher_util.h"

static constexpr const char *BINARY_TYPE = "binary_type";

using my_rules_dotnet::launcher::DotnetBinaryLauncher;
using bazel::launcher::BinaryLauncherBase;
using bazel::launcher::GetBinaryPathWithExtension;
using bazel::launcher::LaunchDataParser;
using bazel::launcher::die;
using std::make_unique;
using std::unique_ptr;

int wmain(int argc, wchar_t *argv[]) {
    LaunchDataParser::LaunchInfo launch_info;

    if (!LaunchDataParser::GetLaunchInfo(GetBinaryPathWithExtension(argv[0]),
                                         &launch_info)) {
        die(L"Failed to parse launch info.");
    }

    auto result = launch_info.find(BINARY_TYPE);
    if (result == launch_info.end()) {
        die(L"Cannot find key \"%hs\" from launch data.", BINARY_TYPE);
    }

    unique_ptr <BinaryLauncherBase> binary_launcher;

    if (result->second == L"Dotnet") {
        binary_launcher =
                make_unique<DotnetBinaryLauncher>(launch_info, argc, argv);
    } else {
        die(L"Unknown binary type, cannot launch anything.");
    }

    return binary_launcher->Launch();
}
