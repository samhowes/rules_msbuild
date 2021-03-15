import unittest
from tests.pytools.build_test_case import BuildTestCase

class TestHelloBazel(BuildTestCase):
    def setUp(self):
        self.setUpBase()

    def test_output(self):
        self.assertOutput("Hello Secret: Foo")
    
    def test_files(self):
        self.assertFiles([
            "Dependent.pdb",
            "ClassLibrary.dll",
            "ClassLibrary.pdb",
        ])

if __name__ == '__main__':
    unittest.main()

