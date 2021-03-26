import os
import sys

import pytest


def main(file):
    # print(pytest)
    print(file)
    xml_output = os.environ.get("XML_OUTPUT_FILE")
    exit_code = pytest.main([file, "-vv", f'--junitxml={xml_output}', '--tb=short'])
    sys.exit(exit_code)
