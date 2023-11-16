#!/bin/bash
## Run
## ./MockGUI/modules.sh .exe
## to copy .exe binaries. Skip ".exe" for Linux.
EXT=$1
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
rm -rf $SCRIPT_DIR/modules/golem $SCRIPT_DIR/modules/plugins;

mkdir -p $SCRIPT_DIR/modules/golem;
cp $SCRIPT_DIR/../yagna/target/debug/{yagna$EXT,ya-provider$EXT} $SCRIPT_DIR/modules/golem/;

mkdir -p $SCRIPT_DIR/modules/plugins;

AI_RUNTIME_NAME=ya-runtime-ai
export AI_RUNTIME_FILE=$AI_RUNTIME_NAME$EXT;
cp $SCRIPT_DIR/../ya-runtime-ai/target/debug/{$AI_RUNTIME_FILE,dummy$EXT} $SCRIPT_DIR/modules/plugins;
cp $SCRIPT_DIR/../ya-runtime-ai/conf/$AI_RUNTIME_NAME.json $SCRIPT_DIR/modules/plugins;

tmp=$(mktemp)
jq '(.[] | select(.name == "ai-dummy") )."supervisor-path" |= env.AI_RUNTIME_FILE' modules/plugins/ya-runtime-ai.json > "$tmp" && mv "$tmp" $SCRIPT_DIR/modules/plugins/ya-runtime-ai.json

mkdir -p $SCRIPT_DIR/modules/golem-data;