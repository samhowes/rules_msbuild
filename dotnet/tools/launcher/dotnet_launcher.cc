#include "dotnet/tools/launcher/dotnet_launcher.h"

#include <string>
#include <sstream>
#include <vector>

#include "external/bazel_tools_public/src/tools/launcher/util/launcher_util.h"

using bazel::launcher::GetBinaryPathWithoutExtension;
using bazel::launcher::DoesFilePathExist;
using bazel::launcher::GetWindowsLongPath;
using bazel::launcher::SetEnv;
using bazel::launcher::GetEnv;

namespace my_rules_dotnet {
namespace launcher {

using std::vector;
using std::wstring;
using std::wstring;
using std::wstringstream;

static constexpr const char *DOTNET_BIN_PATH = "dotnet_bin_path";
static constexpr const char *DOTNET_ENV = "dotnet_env";

ExitCode DotnetBinaryLauncher::Launch() {
    wstring dotnet_binary = this->GetLaunchInfoByKey(DOTNET_BIN_PATH);

    // LauncherBase doesn't set RUNFILES_DIR if we're not symlinked.
    wstring runfiles_dir;
    if (!GetEnv(L"RUNFILES_DIR", &runfiles_dir)) {
        runfiles_dir = this->GetRunfilesPath();
    }
    SetEnv(L"RUNFILES_DIR", runfiles_dir);

    // Allow three kinds of values for `dotnet_binary`:
    // 1. An absolute path to a system binary.
    // 2. A runfile path to an in-workspace binary. Currently, this is the only 
    //    impelmented value.
    // 3. The special constant, "dotnet". This is the default case if neither of
    //    the above apply. Rlocation resolves runfiles paths to absolute paths, and
    //    if given an absolute path it leaves it alone, so it's suitable for cases 1
    //    and 2.
    if (GetBinaryPathWithoutExtension(dotnet_binary) != L"dotnet") {
        // Rlocation returns the original path if dotnet_binary is an absolute path.
        dotnet_binary = this->Rlocation(dotnet_binary, true);
    }

    // If specified dotnet binary path doesn't exist, then fall back to
    // dotnet.exe and hope it's in PATH.
    if (!DoesFilePathExist(dotnet_binary.c_str())) {
        dotnet_binary = L"dotnet.exe";
    }

    wstring env_var;
    wstringstream classpath_ss(this->GetLaunchInfoByKey(DOTNET_ENV));
    while (getline(classpath_ss, env_var, L';')) {
        int equals = env_var.find_first_of(L'=');
        SetEnv(env_var.substr(0, equals), env_var.substr(equals + 1));
    }

    vector <wstring> args = this->GetCommandlineArguments();
    wstring user_assembly;
    // In case the given binary path is a shortened Windows 8dot3 path, we need to
    // convert it back to its long path form before using it to find the user's assembly
    // file.
    wstring full_assembly_path = GetWindowsLongPath(args[0]);
    // assume the user's assembly is named the same as args[0] and in the same directory
    user_assembly = GetBinaryPathWithoutExtension(full_assembly_path) + L".dll";

    // Replace the first argument with the user's assembly
    args[0] = user_assembly;

    for (int i = 1; i < args.size(); i++) {
        args[i] = this->EscapeArg(args[i]);
    }

    // todo(#12) prepend args with "exec" or "test"
    return this->LaunchProcess(dotnet_binary, args);
}

}  // namespace launcher
}  // namespace my_rules_dotnet
