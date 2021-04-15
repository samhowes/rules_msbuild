import os

from rules_python.python.runfiles import runfiles

from tests.tools.executable import Executable


class BuildTestCase:

    @classmethod
    def setup_class(cls):
        cls.target = os.environ.get("TARGET_EXECUTABLE")
        cls.args = os.environ.get("TARGET_EXECUTABLE_ARGS")
        if cls.args is not None and len(cls.args) == 0:
            cls.args = None
        if cls.args is not None:
            cls.args = cls.args.split(";")

        cls.r = runfiles.Create()
        cls.workspace_name = os.environ.get("TEST_WORKSPACE")
        cls.output_base = os.path.dirname(os.environ.get('TEST_BINARY'))

    def location(self, short_path, external=False):
        rpath = None
        if not external:
            rpath = "/".join([self.workspace_name, short_path])
        else:
            rpath = short_path

        return self.r.Rlocation(rpath)

    def get_output(self):
        executable = Executable(self.location(self.target))
        return executable.run(self.args, self.r.EnvVars())
