import unittest
from rules_python.python.runfiles import runfiles
from subprocess import check_output
from sys import argv, exit, stderr
from os import path, environ, linesep

class BuildTestCase(unittest.TestCase):
    
    def setUpBase(self):
        self.target = environ.get("TARGET_EXECUTABLE")
        self.args = environ.get("TARGET_EXECUTABLE_ARGS")
        if self.args != None:
            self.args = self.args.split(";")

        self.r = runfiles.Create()
        self.workspace_name = environ.get("TEST_WORKSPACE")
        self.output_base = path.dirname(environ.get('TEST_BINARY'))

    def location(self, short_path):
        return self.r.Rlocation("/".join([self.workspace_name, short_path]))

    def assertOutput(self, expected):
        executable = self.location(self.target)
        args = [executable] + self.args
        
        actual_raw = check_output(args)

        actual = str(actual_raw, 'utf-8')
        expected += linesep
        self.assertEqual(expected, actual, msg="Incorrect stdout")

    def assertFiles(self, dirname, file_list):
        for f in file_list:
            rpath = path.join(self.output_base, dirname, f)
            fpath = self.location(rpath)
            self.assertTrue(path.exists(fpath), msg=f"Missing file: name={f}\n rpath={rpath}\n fpath={fpath}")

