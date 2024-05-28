#!/bin/bash

set -e
source $(dirname "$0")/vars.sh

export YAGNA_METRICS_GROUP=Example-GamerHash

cd ${YAGNA_DATADIR}
${GOLEM_BIN}/yagna service run 
