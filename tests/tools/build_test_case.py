from os import path, environ
from subprocess import run, CalledProcessError

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

    def decode(self, output):
        return str(output, 'utf-8')

    def get_output(self):
        executable = self.location(self.target)
        args = [executable] + self.args

        completed = None
        try:
            completed = run(args, capture_output=True)
        except CalledProcessError as error:
            completed = error

        return completed.returncode, self.decode(completed.stdout), self.decode(completed.stderr)
