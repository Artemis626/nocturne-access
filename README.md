# Nocturne Access

A screen reader accessibility mod for Shin Megami Tensei III Nocturne HD Remaster (Steam). It speaks the game through NVDA so that blind and low-vision players can play it.

## Status

In active development and playable. It speaks the game's own text through NVDA: nothing is guessed or made up.

### What you can do in game right now

- **Story and menus:** dialogue with speaker names, choices, prompts, every menu, saving and loading, config, name entry.
- **Battles:** whose turn it is, Press Turns, enemy actions, damage and healing, weaknesses and critical hits, Analyze results, skill costs, summoning with each demon's level, HP and MP, and battle results including bosses. Status keys read your party and the enemies at any time, and a battle log repeats what just happened.
- **Shops and services:** the Junk Shop with prices and owned counts, the Fountain of Life with fees, the Terminal and Amala Link travel, Rag's Jewelry trades. At the Cathedral of Shadows: both fusion tables with results and the Demon Compendium.
- **Exploration:** a list of nearby points of interest (doors, exits, ladders, people, demons, item boxes, events); auto-walk to any of them, or spoken directions to walk there yourself; first-person blobber mode that keeps the game's own controls and announces walls and openings; "Where am I"; a memory of the doors you've been through, and of opened boxes. This works in dungeons, fixed-camera rooms and on the overworld map.

### How effective it is

The game has been played from the start through Shinjuku, Shibuya, the Amala Network, Ginza, the Ginza Underpass, Ikebukuro and into the Assembly of Nihilo, beating bosses such as Matador, Orthrus, Yaksini, Thor and Raidou, now in long sessions with eyes completely closed. Handling stairs, ramps and ladders properly made areas that used to be a maze quick to cross.

### Why play through to the end

Every new area of Nocturne has brought something the mod had never met: hidden warps in the Amala Network, ladders in the Underpass, a pyramid built differently from everything before it, corridors of steps. Each one blocked progress until it was found and fixed, often within the same day. The only way to make sure a blind player can finish the game is to play it to the end, find those spots and fix them. Every fix also carries over to the areas still ahead.

Keys: `docs/KEYS.md`. Project state and roadmap: `docs/HANDOFF.md`.

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

## Acknowledgements

Some parts of the game are solved by sight: the Obelisk's time puzzles, the order of the Moirai fights, and the route
through the Labyrinth of Amala. For these the mod's guidance was worked out with the help of fan walkthroughs, then matched
to the game's own data, so the mod speaks only the game's names and never the guides' text. Thanks to their authors:

- [Samurai Gamers](https://samurai-gamers.com/shin-megami-tensei-iii-nocturne-hd-remaster/walkthroughs-8/): the Obelisk
  puzzles and the Labyrinth of Amala, including the
  [Second Kalpa walkthrough](https://samurai-gamers.com/shin-megami-tensei-iii-nocturne-hd-remaster/second-kalpa-walkthrough/)
  behind the way on (Shift+End).
- [Ludo.guide](https://www.ludo.guide/guide/shin-megami-tensei-iii-nocturne-hd-remaster): the Obelisk puzzle routes.
- [Screen Rant](https://screenrant.com/), [GameFAQs](https://gamefaqs.gamespot.com/) and
  [Neoseeker](https://www.neoseeker.com/): the Moirai sisters' fights.

### Where the way on (Shift+End) has data, and its sources

Each route below was read from the guides, then matched to the game's door table, area files and field script
(`src/NocturneAccess/Data/WayOn.txt` holds the details per area). Other places have no way on yet: Shift+End says
"None known for this area."

| Place | Field | Sources |
|---|---|---|
| Labyrinth of Amala, Second Kalpa | 42 | [Samurai Gamers, Second Kalpa walkthrough](https://samurai-gamers.com/shin-megami-tensei-iii-nocturne-hd-remaster/second-kalpa-walkthrough/) |
| Amala Network (second visit), the soul rooms of areas 5 and 8 | 28 | [LP Archive, FrankZP part 23](https://lparchive.org/Shin-Megami-Tensei-Nocturne-(by-FrankZP)/Update%2023/), [Neoseeker](https://www.neoseeker.com/shin-megami-tensei-iii-nocturne-hd-remaster/) (search summary); area 8 from the field script alone |
| Ikebukuro Tunnel, to the Asakusa exit | 26 | [Samurai Gamers, Kabukicho Prison and Ikebukuro Tunnel](https://samurai-gamers.com/shin-megami-tensei-iii-nocturne-hd-remaster/kabukicho-prison-and-ikebukuro-tunnel-walkthrough/), [LP Archive, FrankZP part 17](https://lparchive.org/Shin-Megami-Tensei-Nocturne-(by-FrankZP)/Update%2017/) |
| Asakusa Tunnel, Yushima to Gaienmae | 29 | [Game8, Yoyogi Park 2](https://game8.co/games/Shin-Megami-Tensei-III-Nocturne/archives/333251), [Neoseeker, Asakusa Tunnel](https://www.neoseeker.com/shin-megami-tensei-iii-nocturne-hd-remaster/Asakusa_Tunnel), [Samurai Gamers, part 8](https://samurai-gamers.com/?p=212902), [LP Archive, FrankZP part 25](https://lparchive.org/Shin-Megami-Tensei-Nocturne-(by-FrankZP)/Update%2025/) |
| Yoyogi Park, the construction site to the communication tower | 16 | [Samurai Gamers, Yoyogi Park Revisited](https://samurai-gamers.com/?p=212994), [LP Archive, FrankZP part 26](https://lparchive.org/Shin-Megami-Tensei-Nocturne-(by-FrankZP)/Update%2026/), [Game8, Yoyogi Park 2](https://game8.co/games/Shin-Megami-Tensei-III-Nocturne/archives/333251) |
| Amala Temple: Black Temple to Alciel, White Temple to Albion, Red Temple to Skadi | 34 | [Samurai Gamers, Amala Temple walkthrough](https://samurai-gamers.com/shin-megami-tensei-iii-nocturne-hd-remaster/amala-temple-walkthrough); the route itself read from the game's door table, area files and field script |
| Labyrinth of Amala, Third Kalpa: entrance to the ladder down to the chase (B1F), by the player's highest stat (Strength, Magic or Luck wing) | 43 | read from the game's door table, area files and field script offline (`tools/unity/waybfs.pl`); no walkthrough used |

## License

GNU General Public License version 3. See `LICENSE` and `THIRD_PARTY_NOTICES.md`.
