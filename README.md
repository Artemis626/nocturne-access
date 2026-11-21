# Nocturne Access

A screen reader accessibility mod for Shin Megami Tensei III Nocturne HD Remaster (Steam). It speaks the game through NVDA so that blind and low-vision players can play it.

## Status

In active development and playable. It speaks the game's own text through NVDA: nothing is guessed or made up.

### What you can do in game right now

- **Story and menus:** dialogue with speaker names, choices, prompts, every menu, saving and loading, config, name entry.
- **Battles:** whose turn it is, Press Turns, enemy actions, damage and healing, weaknesses and critical hits, statuses as they start and end, Analyze results, skill costs, summoning with each demon's level, HP and MP, and battle results including bosses. Status keys read your party and the enemies at any time, and a battle log repeats what just happened.
- **Shops and services:** the Junk Shop with prices and owned counts, the Fountain of Life with fees, the Terminal and Amala Link travel, Rag's Jewelry trades. At the Cathedral of Shadows: both fusion tables with results and the Demon Compendium.
- **Exploration:** a list of nearby points of interest (doors, exits, ladders, people, demons, item boxes, events); auto-walk to any of them, or spoken directions to walk there yourself; first-person blobber mode that keeps the game's own controls and announces walls and openings; "Where am I"; a memory of the doors you've been through, and of opened boxes; the way on (Shift+End) in the hardest dungeons; step-by-step help for the Obelisk's time puzzles; an autopilot for the Labyrinth's falling minigame. This works in dungeons, fixed-camera rooms and on the overworld map.

### How effective it is

As of November 2026, the game has been played in Chronicle mode, in long sessions with eyes completely closed, from the start well into its second half:

- **Places:** Shinjuku, Shibuya, the Amala Network (both visits), Ginza and its Underpass, Ikebukuro and Mantra's Headquarters, the Assembly of Nihilo, Kabukicho Prison, the Ikebukuro Tunnel, Asakusa and Mifunashiro, the Obelisk with its time puzzles, Yoyogi Park, the Asakusa Tunnel, the Amala Temple (all three temples), and the Labyrinth of Amala's First, Second and Third Kalpas.
- **Bosses beaten include:** Matador, Orthrus, Yaksini, Thor, Raidou (twice, the second time at the end of the Third Kalpa), Daisoujou, Ose, Mizuchi, Kin-Ki, the Moirai sisters, the White, Red, Black and Pale Riders, Alciel and the Mother Harlot.

Handling stairs, ramps, ladders and hidden warps properly made areas that used to be a maze quick to cross. In the hardest dungeons (the Labyrinth's Kalpas, the Amala Temple, Yoyogi Park's construction site), the way on (Shift+End) points to the next step of the main route.

### Why play through to the end

Every new area of Nocturne has brought something the mod had never met: hidden warps in the Amala Network, ladders in the Underpass, a pyramid built differently from everything before it, corridors of steps, a prison whose floors swap places, the Obelisk's time puzzles, the Labyrinth's falling minigame and stat-locked doors. Each one blocked progress until it was found and fixed, often within the same day. The only way to make sure a blind player can finish the game is to play it to the end, find those spots and fix them. Every fix also carries over to the areas still ahead.

### Design principles

- **The game stays as vanilla as possible.** The mod makes up for what a blind player cannot see. It does not make up for what every player finds hard: an obscure quest or an unintuitive chain of events is the same puzzle with or without the mod, so the mod leaves it to the player. Example: the Labyrinth's Star Key, found only through a chain of conversations across several places.
- **The way on (Shift+End) is a last resort.** It is used only in dungeons where getting through feels impossible without the game's map, never to point the way through the story.
- **Where the game hides the map from everyone, so does the mod.** It makes moving without the map completely reliable instead. The Labyrinth's Fourth Kalpa gives no map to any player; with the mod it was crossed blind, challenging but doable, with no way on.
- **Changes are a last resort too, and always disclosed.** Sound cues come first, then help that does the task for you, and skipping a part only when nothing else works. Every change is listed below with its switch, and bosses are never skipped.

### Game modes

**Recommended: play in Chronicle mode.** The Chronicle Pack is free DLC on Steam (it must be downloaded separately from the game). It is the mode the mod is played and tested in, so it is the one with full compatibility.

The mod is meant to work the same in every mode the game offers: **New Game** (the 2003 game, without the extra content), **Chronicle** (the free Chronicle Pack DLC, with Raidou, the Labyrinth of Amala and more) and **Maniax** (paid DLC, with Dante instead of Raidou). It is played and tested in Chronicle mode. The game's data was compared offline: Maniax uses the same maps, doors and puzzles as Chronicle (only story scenes differ), so the Labyrinth's way on and the changes below apply to both, and where the mod names Raidou it says Dante in Maniax. New Game has no Labyrinth, so none of that comes up there.

### Content changed or skipped for accessibility

A few short parts of the game depend on reacting to what is on screen faster than speech or sound can tell you. Rather than hold back the whole mod for them, the mod changes them, and says so here. Each change can be switched off to play the game's original version: in `UserData/NocturneAccess.cfg` (with the game closed), or with a key in game where noted.

