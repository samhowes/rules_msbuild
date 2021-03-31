#include "src/tools/launcher/dotnet_launcher.h"

#include <string>
#include <vector>

#include "external/bazel_tools_public/src/tools/launcher/util/launcher_util.h"

using bazel::launcher::GetBinaryPathWithoutExtension;
using bazel::launcher::DoesFilePathExist;
using bazel::launcher::GetWindowsLongPath;

namespace my_rules_dotnet {
    namespace launcher {

        using std::vector;
        using std::wstring;

        static constexpr const char *DOTNET_BIN_PATH = "dotnet_bin_path";

        ExitCode DotnetBinaryLauncher::Launch() {
            wstring dotnet_binary = this->GetLaunchInfoByKey(DOTNET_BIN_PATH);

            // There are three kinds of values for `dotnet_binary`:
            // todo(#31): decide these for dotnet
            // 1. An absolute path to a system interpreter. This is the case if
            // `--python_path` is set by the
            //    user, or if a `py_runtime` is used that has `interpreter_path` set.
            // 2. A runfile path to an in-workspace interpreter. This is the case if a
            // `py_runtime` is used that has `interpreter` set.
            // 3. The special constant, "python". This is the default case if neither of
            // the above apply. Rlocation resolves runfiles paths to absolute paths, and
            // if given an absolute path it leaves it alone, so it's suitable for cases 1
            // and 2.
            if (GetBinaryPathWithoutExtension(dotnet_binary) != L"dotnet") {
                // Rlocation returns the original path if dotnet_binary is an absolute path.
                dotnet_binary = this->Rlocation(dotnet_binary, true);
            }

            // If specified dotnet binary path doesn't exist, then fall back to
            // dotnet.exe and hope it's in PATH.
            if (!DoesFilePathExist(dotnet_binary.c_str())) {
                dotnet_binary = L"dotnet.exe";
            }

            vector <wstring> args = this->GetCommandlineArguments();
            wstring user_assembly;
            // In case the given binary path is a shortened Windows 8dot3 path, we need to
            // convert it back to its long path form before using it to find the user's assembly
            // file.
            // todo(#31) figure out if this is needed for dotnet
            wstring full_assembly_path = GetWindowsLongPath(args[0]);
            user_assembly = GetBinaryPathWithoutExtension(full_assembly_path);


            // Replace the first argument with python file path
            args[0] = user_assembly;

            for (int i = 1; i < args.size(); i++) {
                args[i] = this->EscapeArg(args[i]);
            }

            return this->LaunchProcess(dotnet_binary, args);
        }

    }  // namespace launcher
}  // namespace my_rules_dotnet
