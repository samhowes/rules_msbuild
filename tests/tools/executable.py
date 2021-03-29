from subprocess import run, CalledProcessError


class Executable(object):
    def __init__(self, binpath: str):
        self.binpath = binpath
        self.args = [self.binpath]

    def decode(self, output):
        return str(output, 'utf-8')

    def run(self, args=None, env=None, **kwargs):
        args = self.args + (args if args is not None else [])

        completed = None
        try:
            completed = run(args, env=env, capture_output=True, **kwargs)
        except CalledProcessError as error:
            completed = error

        return completed.returncode, self.decode(completed.stdout), self.decode(completed.stderr)
