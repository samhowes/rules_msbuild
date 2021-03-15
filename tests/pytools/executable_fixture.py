from rules_python.python.runfiles import runfiles
from subprocess import check_output
from sys import argv, exit, stderr
from os import path

class Fixture:
    r = None
    def __init__(self, target):
        self.target = target

    def expect(self, expected):
        if not self.r:
            self.r = runfiles.Create()

        executable = self.r.Rlocation("my_rules_dotnet/" + self.target)

        actual_raw = check_output([executable])

        actual = str(actual_raw, 'utf-8')
        expected += "\r\n"
        if actual != expected:
            print(f'Expected:\n"{expected}"\nActual:\n"{actual}"', file=stderr)
            exit(1)
