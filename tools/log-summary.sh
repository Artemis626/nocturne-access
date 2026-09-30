#!/usr/bin/env bash
# Quick overview of a MelonLoader session log for Nocturne Access.
# Usage: tools/log-summary.sh [log file]   (default: the game's MelonLoader/Latest.log)
# Game folder can be overridden with SMT3HD_DIR.

GAME="${SMT3HD_DIR:-E:/SteamLibrary/steamapps/common/smt3hd}"
LOG="${1:-$GAME/MelonLoader/Latest.log}"
ARCHIVE="$GAME/UserData/NocturneAccess/Logs"

if [ ! -f "$LOG" ]; then
  echo "Log not found: $LOG"
  exit 1
fi

echo "== Log: $LOG"
echo "   modified: $(date -r "$LOG" '+%Y-%m-%d %H:%M:%S')  lines: $(wc -l < "$LOG")"
grep -a -m1 -E 'MelonLoader v' "$LOG" | sed 's/^/   /'
grep -a -m1 -E 'Game Version' "$LOG" | sed 's/^/   /'
echo "   session: $(grep -a -o -m1 -E '\[[0-9:.]+\]' "$LOG") to $(tail -n1 "$LOG" | grep -a -o -E '^\[[0-9:.]+\]')"

echo
echo "== Mods"
grep -a -E 'Melon Assembly loaded|Mods? loaded\.' "$LOG" | sed 's/^/   /'

echo
echo "== Problems (errors, exceptions, warnings, failed or missing hooks)"
grep -a -n -i -E 'error|exception|warn|fail|NOT DECLARED' "$LOG" \
  | grep -a -v -E 'MelonStartScreen|UnhollowerMini|MelonUnityEngine|FileSystemWatcher|Preferences.IO.Watcher|ctor\(System.Int32, System.Int32\)|End of inner exception|^[0-9]+: +at ' \
  | cut -c1-300 | head -40 | sed 's/^/   /'

echo
echo "== Hook summary"
grep -a -E '\[Patch\] Summary|\[Probe\] ' "$LOG" | cut -c1-200 | sed 's/^/   /'

echo
echo "== Lines per tag"
for tag in 'SPEECH' 'Probe:TMP' 'Probe:Mes' 'UiDump' 'Scene' 'LogArchive'; do
  printf '   %-12s %s\n' "$tag" "$(grep -a -c "\[$tag\]" "$LOG")"
done

echo
echo "== Scenes"
grep -a '\[Scene\]' "$LOG" | cut -c1-160 | sed 's/^/   /'

echo
echo "== Last 10 spoken lines"
grep -a '\[SPEECH\]' "$LOG" | tail -n 10 | cut -c1-220 | sed 's/^/   /'

if [ -d "$ARCHIVE" ]; then
  echo
  echo "== Archived sessions (newest first)"
  ls -1t "$ARCHIVE" | head -5 | sed 's/^/   /'
fi
