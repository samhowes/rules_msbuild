import os
from subprocess import run, CalledProcessError


class Executable(object):
    def __init__(self, binpath: str):
        self.binpath = binpath.replace('/', os.path.sep)

    def decode(self, output):
        return str(output, 'utf-8')

    def run(self, args, env, **kwargs):
        args = [self.binpath] + (args if args is not None else [])

        if env is None:
            env = {'foo': 'bar'}  # env has to be non-empty otherwise Windows error 87 will be thrown by python

        completed = None
        try:
            print('Args: ', args)
            print('Env: ', env)
            completed = run(args, executable=self.binpath, env=env, capture_output=True, shell=False, **kwargs)
        except CalledProcessError as error:
            completed = error

        out = self.decode(completed.stdout)
        err = self.decode(completed.stderr)
        print('Stdout: ', out)
        print('Stderr: ', err)
        return completed.returncode, out, err
