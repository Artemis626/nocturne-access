using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Il2Cpp;
using Il2Cppfield_H;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NocturneAccess.Core;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Navigation;
using UnityEngine;
using static NocturneAccess.Systems.Navigation.FieldData;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Shift+F6 (debug; F5 until 0.0.19): writes the navigation data of the loaded field, read only, to
    /// UserData/NocturneAccess/NavDumps/navdump_[time].txt, and a summary to Latest.log as [Nav:Dump] lines.
    /// Sections: player and camera, place names, New Game+ statics, collision polygons with their face flags,
    /// auto-map areas, sub-blocks and icons, NPCs, item boxes, event volumes, and the input and story-flag
    /// tables gathered by NavProbe. Every name read here is in docs/class-index.md "Field navigation".
    /// Each section is guarded on its own, so one failing read does not lose the rest.
    /// </summary>
    internal static class NavDump
    {
        private const int NearestCollision = 12;
        private const int NearestEvents = 15;

        private static DumpWriter _dump;

        private static void Both(string line) => _dump.Both(line);

        private static void FileOnly(string line) => _dump.FileOnly(line);

        private static void Section(string name, Action body) => _dump.Section(name, body);

        internal static void Dump()
        {
            // Field statics are only read once the game's own field update has run.
            if (FieldState.CalcCount == 0)
            {
                Log.Info("[Nav:Dump] No field loaded yet.");
                SpeechManager.Say("No field loaded yet.", interrupt: true, force: true);
                return;
            }

            _dump = new DumpWriter("[Nav:Dump]", "NavDumps", "navdump", "Navigation");
            PlayerSnapshot s = default;

            FileOnly($"Nocturne Access {ModMain.ModVersion} navigation dump, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Section("Player", () => s = DumpPlayer());
            Section("Names", () => DumpNames(s));
            Section("New Game+", () => Both(NavProbe.NewGame2Line()));
            Section("Collision", () => DumpCollision(s));
            Section("Auto-map", () => DumpAutoMap(s));
            Section("NPCs", () => DumpNpcs(s));
            Section("Item boxes", () => DumpBoxes(s));
            Section("Event volumes", () => DumpEvents(s));
            Section("Door warp table", () => DumpDoorTable(s));
            Section("Warps of this area (0.0.52)", () =>
            {
                List<Warp> warps = WarpMap.All(s.FieldId, s.AreaId);
                Both($"{warps.Count} warps ({warps.Count(w => w.Touch)} fire when walked into)");
                foreach (Warp w in warps)
                    Both($"  {w.Name} gEvnRng {w.Index} movetype {w.MoveType} at {F(w.Zone.X)} {F(w.Zone.Z)} facing {F(w.Zone.NX)} {F(w.Zone.NZ)} " +
                         $"{F(w.Zone.HalfWidth * 2)} wide {F(w.Zone.HalfDepth * 2)} deep -> {w.DestName} " +
                         (w.HasDest ? $"at {F(w.DestX)} {F(w.DestY)} {F(w.DestZ)}" : "not found"));
                FileOnly("Resource tables of the loaded area file:");
                foreach (string line in WarpMap.DescribeTables())
                    FileOnly(line);
            });
            Section("Points of interest and walk grid", () => DumpWalkGrid(s));
            Section("Input and flags", DumpInputAndFlags);

            _dump.Save();
            _dump = null;
        }

        // ---- player ----

        private static PlayerSnapshot DumpPlayer()
        {
            PlayerSnapshot s = ReadPlayer();
            Both($"exploring {FieldState.IsExploring}; fldPlayerCalc calls {FieldState.CalcCount} (Nml {NavProbe.NmlCalls}, syukan {NavProbe.SyukanCalls})");
            if (!s.Valid)
            {
                Both("fldGlobal.fldGb is null: not in a field.");
                return s;
            }

            fldGlobalWork_t gb = fldGlobal.fldGb;
            Both($"fieldID {s.FieldId} areaID {s.AreaId} blockID {s.BlockId} fieldOldID {gb.fieldOldID} areaOldID {gb.areaOldID} " +
                 $"AmSBlock {s.AmSBlock} AmFloor {s.AmFloor} ElvIdx {s.ElvIdx} AreaToArea {gb.AreaToArea} flag {gb.flag}");
            Both($"player {F(s.X)} {F(s.Y)} {F(s.Z)}; old {F(gb.playerOldX)} {F(gb.playerOldY)} {F(gb.playerOldZ)}; " +
                 $"new {F(gb.playerNewX)} {F(gb.playerNewY)} {F(gb.playerNewZ)}");
            Both($"PlayerKakudo {F(s.Kakudo)} goal {F(s.KakudoGoal)} PlayerSaka {F(gb.PlayerSaka)} LastMoveLeng {F(gb.LastMoveLeng)} HitUpdate {gb.HitUpdate}");
            Both($"player object {(s.HasObject ? V(s.ObjPos) + " yaw " + F(s.ObjYaw) : "none")}; " +
                 $"camera object {(s.HasCamera ? V(s.CamPos) + " yaw " + F(s.CamYaw) : "none")}");
            if (s.HasObject)
            {
                Transform t = fldPlayer.fldPlayerObj.transform;
                Both($"player object forward {V(t.forward)} euler {V(t.eulerAngles)} scale {V(t.lossyScale)} name {Q(fldPlayer.fldPlayerObj.name)}");
            }
            Both($"cammode {s.CamMode} cammode2 {s.CamMode2} cammode5name {Q(gb.cammode5name)} syukan_cam_reset_flg {s.SyukanCamReset} campos {V(gb.campos)}");
            Both($"fldPlayer: act {s.Act} run {s.Run} gKeyInputDir {s.KeyInputDir} gKeyInputCnt {fldPlayer.gKeyInputCnt} " +
                 $"fldCamKakudoGoal {F(fldPlayer.fldCamKakudoGoal)} gfldPlayerKakudo2 {F(fldPlayer.gfldPlayerKakudo2)} " +
                 $"gfldPlayerKakudo2Mode {fldPlayer.gfldPlayerKakudo2Mode} gFldPlayerSyukanDly {s.SyukanDly} PlayerStayCnt {fldPlayer.PlayerStayCnt} gPlayerMsk {fldPlayer.gPlayerMsk}");
            Both($"fldCamera: CameraMoveDir {F(fldCamera.CameraMoveDir)} fldCamDirLockRotY {F(fldCamera.fldCamDirLockRotY)}");
            Both($"NoEncountCnt {gb.NoEncountCnt} EncPID {gb.EncPID} NoInpPlCnt {gb.NoInpPlCnt} NoInpAmCnt {gb.NoInpAmCnt}");
            return s;
        }

        // ---- names ----

        private static void DumpNames(PlayerSnapshot s)
        {
            if (!s.Valid)
                return;

            Both($"area name (fld {s.FieldId}, area {s.AreaId}): {Q(AreaName(s.FieldId, s.AreaId))}");
            Both($"floor name (fld {s.FieldId}, floor AmFloor {s.AmFloor}): {Q(FloorName(s.FieldId, s.AmFloor))}");

            // One fresh read for the whole dump; the collision section uses it too.
            CollisionCache.Refresh(s.FieldId, s.AreaId, force: true);
            CollisionPoly floor = CollisionCache.FloorUnder(s.X, s.Y, s.Z, out float planeY);
            if (floor == null || !floor.HasFace)
                Both("place (basyo): no floor polygon found under the player");
            else
                Both($"place (basyo {floor.Basyo}, floor polygon #{floor.Index} at y {F(planeY)}): {Q(BasyoName(s.FieldId, floor.Basyo))}");

            Il2CppReferenceArray<Il2CppStructArray<byte>> fieldNames = fldGlobal.field_name;
            if (fieldNames != null)
            {
                string raw = s.FieldId >= 0 && s.FieldId < fieldNames.Length ? Bytes(fieldNames[s.FieldId]) : "out of range";
                Both($"field_name: {fieldNames.Length} entries; [{s.FieldId}] {raw}");
            }

            // The name table Where am I reads (0.0.54): FldNameData.GetFieldName, GetSuspendPosName, and every field name it holds.
            try
            {
                FldNameData nd = fldGlobal.fldNameData;
                if (nd != null)
                {
                    Both($"FldNameData: field {Q(nd.GetFieldName(s.FieldId))}, area {Q(nd.GetAreaName(s.FieldId, s.AreaId))}, suspend {Q(nd.GetSuspendPosName(s.FieldId, s.AreaId))}");
                    var all = new List<string>();
                    Il2CppSystem.Collections.Generic.List<string> tbl = nd._fldFieldNameTbl;
                    for (int i = 0; tbl != null && i < tbl.Count; i++)
                        all.Add($"{i} {Q(tbl[i])}");
                    FileOnly("FldNameData field names: " + string.Join("; ", all));
                }
            }
            catch (Exception ex)
            {
                Both($"FldNameData could not be read: {ex.Message}");
            }

            Il2CppReferenceArray<fldAreaName_t> areas = fldGlobal.area_name;
            Il2CppReferenceArray<fldBasyoName_t> places = fldGlobal.basyo_name;
            Il2CppReferenceArray<fldFloorName_t> floors = fldGlobal.floor_name;
            Both($"name tables: area_name {areas?.Length ?? -1}, basyo_name {places?.Length ?? -1}, floor_name {floors?.Length ?? -1} entries (rows for this field in the file)");

            int shown = 0;
            if (areas != null)
                for (int i = 0; i < areas.Length; i++)
                {
                    fldAreaName_t e = areas[i];
                    if (e == null) continue;
                    if (e._fld == s.FieldId || i < 5) { FileOnly($"  area_name[{i}] fld {e._fld} area {e._area} {Q(e._name)}"); shown++; }
                }
            if (places != null)
                for (int i = 0; i < places.Length; i++)
                {
                    fldBasyoName_t e = places[i];
                    if (e == null) continue;
                    if (e._fld == s.FieldId || i < 5) { FileOnly($"  basyo_name[{i}] fld {e._fld} basyo {e._basyo} {Q(e._name)}"); shown++; }
                }
            if (floors != null)
                for (int i = 0; i < floors.Length; i++)
                {
                    fldFloorName_t e = floors[i];
                    if (e == null) continue;
                    if (e._fld == s.FieldId || i < 5) { FileOnly($"  floor_name[{i}] fld {e._fld} floor {e._floor} {Q(e._name)}"); shown++; }
                }
            Both($"name rows written to the file: {shown} (this field, plus the first 5 of each table)");
        }

        private static string Bytes(Il2CppStructArray<byte> a)
        {
            if (a == null)
                return "null";
            var hex = new StringBuilder();
            var text = new StringBuilder();
            for (int i = 0; i < a.Length; i++)
            {
                byte b = a[i];
                hex.Append(b.ToString("X2")).Append(' ');
                text.Append(b >= 32 && b < 127 ? (char)b : '.');
            }
            return $"{a.Length} bytes [{hex.ToString().Trim()}] \"{text}\"";
        }

        // ---- collision ----

        private static void DumpCollision(PlayerSnapshot s)
        {
            if (!s.Valid)
                return;

            IReadOnlyList<CollisionPoly> polys = CollisionCache.Polys;
            Both($"gfldCollsionNum {CollisionCache.SourceCount}, gfldCollsionBlkNum {CollisionCache.SourceBlockCount}, array {CollisionCache.ArrayLength}, read {polys.Count}");
            if (polys.Count == 0)
                return;

            int floors = polys.Count(p => Math.Abs(p.Normal.y) >= 0.5f);
            int walls = polys.Count(p => Math.Abs(p.Normal.y) < 0.2f);
            Both($"by normal: floor-like (|ny| >= 0.5) {floors}, wall-like (|ny| < 0.2) {walls}, between {polys.Count - floors - walls}");
            Both("mode2D: " + Histogram(polys, p => p.Mode2D.ToString()));
            Both("numedges: " + Histogram(polys, p => p.NumEdges.ToString()));
            Both("points array length: " + Histogram(polys, p => p.PointArrayLength.ToString()));
            Both("hitflag: " + Histogram(polys, p => p.HitFlag.ToString()));
            Both("blockID: " + Histogram(polys, p => p.BlockId.ToString()));
            Both("npcID: " + Histogram(polys, p => p.NpcId.ToString()));
            Both("has pFace: " + Histogram(polys, p => p.HasFace.ToString()));
            Both("Attr: " + Histogram(polys, p => "0x" + p.Attr.ToString("X")));
            Both("Myuka: " + Histogram(polys, p => p.Myuka.ToString()));
            Both("Se: " + Histogram(polys, p => p.Se.ToString()));
            Both("Amap0/Amap1: " + Histogram(polys, p => p.Amap0 + "/" + p.Amap1));
            Both("EncType: " + Histogram(polys, p => p.EncType.ToString()));
            Both("Myuka by normal (floor-like / wall-like): " +
                 Histogram(polys.Where(p => Math.Abs(p.Normal.y) >= 0.5f).ToList(), p => p.Myuka.ToString()) + " / " +
                 Histogram(polys.Where(p => Math.Abs(p.Normal.y) < 0.2f).ToList(), p => p.Myuka.ToString()));

            var basyos = polys.Where(p => p.HasFace).GroupBy(p => p.Basyo).OrderBy(g => g.Key);
            Both("Basyo: " + string.Join(", ", basyos.Select(g => $"{g.Key} x{g.Count()} {Q(BasyoName(s.FieldId, g.Key))}")));

            var all = polys.SelectMany(p => p.Points).ToList();
            if (all.Count > 0)
                Both($"point bounds: x {F(all.Min(p => p.x))}..{F(all.Max(p => p.x))}, y {F(all.Min(p => p.y))}..{F(all.Max(p => p.y))}, " +
                     $"z {F(all.Min(p => p.z))}..{F(all.Max(p => p.z))}");

            CollisionPoly under = CollisionCache.FloorUnder(s.X, s.Y, s.Z, out float planeY);
            Both(under == null ? "floor under the player: none" : $"floor under the player (plane y {F(planeY)}): {Describe(under)}");

            Both($"nearest {NearestCollision} polygons to the player (horizontal distance):");
            foreach (var item in polys.Select(p => (p, d: CollisionCache.DistanceXZ(p, s.X, s.Z))).OrderBy(x => x.d).Take(NearestCollision))
                Both($"  d {F(item.d)} {Describe(item.p)}");

            FileOnly("every polygon:");
            foreach (CollisionPoly p in polys)
            {
                FileOnly("  " + Describe(p));
                FileOnly("    edge normals " + string.Join(" | ", p.EdgeNormals.Select(V)) + "; e_num " + string.Join(" ", p.EdgeNums.Select(F)));
            }
        }

        private static string Describe(CollisionPoly p)
        {
            string face = p.HasFace
                ? $"attr 0x{p.Attr:X} myuka {p.Myuka} se {p.Se} basyo {p.Basyo} amap {p.Amap0}/{p.Amap1} tome {p.TomeIdx} enc {p.EncType}/{p.EncIdx} spc {p.Spc1}/{p.Spc2}"
                : "no face";
            return $"#{p.Index} mode2D {p.Mode2D} edges {p.NumEdges} n {V(p.Normal)} num {F(p.Num)} " +
                   $"min [{string.Join(" ", p.Min.Select(F))}] max [{string.Join(" ", p.Max.Select(F))}] block {p.BlockId} hit {p.HitFlag} " +
                   $"res {p.ResId} hoji {p.HojiId} npc {p.NpcId} elv {V(p.Elv)} | {face} | pts {string.Join(" | ", p.Points.Select(V))}";
        }

        private static string Histogram<T>(IReadOnlyCollection<T> items, Func<T, string> key)
        {
            return string.Join(", ", items.GroupBy(key).OrderByDescending(g => g.Count()).Take(30).Select(g => $"{g.Key} x{g.Count()}"));
        }

        // ---- auto-map ----

        private static void DumpAutoMap(PlayerSnapshot s)
        {
            Both($"gAmap_NowField {fldAutoMap.gAmap_NowField} NowArea {fldAutoMap.gAmap_NowArea} NowFloor {fldAutoMap.gAmap_NowFloor} " +
                 $"InFloor {fldAutoMap.gAmap_InFloor} Ofs {fldAutoMap.gAmap_OfsX} {fldAutoMap.gAmap_OfsZ} In {fldAutoMap.gAmap_InX} {fldAutoMap.gAmap_InZ} " +
                 $"AutoMapSeq {fldAutoMap.AutoMapSeq} mapIndex {fldAutoMap.mapIndex}");

            Il2CppReferenceArray<fldAmAreaData_t> areas = fldAutoMap.pfldAmArea;
            int n = areas == null ? 0 : Math.Min(fldAutoMap.nfldAmArea, areas.Length);
            Both($"nfldAmArea {fldAutoMap.nfldAmArea}, array {areas?.Length ?? -1}");
            var iconTypes = new Dictionary<uint, int>();
            for (int i = 0; i < n; i++)
            {
                fldAmAreaData_t area = areas[i];
                if (area == null)
                {
                    Both($"  area {i}: null");
                    continue;
                }
                Il2CppReferenceArray<fldAmSBlock_t> blocks = area.pSB;
                int nb = blocks == null ? 0 : (int)Math.Min(area.nSB, (uint)blocks.Length);
                Both($"  area {i} {Q(area.pName)} pos {Xyz(area.pPos)} nSB {area.nSB}");
                for (int j = 0; j < nb; j++)
                {
                    fldAmSBlock_t b = blocks[j];
                    if (b == null)
                        continue;
                    bool inside = s.Valid && b.pPos0 != null && b.pPos1 != null
                                  && Between(s.X, b.pPos0.x, b.pPos1.x) && Between(s.Z, b.pPos0.z, b.pPos1.z);
                    string line = $"    sub-block {j} {Q(b.pName)} node {b.nodeIdx} floor {b.floor} box {Xyz(b.pPos0)} .. {Xyz(b.pPos1)} icons {b.nIcon}{(inside ? " <- player inside (x, z)" : "")}";
                    if (inside)
                        Both(line);
                    else
                        FileOnly(line);

                    Il2CppReferenceArray<fldAmIcon_t> icons = b.pIcon;
                    int ni = icons == null ? 0 : (int)Math.Min(b.nIcon, (uint)icons.Length);
                    for (int k = 0; k < ni; k++)
                    {
                        fldAmIcon_t icon = icons[k];
                        if (icon == null)
                            continue;
                        iconTypes[icon.type] = iconTypes.TryGetValue(icon.type, out int c) ? c + 1 : 1;
                        FileOnly($"      icon {k} type {icon.type} pos {Xyz(icon.pPos)}");
                    }
                }
            }
            Both("icon types: " + string.Join(", ", iconTypes.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} x{kv.Value}")));
        }

        private static bool Between(float v, float a, float b) => v >= Math.Min(a, b) && v <= Math.Max(a, b);

        private static string Xyz(fldAmXYZ_t p) => p == null ? "null" : $"{F(p.x)} {F(p.y)} {F(p.z)}";

        // ---- NPCs and item boxes ----

        /// <summary>The points of interest and the walkability grid as a map (docs/PATHFINDING.md section 2.4).</summary>
        private static void DumpWalkGrid(PlayerSnapshot s)
        {
            List<Poi> pois = fldGlobal.fldGb == null ? new List<Poi>() : PointsOfInterest.Build(fldGlobal.fldGb);
            Both($"points of interest: {pois.Count}");
            foreach (Poi p in pois)
                Both($"  gEvnRng {p.Index} {p.Label} {Q(p.ResName)} at {F(p.X)} {F(p.Z)} d {F(p.Distance)}: {PointsOfInterest.Describe(s.X, s.Y, s.Z, p)}{(p.Reachable ? "" : ", not reachable from here")}");
            if (!WalkGrid.Ensure(s.FieldId, s.AreaId, s.X, s.Y, s.Z))
            {
                Both("walk grid: no floor here");
                return;
            }
            Both($"walk grid {WalkGrid.Width} x {WalkGrid.Height} (map in the file)");
            FileOnly(WalkGrid.AsciiMap(s.X, s.Y, s.Z, pois.Select(p => (p.X, p.Z, p.IsDoorLike ? 'D' : p.Kind == PoiKind.Person ? 'P' : 'B'))));
        }

        private static void DumpNpcs(PlayerSnapshot s)
        {
            Il2CppReferenceArray<fldNpc_tag> npcs = fld_Npc.fldNpc;
            int n = npcs == null ? 0 : Math.Min(fld_Npc.fldNpcNum, npcs.Length);
            Both($"fldNpcNum {fld_Npc.fldNpcNum}, array {npcs?.Length ?? -1}; gFldNowTalk {fld_Npc.gFldNowTalk} gFldNowTalkIdx {fld_Npc.gFldNowTalkIdx}");
            for (int i = 0; i < n; i++)
            {
                fldNpc_tag e = npcs[i];
                if (e == null)
                    continue;
                GameObject obj = e.fldObj;
                string objText = obj == null ? "none" : $"{Q(obj.name)} active {obj.activeInHierarchy} at {V(obj.transform.position)}";
                Both($"  npc {i} Type {e.Type} name {Q(e.name)} pos {F(e.x)} {F(e.y)} {F(e.z)} d {Dist(s, e.x, e.y, e.z)} rot {V(e.rot)} " +
                     $"hoji {e.hojiID} ItemBoxIdx {e.ItemBoxIdx} tType {e.tType} tCamera {e.tCamera} tId {e.tId1}/{e.tId2} model {e.npcmodel} csize {F(e.csize)} obj {objText}");
            }
        }

        private static void DumpBoxes(PlayerSnapshot s)
        {
            Il2CppReferenceArray<fldTakaraWork_tag> boxes = fld_Npc.gfldTakaraWork;
            int n = boxes == null ? 0 : Math.Min(fld_Npc.gfldTakaraNum, boxes.Length);
            Both($"gfldTakaraNum {fld_Npc.gfldTakaraNum}, array {boxes?.Length ?? -1}");
            for (int i = 0; i < n; i++)
            {
                fldTakaraWork_tag e = boxes[i];
                if (e == null)
                    continue;
                Both($"  box {i} TakaraIdx {e.TakaraIdx} type {e.type} pos {F(e.x)} {F(e.y)} {F(e.z)} d {Dist(s, e.x, e.y, e.z)} r {F(e.r)} " +
                     $"MotPhase {e.MotPhase} fldTakaraTblIdx {e.fldTakaraTblIdx}");
            }
        }

        /// <summary>
        /// The field's door warp table, fldWap.gFldWarpBuff.door_buff (doormovetbl_s), read as data (docs/class-index.md, "Door
        /// warp table"): per entry the source area (union01 = area + 1) and door volume name (union02), the warp kind (warptype; 0
        /// a plain warp), the destination field (union08, 0 = this field), area (union09 = area + 1, 0 = this area) and arrival
        /// point (warp_pos). The story flag (flag) is printed, not checked. Entries of the current area go to the log too.
        /// </summary>
        private static void DumpDoorTable(PlayerSnapshot s)
        {
            fldGlobalWork_t gb = fldGlobal.fldGb;
            fldwarp_s warp = fldWap.gFldWarpBuff;
            Il2CppReferenceArray<doormovetbl_s> table = warp?.door_buff;
            Both($"door_buff {(table == null ? "null" : table.Length.ToString())}, field {gb?.fieldID}, area {gb?.areaID} (union01 {gb?.areaID + 1})");
            if (table == null || gb == null)
                return;
            int used = 0;
            for (int i = 0; i < table.Length; i++)
            {
                doormovetbl_s e = table[i];
                if (e == null)
                    continue;
                string from = Chars(e.union02);
                if (e.movetype == 0 && from.Length == 0)
                    continue;
                used++;
                string line = $"  door {i} movetype {(int)e.movetype} flagmode {(int)e.flagmode} flag {e.flag} from area+1 {e.union01} {Q(from)} | " +
                              $"u03 {e.union03} u04 {e.union04} u05 {e.union05} u06 {Q(Chars(e.union06))} u07 {Q(Chars(e.union07))} | " +
                              $"warptype {(int)e.warptype} attr {(int)e.warp_attr} to field {e.union08} area+1 {e.union09} u10 {e.union10} at {Q(Chars(e.warp_pos))} | " +
                              $"cam {(int)e.warp_cammode}/{(int)e.warp_camtbl} {Q(Chars(e.warp_camname))} bgm {(int)e.warp_bgm} foot {(int)e.warp_foot} after {(int)e.after_flag} {Q(Chars(e.after_scr))}";
                if (e.union01 == gb.areaID + 1)
                    Both(line);
                else
                    FileOnly(line);
            }
            Both($"{used} entries used");
        }

        /// <summary>A game char array up to its first NUL.</summary>
        private static string Chars(Il2CppStructArray<char> a)
        {
            if (a == null)
                return "";
            var sb = new StringBuilder();
            for (int i = 0; i < a.Length && a[i] != '\0'; i++)
                sb.Append(a[i]);
            return sb.ToString();
        }

        private static string Dist(PlayerSnapshot s, float x, float y, float z)
        {
            if (!s.Valid)
                return "?";
            float dx = x - s.X, dz = z - s.Z;
            return $"{F((float)Math.Sqrt(dx * dx + dz * dz))} dy {F(y - s.Y)}";
        }

        // ---- event volumes ----

        private static void DumpEvents(PlayerSnapshot s)
        {
            Both($"gEvnRngNum {fldEveHit.gEvnRngNum} gEveNum {fldEveHit.gEveNum} gEveOff {fldEveHit.gEveOff} gProgramEveHitNum {fldEveHit.gProgramEveHitNum}; " +
                 $"gFldEveRngIdx {fldEveHit.gFldEveRngIdx} _N {fldEveHit.gFldEveRngIdx_N} _T {fldEveHit.gFldEveRngIdx_T} OldHitDoor {fldEveHit.OldHitDoor} OldHitInf {fldEveHit.OldHitInf}");

            Il2CppReferenceArray<fldResHead_t> ranges = fldEveHit.gEvnRng;
            Il2CppReferenceArray<putevebox_s> boxes = fldEveHit.gEvnRngBox;
            int n = ranges == null ? 0 : Math.Min(fldEveHit.gEvnRngNum, ranges.Length);
            var rows = new List<(float d, string text)>();
            for (int i = 0; i < n; i++)
            {
                fldResHead_t head = ranges[i];
                string box = boxes != null && i < boxes.Length ? DescribeBox(boxes[i]) : "no box";
                (float d, string text) = DescribeHead(s, head);
                string line = $"  gEvnRng {i} {text} | box {box}";
                rows.Add((d, line));
                FileOnly(line);
            }
            Both($"event ranges: {n}; the {NearestEvents} nearest:");
            foreach ((float d, string text) row in rows.OrderBy(r => r.d).Take(NearestEvents))
                Both(row.text);

            Il2CppReferenceArray<fldResHead_t> eves = fldEveHit.gEve;
            int ne = eves == null ? 0 : Math.Min(fldEveHit.gEveNum, eves.Length);
            rows.Clear();
            for (int i = 0; i < ne; i++)
            {
                (float d, string text) = DescribeHead(s, eves[i]);
                string line = $"  gEve {i} {text}";
                rows.Add((d, line));
                FileOnly(line);
            }
            Both($"events (gEve): {ne}; the {NearestEvents} nearest:");
            foreach ((float d, string text) row in rows.OrderBy(r => r.d).Take(NearestEvents))
                Both(row.text);

            Il2CppReferenceArray<fldProgramEveHit_tag> prog = fldEveHit.gProgramEveHit;
            int np = prog == null ? 0 : Math.Min(fldEveHit.gProgramEveHitNum, prog.Length);
            Both($"program event hits: {np}");
            for (int i = 0; i < np; i++)
            {
                fldProgramEveHit_tag e = prog[i];
                if (e == null)
                    continue;
                string ball = e.ball == null ? "none" : $"type {e.ball.Type} r {F(e.ball.r)}";
                (_, string text) = DescribeHead(s, e.ResHead);
                Both($"  program {i} pos {(e.sisei == null ? "null" : V(e.sisei.pos))} ball {ball} head {text}");
            }
        }

        private static (float d, string text) DescribeHead(PlayerSnapshot s, fldResHead_t h)
        {
            if (h == null)
                return (float.MaxValue, "null");

            float d = float.MaxValue;
            string pos = "null";
            fldResHeadPos_t sisei = h.sisei;
            if (sisei != null)
            {
                Vector4 p = sisei.pos;
                if (s.Valid)
                {
                    float dx = p.x - s.X, dz = p.z - s.Z;
                    d = (float)Math.Sqrt(dx * dx + dz * dz);
                }
                pos = $"{V(p)} d {(d == float.MaxValue ? "?" : F(d))} rot {V(sisei.rot)} scl {V(sisei.scl)}";
            }

            string area = h.areaname == null ? "null" : $"{h.areaname.num} {Q(h.areaname.name)}";
            string links = "none";
            fldResHeadLink_t link = h.resourceLink;
            if (link != null && link.data != null)
            {
                int nl = (int)Math.Min(link.num, (uint)link.data.Length);
                var parts = new List<string>();
                for (int k = 0; k < nl; k++)
                {
                    fldResHeadLinkData_t ld = link.data[k];
                    if (ld != null)
                        parts.Add($"{ld.mode}:{Q(ld.name)}:{ld.scl}");
                }
                links = $"{link.num} [{string.Join(", ", parts)}]";
            }

            fldResPut_t put = h.pPut;
            string putText = put == null ? "null" : $"Type {put.Type} EveIdx {put.EveIdx} Visible {put.Visible}";
            string text = $"id {h.resourceID} type {h.resourceType} name {Q(h.resourceName)} flag 0x{h.flag:X} pos {pos} " +
                          $"areaname {area} links {links} SBlockName {Q(h.SBlockName)} union {h.union} put {putText}";
            return (d, text);
        }

        private static string DescribeBox(putevebox_s b)
        {
            if (b == null)
                return "null";
            Il2CppStructArray<Vector4> vtx = b.vtx;
            string verts = vtx == null ? "null" : string.Join(" | ", Enumerable.Range(0, vtx.Length).Select(k => V(vtx[k])));
            return $"type {b.type} size {F(b.sizex)} {F(b.sizey)} {F(b.sizez)} TakaraIdx {b.TakaraIdx} npc hoji {b.npcHoji} hit {b.npcHit} idx {b.npcIdx} " +
                   $"mode2D {b.mode2D} facehit {b.facehit} serial {b.put_serialID} boxnml {V(b.boxnml)} vtx {verts}";
        }

        // ---- input and story flags ----

        private static void DumpInputAndFlags()
        {
            if (!NavProbe.Installed)
            {
                Both("probe hooks not installed (Diagnostics = false)");
                return;
            }

            Both($"padcheck pad indexes seen: mask 0x{NavProbe.PadIndexMask:X}");
            Both("padcheck actions that answered true (and FD_Subjectivity):");
            foreach (string line in NavProbe.PadcheckLines(onlyInteresting: true))
                Both("  " + line);
            FileOnly("padcheck, every action:");
            foreach (string line in NavProbe.PadcheckLines(onlyInteresting: false))
                FileOnly("  " + line);
            Both("analog channels asked (dds3PadManager.GetPadAnalog):");
            foreach (string line in AnalogInput.ChannelLines())
                Both("  " + line);

            DumpChecks("evtBitCheck", NavProbe.EvtBits, annotate: true);
            DumpChecks("cfgGetBit", NavProbe.CfgBits, annotate: false);
        }

        private static void DumpChecks(string name, CheckRecorder recorder, bool annotate)
        {
            Dictionary<int, CheckStat> baseline = recorder.BaselineCopy();
            var inCalc = baseline.Where(kv => kv.Value.InCalc > 0).OrderBy(kv => kv.Key).ToList();
            Both($"{name}: {baseline.Count} distinct values checked outside V captures; {inCalc.Count} of them inside fldPlayerCalc:");
            foreach (KeyValuePair<int, CheckStat> kv in inCalc)
                Both($"  {kv.Key}{(annotate ? NavProbe.NewGame2Name(kv.Key) : "")}: {kv.Value}");
            FileOnly($"{name}, every value:");
            foreach (KeyValuePair<int, CheckStat> kv in baseline.OrderBy(kv => kv.Key))
                FileOnly($"  {kv.Key.ToString(CultureInfo.InvariantCulture)}{(annotate ? NavProbe.NewGame2Name(kv.Key) : "")}: {kv.Value}");
        }
    }
}
