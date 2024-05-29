#!/bin/bash

set -e
source $(dirname "$0")/vars.sh

export $(grep -v '^#' ${REQUESTOR_YAGNA_DATADIR}/.env | xargs)

${REQEUSTOR_GOLEM_BIN}/yagna payment fund --network holesky --driver erc20

cd ${SCRIPT_PATH}
poetry run python3 ai_runtime.py --network holesky --driver erc20 --log-file ${REQUESTOR_APP_DATADIR}/ai-requestor.log
