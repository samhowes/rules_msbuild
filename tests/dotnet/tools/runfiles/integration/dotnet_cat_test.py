import os

from rules_python.python.runfiles import runfiles

from tests.tools import mypytest
from tests.tools.executable import Executable


class TestLauncher(object):
    @classmethod
    def setup_class(cls):
        cls.r = runfiles.Create()
        cls.workspace_name = os.environ.get("TEST_WORKSPACE")

    def location(self, path):
        return self.r.Rlocation(self.workspace_name + "/" + path)

    def get_contents(self, rpath):
        fpath = self.location(rpath)
        with open(fpath) as f:
            contents = f.read()
        return contents

    def test_run_genrule_works(self):
        contents = self.get_contents(os.environ.get("RUN_RESULT"))
        assert "Hello Runfiles!\n" == contents

    def test_run_data_dep_works(self):
        startpath = os.getcwd()
        print(startpath)
        for root, dirs, files in os.walk(startpath):
            level = root.replace(startpath, '').count(os.sep)
            indent = ' ' * 4 * (level)
            print('{}{}/'.format(indent, os.path.basename(root)))
            subindent = ' ' * 4 * (level + 1)
            for f in files:
                print('{}{}'.format(subindent, f))
        env = self.r.EnvVars()
        executable = Executable(self.location(os.environ.get("TARGET_BINARY")))
        contents = self.assert_success(executable, env)
        assert "Hello Runfiles!\n" == contents

    def assert_success(self, executable, env, cwd=None):
        code, stdout, stderr = executable.run([], env=env, cwd=cwd)

        assert code == 0, stderr
        assert stderr == ""
        return stdout


if __name__ == '__main__':
    mypytest.main(__file__)
