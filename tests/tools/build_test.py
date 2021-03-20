import pytest
from os import environ, path
import os
from tests.tools.build_test_case import BuildTestCase
import json
import sys


class TestBuild(BuildTestCase):
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

if __name__ == "__main__":
    log_dir = os.environ.get("TEST_UNDECLARED_OUTPUTS_DIR")
    file_log_path = os.path.join(log_dir, "pytest.xml")
    
    sys.exit(pytest.main([__file__, f'--junitxml={file_log_path}']))
