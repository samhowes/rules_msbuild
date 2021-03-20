import unittest
from rules_python.python.runfiles import runfiles
from subprocess import check_output
from sys import argv, exit, stderr
from os import path, environ, linesep

class BuildTestCase:
    
    @classmethod
    def setup_class(cls):
        cls.target = environ.get("TARGET_EXECUTABLE")
        cls.args = environ.get("TARGET_EXECUTABLE_ARGS")
        if cls.args != None:
            cls.args = cls.args.split(";")

        cls.r = runfiles.Create()
        cls.workspace_name = environ.get("TEST_WORKSPACE")
        cls.output_base = path.dirname(environ.get('TEST_BINARY'))

    def location(self, short_path):
        return self.r.Rlocation("/".join([self.workspace_name, short_path]))

    def assertOutput(self, expected):
        executable = self.location(self.target)
        args = [executable] + self.args
        
        actual_raw = check_output(args)

        actual = str(actual_raw, 'utf-8')
        expected += linesep
        assert expected == actual
        # self.assertEqual(expected, actual, msg="Incorrect stdout")

    def assertFiles(self, dirname, file_list):
        for f in file_list:
            print(f)
            assert f != None
            rpath = path.join(self.output_base, dirname, f)
            assert rpath != None
            print(rpath)
            fpath = self.location(rpath)
            print(fpath)
            assert fpath != None
            assert path.exists(fpath) #, msg=f"Missing file: name={f}\n rpath={rpath}\n fpath={fpath}")

