#!/bin/bash
# Backs up Francesco's save files into save-backups/<date_time>/ (git-ignored; 21 Nov 2026, before mod changes that write to the
# game's save state, such as the Geis gift of 0.0.181). Copies the game's save folder (SMT3HDDATA.sav holds every save file and the
# suspend data; SMT3HDCONFIG the options) and the mod's per-save explored doors (UserData/NocturneAccess/Explored).
# Run with the game closed, so the files are not mid-write: bash tools/backup-saves.sh [label]
set -e
cd "$(dirname "$0")/.."
SAVES="$APPDATA/Sega/smt3hd"
[ -d "$SAVES" ] || SAVES="/c/Users/$USERNAME/AppData/Roaming/Sega/smt3hd"
EXPLORED="/e/SteamLibrary/steamapps/common/smt3hd/UserData/NocturneAccess/Explored"
DEST="save-backups/$(date +%Y-%m-%d_%H-%M)${1:+_$1}"
mkdir -p "$DEST"
cp -r "$SAVES" "$DEST/smt3hd"
[ -d "$EXPLORED" ] && cp -r "$EXPLORED" "$DEST/Explored"
echo "Backed up to $DEST:"; find "$DEST" -type f | sed 's/^/  /'
