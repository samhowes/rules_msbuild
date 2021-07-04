def inline_element(name, attrs):
    attr_strings = [
        "{}=\"{}\"".format(a, attrs[a])
        for a in attrs
    ]
    return "<{name} {attrs} />".format(name = name, attrs = " ".join(attr_strings))
