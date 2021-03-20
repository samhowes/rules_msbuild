import unittest
from os import environ, path
from tests.tools.build_test_case import BuildTestCase
import json


class BuildTest(BuildTestCase):
    def setUp(self):
        self.setUpBase()

    def test_output(self):
        expected = environ.get("EXPECTED_OUTPUT")
        if expected == None or len(expected) == 0:
            return
        
        self.assertOutput(expected)
    
    def test_files(self):
        expected = environ.get("EXPECTED_FILES")
        if expected == None or len(expected) == 0:
            return
        expected_dict = json.loads(expected)
        for dirname in expected_dict:
            self.assertFiles(dirname, expected_dict[dirname])

if __name__ == '__main__':
    unittest.main()

