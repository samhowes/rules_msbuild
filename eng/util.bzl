def expand(packages):
    args = []
    for p in packages:
        args.append(p)
        args.append("$(rootpath %s)" % p)
    return args
