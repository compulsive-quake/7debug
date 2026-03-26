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

echo "Deployed! Debug server starts on port 7860 when game loads."
