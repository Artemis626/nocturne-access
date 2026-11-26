# Silent public release: copyright check and history (26 Nov 2026)

Francesco's request (26 Nov 2026): make the GitHub repository visible, with no announcement and no release, before the proper
launch. Conditions: (1) the sanity check finished (done, `docs/SANITY-CHECK-PLAN.md`, second pass); (2) a copyright check, with
game text and assets scrubbed (done, below); (3) a report on how git history fits in, before anything is executed (below).
Nothing in this file has been executed on GitHub: the repository is still private.

## 1. Copyright check: method

- Every file ever added to the history (172 paths over 291 commits) listed with `git log --all --name-only`, and the largest blobs
  read with `git cat-file`.
- A corpus of the game's English text, from the local extracts in `research/gamedata` (git-ignored): every field's message table
  (`eNNN_en.txt`, 48 files), the shared names table and the battle text: 8 706 lines, 28 075 six-word runs.
- Every tracked file, and every line ever added in the history (`git log -p`, 40 987 lines), matched against the corpus for runs
  of six or more words in a row (five for dialogue only). Script: `tools/copyright-check.pl` (it holds no game text; the
  corpus stays in the git-ignored `research/`). Run it again before the proper launch.
- Code and docs searched for disassembly, byte dumps, decompiled method bodies, binary files and personal data.

## 2. Copyright check: findings

- **No game files, assets or binaries, ever.** The history holds only source, docs, tools and three data files. `research/`,
  `_dlls/` and save backups were git-ignored from the start. The mod's sounds are synthesised in code.
- **No decompiled or disassembled game code.** The docs and `docs/class-index.md` name classes, methods, fields and addresses
  (facts needed to hook the game, as every IL2CPP mod does), with no method bodies. The only code blocks in the docs are the
  mod's own.
- **The mod never stores game text to speak it.** Lines are read at run time from the player's own copy, by the game's text ids
  (for example `F044INF_L0078`). The code's quotations of game lines are all in comments.
- **Data files are facts:** volume names, coordinates, flags and area numbers, with short notes.
- **Verbatim game text in the docs:** about 90 matching lines, none longer than 14 words. Most are short interface lines and
  prompts ("There is a shaft leading downward.", "Will you leave the area?", the config screen's help) that the docs need to say
  what the player hears, plus place names and the game's title. **Ten were character dialogue** (a demon's battle talk, the
  statue's voice, NPC lines, a Soul's hint): paraphrased on 26 Nov 2026 in `docs/HANDOFF.md`, `docs/HANDOFF-OVERWORLD.md`,
  `docs/PATHFINDING.md` and `docs/FOURTH-KALPA-PLAN.md`.
- **Walkthroughs:** routes are cited with links in the README (its way-on sources table) and turned into the game's own volume
  names; no guide text is copied.
- **Personal data:** Francesco's Windows user name in two doc paths (replaced with `%USERPROFILE%`). Still present: his
  e-mail address as the author of every commit (see section 3), and his first name and eyesight in `CLAUDE.md` and the notes
  (his choice whether that stays).
- **Added:** a "Legal" section in the README (unofficial, not affiliated with ATLUS or SEGA, no game files, game text read at
  run time, the executable never touched); `THIRD_PARTY_NOTICES.md` now says the same, and that the NVDA controller client is
  downloaded by the player, as the README says (it said "bundled").

## 3. Git history: what is in it

- One branch (`main`), one tag (`pre-labyrinth`), 291 commits, 13 MB. No fork, no other clone known.
- Game text found only in the history: exactly the ten dialogue lines paraphrased above, in older versions of the notes. Nothing
  else differs from the current files.
- Every commit carries the author e-mail of the Git setup (a personal Gmail address). On a public repository anyone can read it
  (`git log`, or `.patch` after any commit URL).
- The two old doc lines with the Windows user name, and every earlier version of the session notes.

## 4. Git history: the options

### Option A: make the repository public as it is
- One switch in the repository's settings on GitHub. No change to the local folder or to the way of working.
- Exposed: the ten short dialogue lines in old note versions (each under 15 words, quoted in bug reports: a very low copyright
  risk), and the e-mail address on 291 commits.
- Recommended if the e-mail address does not matter.

### Option B: rewrite the history in place, then make it public
- Tool: `git filter-repo` (not installed; a Python package). Run on this same folder, after a full copy of it as a backup:
  `--replace-text` swaps the ten dialogue lines and the user-name paths in every old version, `--mailmap` swaps the author
  e-mail for the GitHub no-reply address. Then the remote is added again and pushed with force (all branches and the tag).
- **No new clone is needed:** this folder is rewritten in place and goes on working. The repository keeps its name and settings.
- Effects: every commit gets a new hash; the 10 or so hashes quoted in old notes (`docs/HANDOFF.md`, `docs/EXPLORED-DOORS-PLAN.md`)
  no longer resolve (harmless, they are history); the tag moves to the rewritten commit; any other copy of the repository would
  need a fresh clone (none known).
- Caveat: GitHub may keep the old commits reachable by their exact hash for a while; nobody outside has those hashes, and GitHub
  Support can purge them. To avoid even that: push the rewritten history to a new private repository, delete the old one, give
  the new one the old name. Locally that is one `git remote set-url`, still no new clone.
- Timing: about half an hour, with the game closed and nothing uncommitted; the mod's work is not affected after that.

### Option C: a fresh repository with one commit
- Not recommended: it throws away the history, which is the project's record of what was tested and why.

### In every case, before or with the switch
- Future commits: set the Git e-mail to the GitHub no-reply address (GitHub, Settings, Emails, "Keep my email addresses
  private"). Turning on "Block command line pushes that expose my email" would make every push fail until the old address is
  out of the commits being pushed: turn it on only after Option B, or not at all with Option A.
- `docs/TECHNICAL-OVERVIEW.md` and its website page (page 13) say the repository is private: both need a line changed, the page
  only with Francesco's go-ahead.
- The README says nothing about privacy; its status section already says there is no release yet.
- After the switch every push is public at once, session notes included: the way of working does not change, only who can read.

## 5. Recommendation

Option B in place, if the e-mail address should stay private; otherwise Option A. Either way the copyright side is ready: what
remains in the history is ten short lines of dialogue in old developer notes.
