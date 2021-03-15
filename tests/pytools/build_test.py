import unittest
from os import environ
from tests.pytools.build_test_case import BuildTestCase

class BuildTest(BuildTestCase):
    def setUp(self):
        self.setUpBase()

    def test_output(self):
        expected = environ.get("DOTNET_BUILD_EXPECTED_OUTPUT")
        if expected == None or len(expected) == 0:
            return
        
        self.assertOutput(expected)
    
    def test_files(self):
        expected = environ.get("DOTNET_BUILD_EXPECTED_FILES")
        if expected == None or len(expected) == 0:
            return
        expected_list = expected.split(';')
        self.assertFiles(expected_list)

if __name__ == '__main__':
    unittest.main()

