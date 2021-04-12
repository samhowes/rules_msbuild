import json
import os
from os import environ, path

import pytest

from tests.tools import mypytest
from tests.tools.build_test_case import BuildTestCase

EXPECTED_OUTPUT = environ.get("EXPECTED_OUTPUT")


class TestBuild(BuildTestCase):

    @pytest.mark.skipif(EXPECTED_OUTPUT is None or len(EXPECTED_OUTPUT) == 0,
                        reason="no output requested")
    def test_output(self):
        expected_output = EXPECTED_OUTPUT + os.linesep
        code, out, err = self.get_output()
        assert (code, out, err) == (0, expected_output, '')

    def test_files(self):
        expected: str = environ.get("EXPECTED_FILES")
        if expected is None or len(expected) == 0:
            return
        expected_dict = json.loads(expected)
        for dirname in expected_dict:
            prefix = None
            external = False
            if len(dirname) > 0 and dirname[0] == "@":
                prefix = [dirname[1:]]
                external = True
            else:
                prefix = [self.output_base]
                if len(dirname) > 0:
                    prefix.append(dirname)

            for f in expected_dict[dirname]:
                assert f is not None

                should_exist = True
                if f[0] == "!":
                    should_exist = False
                    f = f[1:]

                rpath = "/".join(prefix + [f])
                fpath = self.location(rpath, external)
                assert fpath is not None, f'Missing runfile item for {rpath}'
                message_base = f"\n name: {f}\n runfiles path: {rpath}\n file path: {fpath}"
                if should_exist:
                    assert path.exists(fpath), f"Missing expected file: '{f}'" + message_base
                else:
                    assert not path.exists(fpath), f"Expected not to find file: '{f}'" + message_base


if __name__ == "__main__":
    mypytest.main(__file__)
