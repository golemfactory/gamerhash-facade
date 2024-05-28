#!/bin/bash

set -e
source $(dirname "$0")/vars.sh

export $(grep -v '^#' ${YAGNA_DATADIR}/.env | xargs)

${GOLEM_BIN}/yagna payment fund --network holesky --driver erc20

cd ${SCRIPT_PATH}
poetry run python3 ai_runtime.py --network holesky --driver erc20
