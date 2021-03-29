import json
import os
from os import environ, path

from tests.tools import mypytest
from tests.tools.build_test_case import BuildTestCase


class TestBuild(BuildTestCase):
    def test_output(self):
        expected = environ.get("EXPECTED_OUTPUT")
        if expected is None or len(expected) == 0:
            return
        expected += os.linesep
        code, out, err = self.get_output()
        assert (code, out, err) == (0, expected, '')

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

                rpath = "/".join(prefix + [f])
                fpath = self.location(rpath, external)
                assert fpath is not None, f'Missing runfile item for {rpath}'
                assert path.exists(fpath), (
                    f"Missing file: '{f}'\n name: {f}\n runfiles path: {rpath}\n file path: {fpath}")


if __name__ == "__main__":
    mypytest.main(__file__)
