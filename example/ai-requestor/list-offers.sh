#!/bin/bash

set -e
source $(dirname "$0")/vars.sh

export $(grep -v '^#' ${REQUESTOR_YAGNA_DATADIR}/.env | xargs)

cd ${SCRIPT_PATH}
poetry run python3 list-offers.py --network holesky --driver erc20 --subnet-tag public
