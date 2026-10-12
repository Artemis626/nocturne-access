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

4. Static fields and class pointers (2 Oct 2026, the world map's `fldPlayerCalc_Wm2`). IL2CPP code reaches a class's static fields through a global holding its class pointer (`movq X(%rip), %rax` ... `movq 0xb8(%rax), %rax` ... `OFFSET(%rax)`). To name those globals and offsets:

   ```
   llvm-objdump -d --no-show-raw-insn --start-address=$((0x180000000+0x01C00000)) --stop-address=$((0x180000000+0x028A0000)) GameAssembly.dll | perl tools/native/typeinfo.pl research/native/symmap.txt > research/native/typeinfo.txt
   bash tools/native/dis.sh 'fldPlayer.fldPlayerCalc_Wm2(' | perl tools/native/annot-ti.pl research/native/typeinfo.txt
   perl tools/native/statics.pl research/native/acs fldPlayer        # static field offsets from TypeInfo+0xB8
   perl tools/native/fields.pl research/native/acs/field_H/fldGlobalWork_t.cs   # instance field offsets
   perl tools/native/consts.pl GameAssembly.dll 1828c9e30            # a float constant the code loads (movss X(%rip))
   ```

   `typeinfo.pl` (about 7 s) lists, for every global followed by the class-init test `testb $0x2, 0x12f(reg)`, the classes whose methods use it, most often first. The first name is usually the class itself, not always: a static class used everywhere is named by its heaviest user. Check with the static field offsets. Verified: `0x182e4e678` is `fldGlobal` (its `fldGb` at static offset 0x48, although `fldCommand` is listed first), `0x182e44900` is `dds3Basic`, `0x182e4eeb8` `fldPlayer`, `0x182e4deb0` `fldMain`. `consts.pl` reads the `.xdata` section (virtual address 0x1828BE000, file offset 0x28BC800), where the code's float constants live.

5. Callers of a method (11 Oct 2026, the event prompt scouting): every direct `call` (E8 rel32) in `.text` to the given RVAs, named by the symbol map. Indirect calls (through delegates or virtual tables) are not found.

   ```
   perl tools/native/callers.pl "<game>/GameAssembly.dll" research/native/symmap.txt 2444830 24286C0
   ```

6. Readers of a global (12 Oct 2026, the script command table): every RIP-relative 64-bit load of the given addresses (a
   class TypeInfo pointer from `typeinfo.txt`), named by the symbol map; code in no known method shows as "?" with the
   method before it (static constructors are not in the map: `ScrCommandTable`'s builder was found this way at 0x14F04E3).

   ```
   perl tools/native/riprefs.pl "<game>/GameAssembly.dll" research/native/symmap.txt 182e49f40
   ```
