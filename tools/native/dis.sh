#!/usr/bin/env bash
# Disassembles one method of GameAssembly.dll by name, with call targets named.
# Usage: tools/native/dis.sh 'fldPlayer.fldPlayerCalcHasigo('   (a unique substring of the symmap name)
# Needs research/native/symmap.txt (tools/native/README.md step 1) and llvm-objdump on PATH.
G="${SMT3HD_DIR:-E:/SteamLibrary/steamapps/common/smt3hd}/GameAssembly.dll"
DIR="$(cd "$(dirname "$0")" && pwd)"
MAP="$DIR/../../research/native/symmap.txt"
line=$(grep -F -m1 "$1" "$MAP") || { echo "not found: $1"; exit 1; }
rva=$(echo "$line" | cut -f1); len=$(echo "$line" | cut -f3)
echo "; $line"
llvm-objdump -d --no-show-raw-insn --start-address=$((0x180000000 + 0x$rva)) --stop-address=$((0x180000000 + 0x$rva + 0x$len)) "$G" \
  | tail -n +7 | perl "$DIR/annot.pl" "$MAP"
