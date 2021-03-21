from os import path, environ
from subprocess import check_output, CalledProcessError

from rules_python.python.runfiles import runfiles


class BuildTestCase:

    @classmethod
    def setup_class(cls):
        cls.target = environ.get("TARGET_EXECUTABLE")
        cls.args = environ.get("TARGET_EXECUTABLE_ARGS")
        if cls.args is not None:
            cls.args = cls.args.split(";")

        cls.r = runfiles.Create()
        cls.workspace_name = environ.get("TEST_WORKSPACE")
        cls.output_base = path.dirname(environ.get('TEST_BINARY'))

    def location(self, short_path):
        return self.r.Rlocation("/".join([self.workspace_name, short_path]))

    def get_output(self):
        executable = self.location(self.target)
        args = [executable] + self.args

        out_raw = None
        err = None
        try:
            out_raw = check_output(args)
        except CalledProcessError as error:
            out_raw = error.stdout
            err = error.stderr

        out = None
        if out_raw is not None:
            out = str(out_raw, 'utf-8')
        return out, err
