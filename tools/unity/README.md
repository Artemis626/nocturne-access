# Game data tools (read-only, offline)

Built 12 Oct 2026 for the event classification (`docs/EVENT-PROMPTS-PLAN.md`). They read the game's Unity asset bundles in
`smt3hd_Data/StreamingAssets/PC` (UnityFS 6, LZ4 blocks, Unity 2019.4; SerializedFile 21 with type trees). Data files only:
never the executable. Outputs go to `research/gamedata/` (git-ignored: derived from game files).

- `UnityBundle.pm`: the reader (LZ4, bundle directory, SerializedFile objects, type-tree reading).
- `list.pl <bundle> [name regex]`: objects of a bundle (path id, class id, size, name).
- `extract.pl <bundle> <out dir> <name regex>`: TextAsset bytes (`fNNN.bf`, `fNNN_AAA.TB` ...).
- `dumpobj.pl <bundle> <name regex>`: MonoBehaviour tables through their type trees (`ETC_EN`, `E020_EN`, `FldHitData`).
  Localization lines: `perl dumpobj.pl $P/e020_en . | perl -ne 'chomp; if (/msgID = (\S+)/) { $id = $1 } elsif (/msgStr = (.*)/) { print "$id = $1\n" }'`.
- `flowscript.pl <file.bf> [procedure regex]`: disassembles a FlowScript file (procedures, labels, PUSHI with its value word,
  COMM numbers, PUSHSTR strings).
- `bmd.pl <file.bf> [N]`: the message block's dialogs (name, kind, lines, localization keys).
- `events.pl <file.bf> <localization lines> [regex]`: per event procedure the flags it tests first, its prompt and first message.
- `simulate.pl <file.bf> [regex]` (`FLAGS="1268 1269"`): the dry run the mod's `FieldScript` does in game (action, story, idle).

Where things are: field `NNN` bundle `fNNN` (script `fNNN.bf`, per-area `fNNN_AAA.f1/.f2/.TB/.LB`); its English lines
`eNNN_en` (`ENNN_EN`, Souls in `ENNNINF_EN`); prompt words `common_en` `ETC_EN` (`PANELMES_L0001`..`L0011`); field and
area names `common_en` `FLDTERM_EN`, `AREA_EN`, `BASYO_EN`.
