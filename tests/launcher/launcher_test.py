import os
import shutil
import stat
from os import environ, path

from rules_python.python.runfiles import runfiles

from tests.tools import mypytest
from tests.tools.executable import Executable


def IsWindows():
    if os.name == "nt":
        return True
    return False


BINNAME = "Greeter" + ("exe" if IsWindows() else "")
TARGET_PATH = os.environ.get("TARGET_PATH")
BINPATH = f"my_rules_dotnet/{TARGET_PATH}/" + BINNAME
EXPECTED_OUTPUT = "Hello: LauncherTest!\n"


class TestLauncher(object):
    @classmethod
    def setup_class(cls):
        cls.workspace_name = environ.get("TEST_WORKSPACE")
        cls.output_base = path.dirname(environ.get('TEST_BINARY'))
        cls.r = runfiles.Create()
        cls.tmp_dir = os.environ.get("TEST_TMPDIR")
        cls.runfiles_dir = environ.get("TEST_SRCDIR")

    def copy_manifest(self, fake_runfiles_dir):
        copied_manifest = os.path.join(fake_runfiles_dir, "MANIFEST")
        src_manifest = os.path.join(self.runfiles_dir, "MANIFEST")
        shutil.copyfile(src_manifest, copied_manifest)

    def get_contents(self, rpath):
        fpath = self.r.Rlocation(rpath)
        with open(fpath) as f:
            contents = f.read()
        return contents

    def copy_bin(self, dest):
        src = self.r.Rlocation(BINPATH)
        os.makedirs(dest)
        fpath = os.path.join(dest, BINNAME)
        shutil.copyfile(src, fpath)
        os.chmod(fpath, stat.S_IRWXU)

    def test_run_genrule_works(self):
        txt_dir = os.path.dirname(TARGET_PATH)
        contents = self.get_contents(f"my_rules_dotnet/{txt_dir}/run_greeter.txt")
        assert "Hello: genrule!\n" == contents

    def test_run_data_dep_works(self):
        env = self.r.EnvVars()
        executable = Executable(self.r.Rlocation(BINPATH))
        self.assert_success(executable, env)

    def test_run_direct_top_level(self):
        """Simulate the user executing the binary directly"""
        exec_dir = os.path.join(self.tmp_dir, "run_direct")
        self.copy_bin(exec_dir)
        fake_runfiles_dir = os.path.join(exec_dir, BINNAME + ".runfiles")

        if IsWindows():
            os.makedirs(fake_runfiles_dir)
            self.copy_manifest(fake_runfiles_dir)
        else:
            os.symlink(self.runfiles_dir, fake_runfiles_dir)

        env = {}  # deliberately empty env
        os.chdir(exec_dir)
        executable = Executable("./" + BINNAME)
        self.assert_success(executable, env, exec_dir)

    def test_run_symlinked(self):
        """Simulate the user running from inside Greeter's own symlink tree"""
        if IsWindows():
            return

        test_dir = os.path.join(self.tmp_dir, "run_symlinked")
        os.makedirs(test_dir)
        fake_runfiles_dir = os.path.join(test_dir, BINNAME + ".runfiles")
        os.symlink(self.runfiles_dir, fake_runfiles_dir)

        exec_dir = os.path.dirname(os.path.join(fake_runfiles_dir, BINPATH))

        env = {}  # deliberately empty env
        os.chdir(exec_dir)
        executable = Executable("./" + BINNAME)
        self.assert_success(executable, env, exec_dir)

    def test_run_symlinked_other(self):
        """Simulate the user running from inside another binary's symlink tree"""
        if IsWindows():
            return

        exec_dir = os.path.dirname(self.r.Rlocation(BINPATH))
        env = {}  # deliberately empty env
        os.chdir(exec_dir)
        executable = Executable("./" + BINNAME)
        self.assert_success(executable, env, exec_dir)

    def test_bootstrap_env_vars(self):
        if "launcher" not in TARGET_PATH:  # lazy
            return
        executable = Executable(self.r.Rlocation(BINPATH))
        _, stdout, _ = executable.run(env={})

        env: dict[str, str] = {}
        for line in stdout.split("*~*"):
            if line == "":
                continue
            print(f'"{line}"')
            k, v = line.split('=', 1)
            env[k] = v

        assert "RUNFILES_DIR" in env.keys()
        assert "RUNFILES_MANIFEST_FILE" in env.keys()
        assert "DOTNET_MULTILEVEL_LOOKUP" in env.keys()

        assert env["RUNFILES_DIR"].endswith("launcher_test.runfiles")
        assert env["RUNFILES_MANIFEST_FILE"].endswith(os.path.join("launcher_test.runfiles", "MANIFEST"))

    def assert_success(self, executable, env, cwd=None):
        code, stdout, stderr = executable.run(["LauncherTest"], env=env, cwd=cwd)

        assert code == 0, stderr
        assert stderr == ""
        assert stdout == EXPECTED_OUTPUT


if __name__ == '__main__':
    mypytest.main(__file__)
