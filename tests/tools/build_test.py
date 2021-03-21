import json
import os
from os import environ
from os import path

import pytest
import sys

from tests.tools.build_test_case import BuildTestCase


class TestBuild(BuildTestCase):
    def test_output(self):
        expected = environ.get("EXPECTED_OUTPUT")
        if expected is None or len(expected) == 0:
            return

        out, err = self.get_output()
        assert err is None
        assert out == expected

    def test_files(self):
        expected: str = environ.get("EXPECTED_FILES")
        if expected is None or len(expected) == 0:
            return
        expected_dict = json.loads(expected)
        for dirname in expected_dict:
            for f in expected_dict[dirname]:
                assert f is not None
                rpath = "/".join([self.output_base, dirname, f])
                fpath = self.location(rpath)
                assert fpath is not None, f'Missing runfile item for {rpath}'
                assert path.exists(fpath), (
                    f"Missing file: '{f}'\n name: {f}\n runfiles path: {rpath}\n file path: {fpath}")


if __name__ == "__main__":
    xml_output = os.environ.get("XML_OUTPUT_FILE")
    exit_code = pytest.main([__file__, "-vv", f'--junitxml={xml_output}'])
    sys.exit(exit_code)
