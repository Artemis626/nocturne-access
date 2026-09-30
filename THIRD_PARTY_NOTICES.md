# Third-party notices

Nocturne Access is licensed under the GNU General Public License version 3 (see `LICENSE`). It uses the components below. None of their files are stored in this repository.

## Bundled in release packages

### NVDA Controller Client

- File: `nvdaControllerClient.dll` (64-bit), shipped unmodified beside `smt3hd.exe`.
- Copyright: NV Access Limited.
- License: GNU Lesser General Public License version 2.1.
- Source: https://github.com/nvaccess/nvda (controller client), https://www.nvaccess.org/
- The full license text will be included here before the first release.

## Required at runtime, not bundled

### MelonLoader

- License: Apache License 2.0.
- Install only from the official releases: https://github.com/LavaGang/MelonLoader/releases
- MelonLoader ships HarmonyX (MIT License) and Il2CppInterop (GNU LGPL version 3), which this mod links against at runtime.

## Optional, not bundled

### Tolk

- Screen reader abstraction library by Davy Kager. License: GNU LGPL. Source: https://github.com/dkager/tolk
- Used only if the player places `Tolk.dll` beside `smt3hd.exe`.

## Design references

No code from these projects is included yet. If any is borrowed, it will be credited here with its license text.

- NocturneModernController, NocturneAddNewSkills (MIT, Gray Ghost): class and method names for field and skill data.
- MatthiewNocturne (MIT, Matthiew Purple): battle UI class names and object paths.
- Suikoden Access (Francesco): text-hooking design, `docs/TEXT-HOOKING-HANDOFF.md`.

## Game files

Shin Megami Tensei III Nocturne HD Remaster is © ATLUS / SEGA. No game files, and no assemblies generated from them, are included in this repository or its releases.
