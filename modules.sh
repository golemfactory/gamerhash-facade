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
cp $SCRIPT_DIR/../ya-runtime-ai/target/debug/{ya-runtime-ai$EXT,dummy$EXT} $SCRIPT_DIR/modules/plugins;
mkdir -p $SCRIPT_DIR/modules/golem-data;
