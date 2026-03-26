#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="$SCRIPT_DIR/src"
GAME_PATH="${GameDir:-D:/SteamLibrary/steamapps/common/7 Days To Die}"

if [ ! -d "$GAME_PATH/7DaysToDie_Data/Managed" ]; then
    echo "ERROR: 7 Days To Die not found at: $GAME_PATH"
    echo "Set GameDir environment variable"
    exit 1
fi

echo "Building 7debug..."
cd "$SRC_DIR"
dotnet build -c Release /p:GameDir="$GAME_PATH"
echo "Build successful!"
