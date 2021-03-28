#!/usr/bin/env bash

exec > >(trap "" INT TERM; sed "s/^/$PREFIX: /")
exec 2> >(trap "" INT TERM; sed "s/^/$PREFIX: (stderr) /" >&2)

%dotnet_bin% "$@"
