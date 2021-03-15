"""Xml Helpers"""

def element(name, value, attrs = {}):
    open_tag_items = [name]
    open_tag_items.extend(
        [
            '{}="{}"'.format(k, v)
            for k, v in attrs.items()
        ],
    )
    return "<{open_tag}>{value}</{name}>".format(
        name = name,
        open_tag = " ".join(open_tag_items),
        value = value,
    )

def inline_element(name, attrs):
    attr_strings = [
        "{}=\"{}\"".format(a, attrs[a])
        for a in attrs
    ]
    return "<{name} {attrs} />".format(name = name, attrs = " ".join(attr_strings))
