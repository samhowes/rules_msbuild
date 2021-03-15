import unittest
from rules_python.python.runfiles import runfiles
from subprocess import check_output
from sys import argv, exit, stderr
from os import path, environ

class BuildTestCase(unittest.TestCase):
    
    def setUpBase(self):
        self.target = environ.get("DOTNET_BUILD_TARGET")
        self.r = runfiles.Create()
        self.workspace_name = "my_rules_dotnet"

    def location(self, short_path):
        return self.r.Rlocation(self.workspace_name + "/" + self.target)

    def assertOutput(self, expected):
        executable = self.location(self.target)

        actual_raw = check_output([executable])

        actual = str(actual_raw, 'utf-8')
        expected += "\r\n"
        if actual != expected:
            print(f'Expected:\n"{expected}"\nActual:\n"{actual}"', file=stderr)
            exit(1)

    def assertFiles(self, file_list):
        output_dir = path.dirname(self.location(self.target))

        for f in file_list:
            fpath = path.join(output_dir, f)
            self.assertTrue(path.exists(fpath), msg=f"Missing file {fpath}")

