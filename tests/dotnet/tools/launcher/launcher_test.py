import os
import shutil
from os import environ, path

from rules_python.python.runfiles import runfiles

from tests.tools import mypytest
from tests.tools.executable import Executable


def IsWindows():
    if os.name == "nt":
        return True
    return False


BINPATH = os.environ.get("TARGET_PATH")
BINNAME = os.path.basename(BINPATH)
EXPECTED_OUTPUT = "Hello: LauncherTest!" + os.linesep


class TestLauncher(object):
    @classmethod
    def setup_class(cls):
        cls.workspace_name = environ.get("TEST_WORKSPACE")
        cls.output_base = path.dirname(environ.get('TEST_BINARY'))
        cls.r = runfiles.Create()
        cls.tmp_dir = os.environ.get("TEST_TMPDIR")
        cls.runfiles_dir = environ.get("TEST_SRCDIR")

    def rlocation(self, rpath):
        print("Locating: ", rpath)
        fpath = self.r.Rlocation("/".join([self.workspace_name, rpath]))
        return fpath

    def copy_manifest(self, fake_runfiles_dir):
        copied_manifest = os.path.join(fake_runfiles_dir, "MANIFEST")
        src_manifest = os.path.join(self.runfiles_dir, "MANIFEST")
        shutil.copyfile(src_manifest, copied_manifest)

    def get_contents(self, rpath):
        fpath = self.rlocation(rpath)
        print("Reading: ", fpath)
        with open(fpath) as f:
            contents = f.read()
        return contents

    def test_run_genrule_works(self):
        contents = self.get_contents(os.environ.get("TXT_RESULT"))
        assert "Hello: genrule!\n" == contents

    def test_run_data_dep_works(self):
        env = self.r.EnvVars()
        executable = Executable(self.rlocation(BINPATH))
        out = self.assert_success(executable, env)
        assert out == EXPECTED_OUTPUT

    def run_direct(self, dirname: str, args=None):
        """Simulate the user executing the binary directly given that they have built it directly"""
        exec_dir = os.path.join(self.tmp_dir, dirname)
        fake_runfiles_dir = os.path.join(exec_dir, BINNAME + ".runfiles")

        if IsWindows():
            bin_path = self.rlocation(BINPATH)

            shutil.copytree(
                os.path.dirname(bin_path),
                exec_dir,
                ignore=lambda _, __: [os.path.basename(bin_path) + ".runfiles"])

            os.makedirs(fake_runfiles_dir)
            self.copy_manifest(fake_runfiles_dir)
            print(exec_dir)
            print(os.listdir(exec_dir))
        else:
            os.symlink(self.runfiles_dir, fake_runfiles_dir)

        env = None  # deliberately empty env
        os.chdir(exec_dir)
        print(os.getcwd())
        executable = Executable(os.path.join("", BINNAME))
        out = self.assert_success(executable, env, exec_dir, args=args)
        return out

    def test_run_direct_top_level(self):
        out = self.run_direct("top_level")
        assert out == EXPECTED_OUTPUT

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
        executable = Executable(os.path.join("", BINNAME))
        out = self.assert_success(executable, env, exec_dir)
        assert out == EXPECTED_OUTPUT

    def test_run_symlinked_other(self):
        """Simulate the user running from inside another binary's symlink tree"""
        if IsWindows():
            return

        exec_dir = os.path.dirname(self.rlocation(BINPATH))
        env = {}  # deliberately empty env
        os.chdir(exec_dir)
        executable = Executable(os.path.join("", BINNAME))
        out = self.assert_success(executable, env, exec_dir)
        assert out == EXPECTED_OUTPUT

    def test_bootstrap_env_vars(self):
        stdout = self.run_direct("bootstrap_env", args=[])

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

        assert env["RUNFILES_DIR"].endswith(BINNAME + ".runfiles")

        # the launcher apparently always uses forward slashes for this one
        assert env["RUNFILES_MANIFEST_FILE"].endswith("/".join([BINNAME + ".runfiles", "MANIFEST"]))

    def assert_success(self, executable, env, cwd=None, args=None):
        args = ["LauncherTest"] if args is None else args

        code, stdout, stderr = executable.run(args, env=env, cwd=cwd)

        assert code == 0, stderr + "\n" + stdout
        assert stderr == ""
        return stdout


if __name__ == '__main__':
    mypytest.main(__file__)
