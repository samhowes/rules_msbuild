from sys import argv
from tests.pytools.executable_fixture import Fixture

f = Fixture(argv[1])

f.expect("Hello Bazel!")
