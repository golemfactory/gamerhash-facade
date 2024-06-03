#!/bin/bash

get_abs_filename() {
  echo "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
}

export WORKSPACE=$(get_abs_filename $(dirname "$0")/../..)
export EXAMPLE_DIR=${WORKSPACE}/example/ai-requestor
export SCRIPT_PATH=${EXAMPLE_DIR}
export MODULES_DIR=$(get_abs_filename ${1:-${WORKSPACE}/modules})

export REQUESTOR_DIR=${MODULES_DIR}/Requestor
export REQUESTOR_YAGNA_DATADIR=${REQUESTOR_DIR}/modules/golem-data/yagna
export REQUESTOR_APP_DATADIR=${REQUESTOR_DIR}/modules/golem-data/app

export GOLEM_BIN=${MODULES_DIR}/golem
export REQEUSTOR_GOLEM_BIN=${REQUESTOR_DIR}/modules/golem