- **The Labyrinth of Amala, Third Kalpa: Raidou's chase (Dante's in Maniax mode). Temporary.** A stealth section in real time: the pursuer runs faster than a player can react by ear, even with the mod's warning sounds. With `ChaseOff = true` (the default) he stands still and never catches you, and on first reaching the chase after loading a save the mod says so ("Chase disabled: set ChaseOff to false ...");  the rest of the section, its doors and the story go on as normal. **The boss fight against him at the end is never skipped.** An accessible way to play the chase is still wanted; until then it stays off. With `ChaseOff = false` you get the game's chase, with a low pulse that quickens as he closes in (`ChaseSound`).
- **The Labyrinth of Amala's falling minigame.** The mod steers the fall for you (F4 hands it back to you during the fall, or a direction key takes over at once), and the party takes no damage from objects hit (`PipeNoDamage = true`, the default; macca lost to hits is unchanged).
- **Asakusa: the Puzzle Boy minigame (the Manikin child), a visual block puzzle the mod cannot read.** Its prize, the Geis Magatama, is needed for later challenges. Once you beat Metatron in the Labyrinth of Amala, the mod gives you Geis the way the Puzzle Boy does, and marks his prize as taken (`GeisGift = true`, the default; it says "Metatron defeated: the Puzzle Boy's prize is yours. Obtained the Geis Magatama."). With `GeisGift = false` the prize stays with the minigame.

Keys: `docs/KEYS.md`. Project state and roadmap: `docs/HANDOFF.md`.

## Requirements

- Shin Megami Tensei III Nocturne HD Remaster, Steam version.
- MelonLoader 0.6.1, from the official releases only: https://github.com/LavaGang/MelonLoader/releases
- .NET 6 Desktop Runtime (x64).
- NVDA, with the 64-bit NVDA controller client DLL (see below).

### Screen reader support

For now the mod supports **NVDA only**. It speaks by calling NV Access's 64-bit controller client, `nvdaControllerClient.dll`, directly. Other screen readers (JAWS, Narrator and others) are not supported yet. Supporting them is a goal for later.

The controller client can't be distributed with this mod because of licence incompatibility, so you need to download it yourself, once:

1. Open NV Access's official release page: https://download.nvaccess.org/releases/stable/
2. Download the file whose name ends in `_controllerClient.zip`, for example `nvda_2025.3_controllerClient.zip`.
3. In the zip, open the `x64` folder and copy `nvdaControllerClient.dll`. Use the 64-bit copy, not the 32-bit one in `x86`.
4. Paste it into the game folder, beside `smt3hd.exe` (for example `steamapps\common\smt3hd`). The mod also looks in the game's `UserLibs` folder.

The mod works alongside other MelonLoader mods for this game, such as Nocturne Graphics Configurator.

**Screen reader users: hide the MelonLoader console.** In Steam, open the game's Properties, and under Launch Options enter `--melonloader.hideconsole`. Otherwise NVDA can end up reading the console's log text, especially when playing with a controller (which keeps working when the game window is not focused). Logs are still written to `MelonLoader/Latest.log`.

## Keys

Press **F7** on any screen to hear its keys: the game's own (from its button guide) and the mod's. The full list is in `docs/KEYS.md`.

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
| Amala Temple: Black Temple to Alciel, White Temple to Albion, Red Temple to Skadi | 34 | [Samurai Gamers, Amala Temple walkthrough](https://samurai-gamers.com/shin-megami-tensei-iii-nocturne-hd-remaster/amala-temple-walkthrough); the route itself read from the game's door table, area files and field script; the Red Temple's shadow maze (its alarm, reset tiles and one-way doors) solved offline from the game's files (`tools/unity/f2vol.pl`, `tools/unity/shadowroute.pl`), played through to Skadi on 6 Nov 2026 |
| Labyrinth of Amala, Third Kalpa: entrance to the ladder down to the chase (B1F), by the player's highest stat (Strength, Magic or Luck wing); on a second traversal (after the Kalpa is done, before the Fourth Kalpa's B2 switch), to the B1F side room's shaft down to that switch | 43 | read from the game's door table, area files and field script offline (`tools/unity/waybfs.pl`); no walkthrough used |
| Labyrinth of Amala, Fifth Kalpa: Hall of Candles to the lift into the deepest part, through the Voice of the Door hub (Magic 25 and Agility 20 demons) and the chain of one-way doors | 45 | read from the game's door table, area files and field script offline (`tools/unity/waybfs.pl`), checked against [Samurai Gamers, Fifth Kalpa walkthrough](https://samurai-gamers.com/shin-megami-tensei-iii-nocturne-hd-remaster/fifth-kalpa-walkthrough/) |
| Yurakucho Tunnel: Yurakucho Station to the exit north towards the Diet Building (Sannou-Nagatacho) | 21 | read from the game's door table only (`tools/unity/waygraph.pl`: each area's doors nearest to the exit first; the area files lack part of the tunnel's collision); no floor-by-floor guide found ([Ludo.guide, Yurakucho Tunnel](https://www.ludo.guide/guide/shin-megami-tensei-iii-nocturne-hd-remaster/able-to-enter-the-world-to-which-i-travel-he-leave) describes its chutes, ladders and one-way doors) |

## License

GNU General Public License version 3. See `LICENSE` and `THIRD_PARTY_NOTICES.md`.
