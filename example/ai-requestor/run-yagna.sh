#!/bin/bash

set -e
source $(dirname "$0")/vars.sh

export YAGNA_METRICS_GROUP=Example-GamerHash

cd ${REQUESTOR_YAGNA_DATADIR}
${REQEUSTOR_GOLEM_BIN}/yagna service run 
