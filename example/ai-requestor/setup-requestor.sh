#!/bin/bash

set -e
source $(dirname "$0")/vars.sh

mkdir -p ${REQUESTOR_APP_DATADIR}
mkdir -p ${REQUESTOR_YAGNA_DATADIR}

dotnet run --project Golem.Package -- build-requestor --target ${REQUESTOR_DIR}

cp ${EXAMPLE_DIR}/.env ${REQUESTOR_YAGNA_DATADIR}/.env
