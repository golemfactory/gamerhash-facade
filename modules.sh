#!/bin/bash
## Run
## ./MockGUI/modules.sh .exe
## to copy .exe binaries. Skip ".exe" for Linux.
EXT=$1
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
rm -rf $SCRIPT_DIR/modules/golem $SCRIPT_DIR/modules/plugins;

mkdir -p $SCRIPT_DIR/modules/golem;
cp $SCRIPT_DIR/../yagna/target/debug/ya-provider$EXT $SCRIPT_DIR/modules/golem/;
cp $SCRIPT_DIR/../yagna/target/debug/yagna$EXT $SCRIPT_DIR/modules/golem/;

mkdir -p $SCRIPT_DIR/modules/plugins;

AI_RUNTIME_NAME=ya-runtime-ai
export AI_RUNTIME_FILE=$AI_RUNTIME_NAME$EXT;
cp $SCRIPT_DIR/../ya-runtime-ai/target/debug/$AI_RUNTIME_FILE $SCRIPT_DIR/modules/plugins;
cp $SCRIPT_DIR/../ya-runtime-ai/target/debug/dummy$EXT $SCRIPT_DIR/modules/plugins;
cp $SCRIPT_DIR/../ya-runtime-ai/conf/ya-dummy-ai.json $SCRIPT_DIR/modules/plugins;

tmp=$(mktemp)
jq '(.[] | select(.name == "ai") )."supervisor-path" |= env.AI_RUNTIME_FILE' modules/plugins/ya-dummy-ai.json > "$tmp" && mv "$tmp" $SCRIPT_DIR/modules/plugins/ya-dummy-ai.json

mkdir -p $SCRIPT_DIR/modules/golem-data;
