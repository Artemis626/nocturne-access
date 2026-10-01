# Native code tools (read-only analysis of GameAssembly.dll)

Used since 1 Oct 2026 (navigation, `docs/NAVIGATION-DESIGN.md` section 8). Read-only, offline. Never `smt3hd.exe`.

Needs: `llvm-objdump` (WinLibs, on PATH), `ilspycmd`, perl. Outputs go to `research/native/` (git-ignored: derived from game files).

1. Address map (method start RVA and name), from MelonLoader's own Cpp2IL output, whose dummy assemblies carry `[Address(RVA = ...)]` on every method:

   ```
   ilspycmd -p -o research/native/acs "<game>/MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out/Assembly-CSharp.dll"
   find research/native/acs -name "*.cs" -print0 | xargs -0 perl tools/native/symmap.pl > research/native/symmap.txt
   ```

2. Quickest: `bash tools/native/dis.sh 'fldPlayer.fldPlayerCalcHasigo('` (any unique part of a name in `symmap.txt`; the map has RVA, name, length).

   By hand, disassemble one method with call targets named (RVA and length from the dummy's `Address` attribute):

   ```
   llvm-objdump -d --no-show-raw-insn --start-address=$((0x180000000+RVA)) --stop-address=$((0x180000000+RVA+LEN)) GameAssembly.dll | perl tools/native/annot.pl research/native/symmap.txt
   ```

3. Find every `mov ecx, <constant>; call` (for example a story flag passed to `EventBit.evtBitCheck`), then name the calling method:

   ```
   perl tools/native/scan-imm.pl GameAssembly.dll 18019B000 19A000 2722800 2800 2208 > hits.txt
   perl tools/native/where.pl research/native/symmap.txt hits.txt
   ```

   The three hex numbers are the `.text` virtual address, file offset and size (`llvm-readobj --sections GameAssembly.dll`).

IL2CPP notes: static methods take their arguments in rcx, rdx, r8, r9 and a trailing `MethodInfo*` (usually 0); a class's static fields are at `[TypeInfo + 0xB8]`, in declaration order (8 bytes per reference); array data starts at +0x20, length at +0x18.
