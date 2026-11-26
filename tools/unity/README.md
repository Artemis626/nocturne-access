# Game data tools (read-only, offline)

**Spoiler warning:** this document names places, bosses and events of the game that some players may consider spoilers.

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
- `wap.pl <FNNN.WAP> [area]`: the field's door warp table (the data of `fldWap.gFldWarpBuff.door_buff`) in the Shift+F6 dump's row format (28 Oct 2026; layout in `docs/class-index.md`).

Where things are: field `NNN` bundle `fNNN` (script `fNNN.bf`, per-area `fNNN_AAA.f1/.f2/.TB/.LB`); its English lines
`eNNN_en` (`ENNN_EN`, Souls in `ENNNINF_EN`); prompt words `common_en` `ETC_EN` (`PANELMES_L0001`..`L0011`); field and
area names `common_en` `FLDTERM_EN`, `AREA_EN`, `BASYO_EN`.
- `f2pos.pl <fNNN_AAA.f2>` (2 Nov 2026): an area file's named points (doors `01d_NN`, arrival points `01pos_NN`, events), x y z.
- `f2dump.pl <fNNN_AAA.f2> <field> <area> <x> <z> [y]`: writes a navigation-dump-like file from the area file's collision
  (blocks: nverts, nfaces, 0, vertex ptr, face ptr, next; vertices x y z w; faces 36 bytes: attr, ?, ?, 4 vertex indices, ?, ?)
  and its named points, so `tools/navregions.pl` floods an area never dumped in game (checked: field 34 area 20 gives the same
  19 regions as dump 15:38:40). Moving collision and some trigger blocks are not in it.
- `waybfs.pl <wap.txt> <dir of fNNN files> <field> <start area> <start point> <goal area> <goal volume> [extra]`: the shortest chain of
  volumes through a field without visiting it: floods each area file from its arrival points (navregions.pl), follows the door table;
  extra lines `area volume -> area point label` add script warps (stat doors); `SKIP="a:vol ..."` leaves volumes out, `TRACE=1` prints
  each flood. Area files with no floors (only automap planes, e.g. Third Kalpa areas 0, 1, 5, 20, 21) flood nothing (0.0.141).
- `wapdest.pl <wap.txt> <dir of fNNN files> <areas...>` (`FIELD=NNN`, default 34): each door's real destination with its
  point's position, following the after_flag 1 follow-up warp (`DoorTable`, 0.0.139).
- `waygraph.pl <wap.txt> <field> <goal area> <goal volume>` (`SHOW=1`, `SKIP`) (21 Nov 2026): way-on lines from the door table alone, each area's doors nearest to the goal first, for fields whose area files lack collision the game builds (Yurakucho Tunnel, field 21). Replaced for field 21 by `wayregions.pl`.
- `wayregions.pl <wap.txt> <dir with fNNN_AAA.f2 and fNNN.flow.txt> <field> <goal area> <goal volume>` (`SHOW=1`, `KEEP=dir`) (22 Nov 2026): way-on lines by the walkable parts of each area: floods every area file (`f2dump.pl`, `navregions.pl`), joins the parts by the door table and by script moves (`CALL_FIELD2`, COMM 0x98: slides), and lists each part's exits nearest to the goal first; an area whose file gives some door or landing no floor counts as one part. Yurakucho Tunnel (field 21), 0.0.183.
