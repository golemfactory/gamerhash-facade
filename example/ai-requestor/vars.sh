#!/bin/bash

get_abs_filename() {
  echo "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
}

export WORKSPACE=$(get_abs_filename ${1:-$(dirname "$0")/../..})
export MODULES_DIR=${WORKSPACE}/modules
export GOLEM_BIN=${MODULES_DIR}/golem
export REQUESTOR_YAGNA_DATADIR=${MODULES_DIR}/Requestor/modules/golem-data/yagna
export REQUESTOR_APP_DATADIR=${MODULES_DIR}/Requestor/modules/golem-data/app
export SCRIPT_PATH=${WORKSPACE}/example/ai-requestor
