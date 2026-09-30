# Nocturne Access

A screen reader accessibility mod for Shin Megami Tensei III Nocturne HD Remaster (Steam). It speaks the game through NVDA so that blind and low-vision players can play it.

## Status

Early development, not yet usable. Phase 0: the mod loads and speaks through NVDA. See `docs/HANDOFF.md` for the roadmap.

## Requirements

- Shin Megami Tensei III Nocturne HD Remaster, Steam version.
- MelonLoader 0.6.1, from the official releases only: https://github.com/LavaGang/MelonLoader/releases
- .NET 6 Desktop Runtime (x64).
- NVDA, with the 64-bit NVDA controller client DLL beside `smt3hd.exe`.

The mod works alongside other MelonLoader mods for this game, such as Nocturne Graphics Configurator.

**Screen reader users: hide the MelonLoader console.** In Steam, open the game's Properties, and under Launch Options enter `--melonloader.hideconsole`. Otherwise NVDA can end up reading the console's log text, especially when playing with a controller (which keeps working when the game window is not focused). Logs are still written to `MelonLoader/Latest.log`.

## Keys

See `docs/KEYS.md`.

## Building

1. Install the .NET 6 SDK or later.
2. Launch the game once with MelonLoader so it generates `MelonLoader/Il2CppAssemblies`.
3. Copy the build references from your install:

   ```bash
   powershell -ExecutionPolicy Bypass -File tools/copy-references.ps1 -GamePath "path to smt3hd"
   ```

4. Optional: copy `NocturneAccess.local.props.example` to `NocturneAccess.local.props` and set `GamePath`. Each build then copies the mod into the game's `Mods` folder.
5. Build:

   ```bash
   dotnet build NocturneAccess.sln -c Release
   ```

Game files and generated assemblies are never committed.

## License

GNU General Public License version 3. See `LICENSE` and `THIRD_PARTY_NOTICES.md`.
