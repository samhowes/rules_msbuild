def expand(packages):
    args = []
    for p in packages:
        args.append(p)
        args.append("$(location %s)" % p)
    return args
