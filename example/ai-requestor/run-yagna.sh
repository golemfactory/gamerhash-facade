#!/bin/bash

set -e
source $(dirname "$0")/vars.sh

cd ${YAGNA_DATADIR}
${GOLEM_BIN}/yagna service run 
