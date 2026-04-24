#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
GAME_PATH="${GameDir:-D:/SteamLibrary/steamapps/common/7 Days To Die}"
MOD_DEST="$GAME_PATH/Mods/7debug"

# Build first
bash "$SCRIPT_DIR/build.sh"

# Deploy
echo "Deploying to $MOD_DEST..."
rm -rf "$MOD_DEST"
mkdir -p "$MOD_DEST"

cp "$SCRIPT_DIR/ModInfo.xml" "$MOD_DEST/"
cp "$SCRIPT_DIR/7debug.dll" "$MOD_DEST/"

# Deploy Config (XML patches)
if [ -d "$SCRIPT_DIR/Config" ]; then
  cp -r "$SCRIPT_DIR/Config" "$MOD_DEST/"
fi

# Deploy worlds
if [ -d "$SCRIPT_DIR/Worlds" ]; then
  for world in "$SCRIPT_DIR/Worlds"/*/; do
    world_name=$(basename "$world")
    world_dest="$GAME_PATH/Data/Worlds/$world_name"
    echo "Deploying world $world_name to $world_dest..."
    rm -rf "$world_dest"
    mkdir -p "$world_dest"
    cp "$world"* "$world_dest/"
  done
fi

echo "Deployed! Debug server starts on port 7860 when game loads."
