from rules_python.python.runfiles import runfiles
from subprocess import check_output
from sys import argv, exit, stderr
from os import path

r = runfiles.Create()

executable = r.Rlocation("my_rules_dotnet/" + argv[1])
print(executable)

actual = str(check_output([executable]), 'utf-8')

expected = 'Hello Bazel!\r\n'

if actual != expected:
    print(f'Expected:\n"{expected}"\nActual:\n"{actual}"', file=stderr)
    exit(1)
