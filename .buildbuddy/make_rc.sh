#!/bin/bash

cat >buildbuddy.bazelrc <<EOF
build --bes_results_url=https://app.buildbuddy.io/invocation/
build --bes_backend=grpcs://cloud.buildbuddy.io
build --remote_cache=grpcs://cloud.buildbuddy.io
build --noremote_upload_local_results # Uploads logs & artifacts without writing to cache
build --remote_timeout=3600
build --remote_header=x-buildbuddy-api-key=$(BUILDBUDDY_API_KEY)
build --build_metadata=ROLE=CI
build --workspace_status_command=$(pwd)/.buildbuddy/workspace_status.sh
EOF
