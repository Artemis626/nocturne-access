using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2Cppcamp_H;
using Il2Cppnewdata_H;
using Il2CppTMPro;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// The status shown beside a name in the party panel (party_statusNN / stock_statusNN), said after the name:
    /// "Dís, Mute, 3 of 11" (0.0.73).
    ///
    /// The game draws the status only as an icon (cmpPanel.cmpDrawDevilInfo → cmpDrawBadStatus), so there is no text on
    /// screen. Read from game data (docs/class-index.md, "Party panel status"):
    /// - the row's unit: dds3GlobalWork.DDS3_GBWK.unitwork[cmpInit.CMP_GBWK.DrawList.StockIdx[row]], party rows first, then
    ///   the stock rows (cmpDrawPartyPanel / cmpDrawStockPanel walk the same list); its datUnitWork_s.badstatus.
    /// - the bit order: nbDebugProcess.GetBadString takes the lowest of 12 bits; the game's status icon objects are listed
    ///   in the string literals as steff_shock, freeze, sleep, panic, bind, close, poison, charm, palyze, fly, stone, dead.
    ///   Taken as bits 0 to 11 (unverified in game: every value is logged).
    /// - the words: the game's own, from its cure help (Francesco's choice, 12 Oct 2026): DATITEMHELP_L0015 "Cures
    ///   Bind/Sleep/Panic", L0016 Poison, L0017 Stun, L0018 Charm, L0019 Mute, L0020 Stone (item N's help is L00NN); Fly from
    ///   DATSKILLHELP_L0201 "Turns one foe into a Fly.". The game has no word for dead: "Dead" is the mod's (to confirm with
    ///   Francesco). Shock and Freeze have no game word: the mod says "Shock" and "Freeze" (Francesco, 4 Nov 2026, 0.0.152).
    /// </summary>
    internal static class PartyStatus
    {
        private const string Tag = "[Menu]";
        private const int DeadBit = 11;

        // Bit -> (localization id, the word's place in its "Cures A/B/C" list, or -1 for the last word of the line).
        private static readonly (int Bit, string Id, int Index)[] Sources =
        {
            (2, "DATITEMHELP_L0015", 1),    // sleep: "Cures Bind/Sleep/Panic"
            (3, "DATITEMHELP_L0015", 2),    // panic
            (4, "DATITEMHELP_L0015", 0),    // bind
            (5, "DATITEMHELP_L0019", 0),    // close: "Cures Mute"
            (6, "DATITEMHELP_L0016", 0),    // poison
            (7, "DATITEMHELP_L0018", 0),    // charm
            (8, "DATITEMHELP_L0017", 0),    // palyze: "Cures Stun"
            (9, "DATSKILLHELP_L0201", -1),  // fly: "Turns one foe into a Fly."
            (10, "DATITEMHELP_L0020", 0),   // stone
        };

        private static readonly Regex Slot = new Regex(@"^(?:party_status(?<party>\d\d)|stock_status(?<stock>\d\d))$", RegexOptions.Compiled);
        private static Dictionary<int, string> _words;
        private static bool _failed;
        private static readonly Dictionary<string, string> Logged = new Dictionary<string, string>();

        /// <summary>MenuSpec.RowDetail for the party pick: the status words of the row's unit, or "".</summary>
        internal static string RowDetail(TMP_Text component, string name)
        {
            if (_failed || component == null)
                return "";
            try
            {
                return Read(component, name);
            }
            catch (Exception ex)
            {
                _failed = true;
                Log.Error($"{Tag} Party panel status unreadable, names only from now: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// MenuSpec.Bystander for the party pick: the row is the unit using the chosen skill. Its name keeps the highlight
        /// through the whole target pick (13 Oct 2026, 13:52:45: Dís casting Diarama, "All 2: Dís, Taraka" on every move).
        /// </summary>
        internal static bool IsSkillUser(TMP_Text component)
        {
            datUnitWork_t user = CampListReader.SkillUser;
            if (_failed || user == null || component == null)
                return false;
            try
            {
                datUnitWork_t unit = UnitOf(component, out _, out _);
                return unit != null && unit.Pointer == user.Pointer;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The row's unit: DDS3_GBWK.unitwork[CMP_GBWK.DrawList.StockIdx[row]], party rows first, or null.</summary>
        private static datUnitWork_t UnitOf(TMP_Text component, out int row, out int index)
        {
            row = index = -1;
            var parent = component.transform.parent;
            Match m = parent == null ? Match.Empty : Slot.Match(parent.name);
            if (!m.Success)
                return null;
            cmpLocalStock_t list = cmpInit.CMP_GBWK?.DrawList;
            var units = dds3GlobalWork.DDS3_GBWK?.unitwork;
            if (list == null || list.StockIdx == null || units == null)
                return null;
            row = m.Groups["party"].Success
                ? int.Parse(m.Groups["party"].Value) - 1
                : list.PartyCnt + int.Parse(m.Groups["stock"].Value) - 1;
            if (row < 0 || row >= list.PartyCnt + list.StockCnt || row >= list.StockIdx.Length)
                return null;
            index = list.StockIdx[row];
            if (index < 0 || index >= units.Length)
                return null;
            return units[index];
        }

        private static string Read(TMP_Text component, string name)
        {
            datUnitWork_t unit = UnitOf(component, out int row, out int index);
            if (unit == null)
                return "";
            var parent = component.transform.parent;
            int bad = unit.badstatus;

            var words = new List<string>();
            if ((bad & (1 << DeadBit)) != 0 || unit.hp == 0)
                words.Add("Dead");
            Dictionary<int, string> table = Words();
            for (int bit = 0; bit < DeadBit; bit++)
                if ((bad & (1 << bit)) != 0 && table.TryGetValue(bit, out string w) && w.Length > 0)
                    words.Add(w);
            string said = string.Join(", ", words);

            // The row last read is watched for a while (Tick): a cure used on it is said (0.0.135).
            if (_watch == null || _watch.Pointer != unit.Pointer)
                _watchBad = bad;
            _watch = unit;
            _watchName = name;
            _watchAt = Clock.ElapsedMilliseconds;

            // Every value once per row and change, to check the bit order in game.
            string key = $"{parent.name}:{name}";
            string seen = $"0x{bad:X} hp {unit.hp} flag 0x{unit.flag:X}";
            if (!Logged.TryGetValue(key, out string last) || last != seen)
            {
                Logged[key] = seen;
                if (bad != 0 || unit.hp == 0 || last != null)
                    Log.Info($"{Tag} Party panel status: {name} (row {row}, unit {index}, id {unit.id}) badstatus {seen} -> \"{said}\".");
            }
            return said;
        }

        // ---- A cure used from the menus (0.0.135) ----
        // Outside battle the game says nothing when an item or skill cures a status: the icon goes and a sound plays (Francesco,
        // 30 Oct 2026, 13:42: Dis-Stun on Art, "Art, Stun", then silence). The unit of the row last read in the party pick is watched
        // for WatchMs; a status it loses is said in Francesco's battle words: "Art: Stun ended."
        private static datUnitWork_t _watch;
        private static string _watchName = "";
        private static int _watchBad;
        private static long _watchAt;
        private const long WatchMs = 15000;
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();

        internal static void Tick()
        {
            if (_watch == null)
                return;
            if (Clock.ElapsedMilliseconds - _watchAt > WatchMs || Battle.BattleReader.Active)
            {
                _watch = null;
                return;
            }
            int bad;
            try
            {
                bad = _watch.badstatus;
            }
            catch
            {
                _watch = null;
                return;
            }
            if (bad == _watchBad)
                return;
            List<string> gone = WordsFor(_watchBad & ~bad);
            Log.Info($"{Tag} Watched unit {_watchName}: badstatus 0x{_watchBad:X} -> 0x{bad:X}, ended [{string.Join(", ", gone)}].");
            _watchBad = bad;
            if (gone.Count > 0)
                SpeechManager.Say($"{_watchName}: {string.Join(", ", gone)} ended.", interrupt: false);
        }

        /// <summary>
        /// The game's words for the statuses in badstatus, Dead left out (0.0.133, battle). Bits 0 and 1 (shock, freeze) are the mod's
        /// words "Shock" and "Freeze" (0.0.152). Bit order confirmed in game: 0x20 Mute (26 Oct 2026, 20:58: Art muted, Dis-Mute chosen),
        /// 0x800 Dead (Setanta at 0 HP).
        /// </summary>
        internal static List<string> WordsFor(int bad)
        {
            var list = new List<string>();
            Dictionary<int, string> table = Words();
            for (int bit = 0; bit < DeadBit; bit++)
                if ((bad & (1 << bit)) != 0 && table.TryGetValue(bit, out string w) && w.Length > 0)
                    list.Add(w);
            return list;
        }

        private static Dictionary<int, string> Words()
        {
            if (_words != null)
                return _words;
            // Shock and Freeze (bits 0, 1): the game draws them only as icons (steff_shock, freeze) and has no word for them; the
            // mod's own words, approved by Francesco, 4 Nov 2026 (Elec hits left Art, Chimera, Quetzalcoatl shocked unsaid).
            _words = new Dictionary<int, string> { [0] = "Shock", [1] = "Freeze" };
            foreach (var (bit, id, place) in Sources)
            {
                string line;
                try
                {
                    line = TextUtil.Clean(Localize.GetLocalizeText($"<{id}>") ?? "");
                }
                catch
                {
                    line = "";
                }
                string word = Pick(line, place);
                _words[bit] = word;
                Log.Info($"{Tag} Status word for bit {bit}: \"{word}\" from {id} \"{line}\".");
            }
            return _words;
        }

        /// <summary>"Cures Bind/Sleep/Panic" place 1 → "Sleep"; place -1 → the line's last word without its full stop.</summary>
        private static string Pick(string line, int place)
        {
            if (line.Length == 0 || line.StartsWith("DAT", StringComparison.Ordinal))
                return "";
            string[] tokens = line.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (place < 0)
                return tokens.Length == 0 ? "" : tokens[tokens.Length - 1].TrimEnd('.');
            if (tokens.Length < 2 || tokens[0] != "Cures")
                return "";
            string[] names = tokens[1].Split('/');
            return place < names.Length ? names[place] : "";
        }

        /// <summary>
        /// F3 outside battle (0.0.204, Francesco's option C: status at a glance): each party member with HP, MP and statuses, the
        /// protagonist first. The party is the game's own: datCalc.datPartyDamageFld (the field's poison steps) hurts the
        /// dds3GlobalWork.DDS3_GBWK.unitwork entries whose flag has 0x2 (read offline, 27 Nov 2026); 0x1 marks an owned unit.
        /// </summary>
        internal static void SpeakField()
        {
            if (!Navigation.WhereAmI.GameLoaded)
            {
                SpeechManager.Say("No game loaded.", interrupt: true, force: true);
                return;
            }
            var parts = new List<string>();
            try
            {
                var units = dds3GlobalWork.DDS3_GBWK?.unitwork;
                for (int k = 0; units != null && k < units.Length; k++)
                {
                    datUnitWork_t w = units[k];
                    if (w == null || w.maxhp == 0 || (w.flag & 0x1) == 0 || (w.flag & 0x2) == 0)
                        continue;
                    string name = k == 0 ? TextUtil.Clean(frName.frGetCNameString(0) ?? "") : TextUtil.Clean(datDevilName.Get(w.id) ?? "");
                    if (name.Length == 0)
                        name = k == 0 ? "You" : "Ally";
                    var words = new List<string>();
                    if ((w.badstatus & (1 << DeadBit)) != 0 || w.hp == 0)
                        words.Add("Dead");
                    words.AddRange(WordsFor(w.badstatus));
                    string status = words.Count > 0 ? ", " + string.Join(", ", words) : "";
                    parts.Add($"{name}: HP {w.hp} of {w.maxhp}, MP {w.mp} of {w.maxmp}{status}.");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"{Tag} Field party status: {ex.Message}");
            }
            string text = parts.Count > 0 ? string.Join(" ", parts) : "No party data.";
            Log.Info($"{Tag} Field party status: {text}");
            SpeechManager.Say(text, interrupt: true, force: true);
        }
    }
}
