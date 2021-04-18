import os

from rules_python.python.runfiles import runfiles

from tests.tools.executable import Executable


class BuildTestCase:

    @classmethod
    def setup_class(cls):
        cls.target = os.environ.get("TARGET_ASSEMBLY")
        cls.target_dir = os.path.dirname(cls.target)
        cls.args = os.environ.get("TARGET_ASSEMBLY_ARGS")
        if cls.args is not None and len(cls.args) == 0:
            cls.args = None
        if cls.args is not None:
            cls.args = cls.args.split(";")

        print(cls.target_dir)
        cls.r = runfiles.Create()
        cls.workspace_name = os.environ.get("TEST_WORKSPACE")
        cls.output_base = os.path.dirname(os.environ.get('TEST_BINARY'))

    def location(self, short_path, external=False):
        print(f"locating {short_path}")
        append = False
        rpath = short_path
        if short_path.startswith(self.target_dir):
            # my_rules_dotnet only declares the assembly and the directory of the assembly as outputs therefore if we
            # are running with MANIFEST_FILE_ONLY (i.e. windows) then we wont have an explicit listing for the
            # individual files in the directory. For these, we'll get the directory path, then append the path of the
            # desired file
            append = True
            rpath = self.target_dir
            short_path = short_path[len(self.target_dir)+1:]

        if not external:
            rpath = "/".join([self.workspace_name, rpath])
        else:
            rpath = rpath

        print(f"computed to {rpath}")
        fpath = self.r.Rlocation(rpath)
        print(f"found {fpath}")
        if append:
            fpath = os.path.join(fpath, short_path)
            print(f"joined {fpath}")
        return fpath

    def get_output(self):
        executable = Executable(self.location(self.target))
        return executable.run(self.args, self.r.EnvVars())
