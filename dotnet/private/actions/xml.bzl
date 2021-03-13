"""Xml Helpers"""

def element(name, value, attrs = {}):
    open_tag_items = [name]
    open_tag_items.extend(
        [
            '{}="{}"'.format(k, v)
            for k, v in attrs.items()
        ],
    )
    return "    <{open_tag}>{value}</{name}>\n".format(
        name = name,
        open_tag = " ".join(open_tag_items),
        value = value,
    )

def inline_element(name, attr_name, value):
    return '    <{name} {attr_name}="{value}" />\n'.format(name = name, attr_name = attr_name, value = value)
