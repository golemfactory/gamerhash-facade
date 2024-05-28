#!/bin/bash

set -ex
source $(dirname "$0")/vars.sh

cd ${YAGNA_DATADIR}
${GOLEM_BIN}/yagna service run 
