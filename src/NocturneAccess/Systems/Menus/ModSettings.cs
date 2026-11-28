using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using NocturneAccess.Core;
using NocturneAccess.Input;
using NocturneAccess.Speech;
using NocturneAccess.Systems.Battle;
using NocturneAccess.Systems.Navigation;
using UnityEngine;
using UnityEngine.UI;
using UnityInput = UnityEngine.Input;

namespace NocturneAccess.Systems.Menus
{
    /// <summary>
    /// The mod settings window (0.0.194; Francesco, 25 Nov 2026): a small visible window with spoken rows, opened with Shift+F
    /// (F is the game's own menu key) only in the field, on the overworld and in battle, never inside a game menu. 0.0.194 tested
    /// two settings (verified 25 Nov 15:07, "clean and simple"); 0.0.195 groups the player settings under categories (Francesco).
    /// Left out on purpose, file only: content changes (ChaseOff, GeisGift, PipeNoDamage: Francesco, the Raidou chase), switches that
    /// could silence the mod or need a restart (Read dialogue, menus, battles; pad injection; first person), development and
    /// debug entries, the speech backend and the keys.
    ///
    /// Drawing: the game's build has IMGUI stripped (GUI.Label and GUI.Box are missing from its UnityEngine.IMGUIModule), so the
    /// window is a uGUI Canvas with an Image panel and one TextMeshProUGUI in the game's own font.
    ///
    /// Input: while the window is open the game sees no key. The game's keyboard reaches it as actions
    /// (dds3ConfigKeyBoardSteam.GetKeyBoardPadMap calls SteamInputAssign.keycheck(SIActionName, SIPressType); read offline,
    /// 25 Nov 2026) and as keys (SteamInputUtil.get_digital_button calls InputUtil.GetKey(ControllerID, KeyID, int)): postfixes
    /// answer false on all of them while the window is open, and until every key is released after it closes. While Shift is held
    /// the game never sees its menu action (FD_CmdMenu, PZL_Menu), so Shift+F opens only this window.
    /// </summary>
    internal static class ModSettings
    {
        internal const string RootName = "NocturneAccessSettings";
        private const string Tag = "[Settings]";
        private const long ReleaseHoldMaxMs = 1500;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private const int VisibleRows = 10;

        private sealed class Item
        {
            public MelonPreferences_Entry<bool> Entry;        // an on / off setting
            public MelonPreferences_Entry<string> KeyEntry;   // a key (0.0.213), named by KeyName
            public string KeyName = "";
            public bool ResetKeys;                            // the "Reset all keys" row
            public string Note = "";          // said after a change ("Takes effect at the next start.")
            public Action<bool> Changed;      // what the game needs besides the new value (routes: cancel a journey)
            public string Label => ResetKeys ? "Reset all keys" : KeyEntry != null ? KeyName
                : string.IsNullOrEmpty(Entry.DisplayName) ? Entry.Identifier : Entry.DisplayName;
            public string Value => ResetKeys ? "" : KeyEntry != null ? Hotkeys.Spoken(KeyEntry.Value) : Entry.Value ? "On" : "Off";
            public string Line => Value.Length > 0 ? $"{Label}, {Value}" : Label;
        }

        private sealed class Category
        {
            public string Name;
            public readonly List<Item> Items = new List<Item>();
            public int Row;
        }

        private static readonly List<Category> Categories = new List<Category>();
        private static bool _open;
        private static bool _releaseHold;
        private static long _closedMs;
        private static int _cat;
        private static bool _inCategory;
        private static GameObject _root;
        private static TextMeshProUGUI _text;
        private static RectTransform _panel;
        private static int _shiftFrame = -1;
        private static bool _shiftHeld;

        internal static bool IsOpen => _open;

        /// <summary>The game sees no key: the window is open, or a key pressed in it is still held.</summary>
        private static bool Blocking => _open || _releaseHold;

        private static Category Cat(string name, params Item[] items)
        {
            var c = new Category { Name = name };
            c.Items.AddRange(items);
            Categories.Add(c);
            return c;
        }

        private static Item Bool(MelonPreferences_Entry<bool> entry, string note = "", Action<bool> changed = null)
            => new Item { Entry = entry, Note = note, Changed = changed };

        internal static void Install()
        {
            Categories.Clear();
            Cat("Speech",
                Bool(Config.SpeakerEveryPage),
                Bool(Config.BrailleOutput));
            Cat("Battle",
                Bool(Config.SpeakHpChanges),
                Bool(Config.SpeakHpLeft));
            Cat("Navigation",
                Bool(Config.BlobberOnAtStart, changed: on => BlobberMode.Set(on)),
                Bool(Config.PipeAutopilot),
                Bool(Config.DoorKinds),
                Bool(Config.PoiMapOrder),
                Bool(Config.AreaRoutes, changed: on => { if (!on) AutoWalk.CancelJourney("routes through other areas turned off"); }),
                Bool(Config.WayOnEnabled, changed: on => { if (!on) WayOn.Forget(); }));
            Cat("Sounds",
                Bool(Config.WallBumpSound),
                Bool(Config.DoorSound),
                Bool(Config.WayOnSound),
                Bool(Config.AlarmSound),
                Bool(Config.SecretSound),
                Bool(Config.CameraSound),
                Bool(Config.ChaseSound));
            // 0.0.213: the player's keys, remapped here (debug keys stay in the cfg file only).
            Cat("Keys",
                KeyRow(Config.KeyHelp, "Key help"),
                KeyRow(Config.KeyRepeat, "Repeat"),
                KeyRow(Config.KeyBattleStatus, "Party status"),
                KeyRow(Config.KeyBattleEnemies, "Enemies"),
                KeyRow(Config.KeyBattleLog, "Battle log"),
                KeyRow(Config.KeyCentre, "Centre on the square"),
                KeyRow(Config.KeyPoiNext, "Next point"),
                KeyRow(Config.KeyPoiPrevious, "Previous point"),
                KeyRow(Config.KeyPoiFilterNext, "Next filter"),
                KeyRow(Config.KeyPoiFilterPrevious, "Previous filter"),
                KeyRow(Config.KeyPoiWalk, "Walk to the point"),
                KeyRow(Config.KeyPoiGuide, "Directions to the point"),
                KeyRow(Config.KeyWayOn, "Way on"),
                KeyRow(Config.KeyWhereAmI, "Where am I"),
                KeyRow(Config.KeyBlobber, "Blobber mode"),
                KeyRow(Config.KeyBattleVerbosity, "HP after actions"),
                KeyRow(Config.KeyAreaRoutes, "Routes through other areas"),
                KeyRow(Config.KeyModSettings, "Mod settings"),
                new Item { ResetKeys = true });

            var action = AccessTools.Method(typeof(ModSettings), nameof(AfterAction));
            var any = AccessTools.Method(typeof(ModSettings), nameof(AfterAnyKey));
            Patcher.Postfix("Mod settings", typeof(SteamInputAssign), nameof(SteamInputAssign.keycheck),
                new[] { typeof(SIActionName), typeof(SIPressType) }, action);
            Patcher.Postfix("Mod settings", typeof(SteamInputAssign), nameof(SteamInputAssign.keycheckOther),
                new[] { typeof(int), typeof(SIPressType) }, any);
            Patcher.Postfix("Mod settings", typeof(SteamInputAssign), nameof(SteamInputAssign.keycheckCmn),
                new[] { typeof(SteamInputAssign.CmnKey), typeof(SIPressType) }, any);
            Patcher.Postfix("Mod settings", typeof(InputUtil), nameof(InputUtil.GetKey),
                new[] { typeof(InputAssign.ControllerID), typeof(InputAssign.KeyID), typeof(int) }, any);
            Patcher.Postfix("Mod settings", typeof(InputUtil), nameof(InputUtil.GetPadKey),
                new[] { typeof(InputAssign.ControllerID), typeof(InputAssign.AssignCode) }, any);
        }

        // ---- Opening and closing ----

        /// <summary>Shift+F (Hotkeys): opens the window where allowed; closing is handled in Update.</summary>
        internal static void Toggle()
        {
            if (_open)
            {
                Close();
                return;
            }
            if (!FieldState.IsExploring && !BattleReader.Active)
            {
                Log.Info($"{Tag} Not opened: not in the field or in battle.");
                SpeechManager.Say("Mod settings open only in the field or in battle.", interrupt: true, force: true);
                return;
            }
            Open();
        }

        private static void Open()
        {
            AutoWalk.Stop("mod settings opened", null);
            PadInput.ReleaseAll();
            _open = true;
            _releaseHold = false;
            _inCategory = false;
            try
            {
                Build();
                Redraw();
            }
            catch (Exception ex)
            {
                Log.Error($"{Tag} Window not drawn: {ex}");
            }
            Log.Info($"{Tag} Opened.");
            SpeechManager.Say($"Mod settings. W and S: move. Enter: open. C: back. {RowLine()}", interrupt: true, force: true);
        }

        private static void Close()
        {
            _open = false;
            _releaseHold = true;
            _closedMs = Clock.ElapsedMilliseconds;
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            _root = null;
            _text = null;
            _panel = null;
            Log.Info($"{Tag} Closed.");
            SpeechManager.Say("Mod settings closed.", interrupt: true, force: true);
        }

        // ---- Keys, each frame from Hotkeys.Update ----

        /// <summary>True while the window is open: it takes every key and the mod's other keys do nothing.</summary>
        internal static bool Update()
        {
            if (!_open)
            {
                if (_releaseHold && (!UnityInput.anyKey || Clock.ElapsedMilliseconds - _closedMs > ReleaseHoldMaxMs))
                    _releaseHold = false;
                return false;
            }

            if (CaptureTick())
                return true;
            if (Hotkeys.IsDown("mod settings"))
            {
                Close();
                return true;
            }
            if (Down(KeyCode.C) || Down(KeyCode.Escape) || Down(KeyCode.Backspace))
            {
                if (_inCategory)
                    Back();
                else
                    Close();
                return true;
            }
            if (Down(KeyCode.W) || Down(KeyCode.UpArrow))
                Move(-1);
            else if (Down(KeyCode.S) || Down(KeyCode.DownArrow))
                Move(1);
            else if (Down(KeyCode.Return) || Down(KeyCode.KeypadEnter) || Down(KeyCode.Space)
                     || Down(KeyCode.A) || Down(KeyCode.D) || Down(KeyCode.LeftArrow) || Down(KeyCode.RightArrow))
            {
                if (_inCategory)
                    Change();
                else
                    Enter();
            }
            else if (Hotkeys.IsDown("repeat"))
                SpeechManager.Say(RowLine(), interrupt: true, force: true);
            else if (Hotkeys.IsDown("key help"))
                SpeechManager.Say(_inCategory
                    ? $"W and S: move. A, D or Enter: change. C: back to the categories. {Hotkeys.KeyText("mod settings")}: close. {Hotkeys.KeyText("repeat")}: repeat."
                    : $"W and S: move. Enter: open a category. C or {Hotkeys.KeyText("mod settings")}: close. {Hotkeys.KeyText("repeat")}: repeat.", interrupt: true, force: true);
            return true;
        }

        private static bool Down(KeyCode key) => UnityInput.GetKeyDown(key);

        private static void Move(int step)
        {
            int count = _inCategory ? Categories[_cat].Items.Count : Categories.Count;
            int at = _inCategory ? Categories[_cat].Row : _cat;
            int next = at + step;
            if (next >= 0 && next < count)
            {
                // No wrap: at an edge the same row is said again, so the player hears where the list ends.
                if (_inCategory)
                    Categories[_cat].Row = next;
                else
                    _cat = next;
                Redraw();
            }
            SpeechManager.Say(RowLine(), interrupt: true, force: true);
        }

        private static void Enter()
        {
            _inCategory = true;
            Redraw();
            SpeechManager.Say($"{Categories[_cat].Name}. {RowLine()}", interrupt: true, force: true);
        }

        private static void Back()
        {
            _inCategory = false;
            Redraw();
            SpeechManager.Say(RowLine(), interrupt: true, force: true);
        }

        private static void Change()
        {
            Category c = Categories[_cat];
            Item item = c.Items[c.Row];
            if (item.ResetKeys)
            {
                AskReset();
                return;
            }
            if (item.KeyEntry != null)
            {
                StartCapture(item);
                return;
            }
            bool on = !item.Entry.Value;
            item.Entry.Value = on;
            Config.Save(item.Entry);
            try
            {
                item.Changed?.Invoke(on);
            }
            catch (Exception ex)
            {
                Log.Error($"{Tag} {item.Entry.Identifier}: {ex.Message}");
            }
            Log.Info($"{Tag} {item.Entry.Identifier} = {on}.");
            Redraw();
            string note = item.Note.Length > 0 ? " " + item.Note : "";
            SpeechManager.Say($"{item.Label}, {item.Value}.{note}", interrupt: true, force: true);
        }

        private static string RowLine()
        {
            if (!_inCategory)
            {
                Category c = Categories[_cat];
                return $"{c.Name}, {c.Items.Count} settings, {_cat + 1} of {Categories.Count}.";
            }
            Category cat = Categories[_cat];
            Item item = cat.Items[cat.Row];
            return $"{item.Line}, {cat.Row + 1} of {cat.Items.Count}.";
        }

        // ---- Keys category (0.0.213, docs/KEYS-PLAN.md section 4): remapping the mod's keys in the window ----

        /// <summary>Letters and keys the game binds (docs/KEYS.md "Game keyboard bindings"): never a mod key, except Shift+F for this window.</summary>
        private static readonly HashSet<KeyCode> GameKeys = new HashSet<KeyCode>
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Q, KeyCode.E, KeyCode.C, KeyCode.F, KeyCode.V, KeyCode.M,
            KeyCode.H, KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Tab, KeyCode.Escape, KeyCode.Equals, KeyCode.Plus,
            KeyCode.KeypadPlus,
        };

        /// <summary>The window's own keys (fixed, so the window always works).</summary>
        private static readonly HashSet<KeyCode> WindowKeys = new HashSet<KeyCode>
        {
            KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space, KeyCode.Backspace,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
        };

        /// <summary>Keys other software uses (docs/KEYS.md "Reserved keys"): Graphics Configurator F9 to F11, Steam F12, NocturneModernController P.</summary>
        private static readonly HashSet<KeyCode> OtherSoftwareKeys = new HashSet<KeyCode>
        {
            KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12, KeyCode.P,
        };

        /// <summary>Every key a press is looked for in while waiting for a new key; modifiers alone never count.</summary>
        private static readonly KeyCode[] CaptureKeys = BuildCaptureKeys();

        private static KeyCode[] BuildCaptureKeys()
        {
            var list = new List<KeyCode>();
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++) list.Add(k);
            for (KeyCode k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++) list.Add(k);
            for (KeyCode k = KeyCode.Keypad0; k <= KeyCode.Keypad9; k++) list.Add(k);
            for (KeyCode k = KeyCode.F1; k <= KeyCode.F12; k++) list.Add(k);
            list.AddRange(new[]
            {
                KeyCode.Insert, KeyCode.Delete, KeyCode.Home, KeyCode.End, KeyCode.PageUp, KeyCode.PageDown,
                KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.Tab, KeyCode.Return,
                KeyCode.KeypadEnter, KeyCode.Space, KeyCode.Backspace, KeyCode.Minus, KeyCode.Equals, KeyCode.Plus,
                KeyCode.KeypadPlus, KeyCode.KeypadMinus, KeyCode.KeypadMultiply, KeyCode.KeypadDivide, KeyCode.KeypadPeriod,
                KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Semicolon, KeyCode.Quote, KeyCode.Comma, KeyCode.Period,
                KeyCode.Slash, KeyCode.Backslash, KeyCode.BackQuote,
            });
            return list.ToArray();
        }

        private static Item _capture;                 // the key row waiting for a new key
        private static string _pending = "";          // a new key waiting for Enter (swap) or a second press (the settings key)
        private static Item _swapWith;                // the row that has that key already
        private static bool _confirmReset;

        private static Item KeyRow(MelonPreferences_Entry<string> entry, string name) => new Item { KeyEntry = entry, KeyName = name };

        private static IEnumerable<Item> KeyItems()
        {
            foreach (Category c in Categories)
                foreach (Item i in c.Items)
                    if (i.KeyEntry != null)
                        yield return i;
        }

        private static IEnumerable<MelonPreferences_Entry<string>> DebugKeyEntries()
            => new[] { Config.KeyDumpUi, Config.KeyToggleProbe, Config.KeyNavDump, Config.KeyBattleDump };

        private static void StartCapture(Item item)
        {
            _capture = item;
            _pending = "";
            _swapWith = null;
            Log.Info($"{Tag} Waiting for a new key for {item.KeyEntry.Identifier} (now {item.KeyEntry.Value}).");
            SpeechManager.Say($"Press the new key for {item.KeyName}. Escape cancels.", interrupt: true, force: true);
        }

        private static void EndCapture()
        {
            _capture = null;
            _pending = "";
            _swapWith = null;
            _confirmReset = false;
            Redraw();
        }

        /// <summary>While a key row or the reset waits: true, and every key goes here.</summary>
        private static bool CaptureTick()
        {
            if (_confirmReset)
            {
                if (Down(KeyCode.Return) || Down(KeyCode.KeypadEnter))
                    ResetAllKeys();
                else if (Down(KeyCode.Escape) || Down(KeyCode.C) || Down(KeyCode.Backspace))
                {
                    EndCapture();
                    SpeechManager.Say("Cancelled.", interrupt: true, force: true);
                }
                return true;
            }
            if (_capture == null)
                return false;

            if (Down(KeyCode.Escape))
            {
                Item was = _capture;
                EndCapture();
                SpeechManager.Say($"Cancelled. {was.KeyName}, {was.Value}.", interrupt: true, force: true);
                return true;
            }
            if (_swapWith != null)
            {
                if (Down(KeyCode.Return) || Down(KeyCode.KeypadEnter))
                {
                    string old = Hotkeys.Canonical(_capture.KeyEntry.Value);
                    Item other = _swapWith;
                    other.KeyEntry.Value = old.Length > 0 ? old : "None";
                    Save(other.KeyEntry);
                    Apply(_capture, _pending, $" {other.KeyName} is now {other.Value}.");
                }
                return true;
            }

            KeyCode key = KeyCode.None;
            foreach (KeyCode k in CaptureKeys)
            {
                if (Down(k))
                {
                    key = k;
                    break;
                }
            }
            if (key == KeyCode.None)
                return true;
            bool shift = UnityInput.GetKey(KeyCode.LeftShift) || UnityInput.GetKey(KeyCode.RightShift);
            bool ctrl = UnityInput.GetKey(KeyCode.LeftControl) || UnityInput.GetKey(KeyCode.RightControl);
            bool alt = UnityInput.GetKey(KeyCode.LeftAlt) || UnityInput.GetKey(KeyCode.RightAlt);
            string text = (ctrl ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "") + key;
            string said = Hotkeys.Spoken(text);
            bool settingsRow = _capture.KeyEntry == Config.KeyModSettings;

            if (_pending.Length > 0)
            {
                // The settings key: the same key twice, so a slip cannot lock the window away.
                if (text == _pending)
                    Apply(_capture, _pending, "");
                else
                {
                    Item was = _capture;
                    EndCapture();
                    SpeechManager.Say($"Not the same key. Cancelled. {was.KeyName}, {was.Value}.", interrupt: true, force: true);
                }
                return true;
            }

            string refusal = null;
            if (GameKeys.Contains(key) && !(settingsRow && text == "Shift+F"))
                refusal = $"{said} is a game key.";
            else if (WindowKeys.Contains(key))
                refusal = $"{said} is used by this window.";
            else if (OtherSoftwareKeys.Contains(key))
                refusal = $"{said} is used by other software.";
            else if (DebugKeyEntries().Any(e => Hotkeys.Canonical(e.Value) == text))
                refusal = $"{said} is a debug key.";
            if (refusal != null)
            {
                Log.Info($"{Tag} {text} refused for {_capture.KeyEntry.Identifier}: {refusal}");
                SpeechManager.Say($"{refusal} Press another key, or Escape.", interrupt: true, force: true);
                return true;
            }
            if (Hotkeys.Canonical(_capture.KeyEntry.Value) == text)
            {
                Item was = _capture;
                EndCapture();
                SpeechManager.Say($"Unchanged. {was.KeyName}, {was.Value}.", interrupt: true, force: true);
                return true;
            }
            Item owner = KeyItems().FirstOrDefault(i => i != _capture && Hotkeys.Canonical(i.KeyEntry.Value) == text);
            if (owner != null)
            {
                _swapWith = owner;
                _pending = text;
                SpeechManager.Say($"{said} is used by {owner.KeyName}. Enter swaps them, Escape cancels.", interrupt: true, force: true);
                return true;
            }
            if (settingsRow)
            {
                _pending = text;
                SpeechManager.Say($"Press {said} again to confirm.", interrupt: true, force: true);
                return true;
            }
            Apply(_capture, text, "");
            return true;
        }

        private static void Apply(Item item, string text, string extra)
        {
            item.KeyEntry.Value = text;
            Save(item.KeyEntry);
            Hotkeys.Init();
            Log.Info($"{Tag} {item.KeyEntry.Identifier} = {text}.");
            EndCapture();
            SpeechManager.Say($"{item.KeyName}, {item.Value}.{extra}", interrupt: true, force: true);
        }

        private static void Save(MelonPreferences_Entry<string> entry) => Config.Save(entry);

        private static void AskReset()
        {
            _confirmReset = true;
            SpeechManager.Say("Press Enter again to set every key back to its default. Escape cancels.", interrupt: true, force: true);
        }

        private static void ResetAllKeys()
        {
            foreach (MelonPreferences_Entry<string> e in Config.KeyEntries())
                e.ResetToDefault();
            Save(Config.KeyHelp);
            Hotkeys.Init();
            Log.Info($"{Tag} Every key reset to its default.");
            EndCapture();
            SpeechManager.Say("All keys reset.", interrupt: true, force: true);
        }

        // ---- Drawing ----

        private static void Build()
        {
            if (_root != null)
                return;
            _root = new GameObject(RootName);
            UnityEngine.Object.DontDestroyOnLoad(_root);
            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var panelGo = new GameObject("panel");
            panelGo.transform.SetParent(_root.transform, false);
            Image image = panelGo.AddComponent<Image>();
            image.color = new Color(0.02f, 0.02f, 0.08f, 0.9f);
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);

            var textGo = new GameObject("text");
            textGo.transform.SetParent(panelGo.transform, false);
            _text = textGo.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GameFont();
            if (font != null)
                _text.font = font;
            else
                Log.Warn($"{Tag} No game font found: TextMesh Pro's default is used.");
            _text.richText = true;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.enableWordWrapping = true;
            RectTransform rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            float pad = Screen.height / 40f;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        private static void Redraw()
        {
            if (_text == null || _panel == null)
                return;
            float size = Math.Max(18f, Screen.height / 28f);
            _text.fontSize = size;

            var sb = new StringBuilder();
            int rows;
            if (!_inCategory)
            {
                sb.Append("<b>Nocturne Access settings</b>\n\n");
                for (int i = 0; i < Categories.Count; i++)
                    Row(sb, $"{Categories[i].Name} ({Categories[i].Items.Count})", i == _cat);
                sb.Append("\n<size=70%>W S: move    Enter: open    C: close</size>");
                rows = Categories.Count;
            }
            else
            {
                Category c = Categories[_cat];
                sb.Append($"<b>Nocturne Access settings: {c.Name}</b>\n\n");
                // At most VisibleRows rows, scrolled to keep the selected one in view (the Keys category has 19, 0.0.213).
                int first = Math.Max(0, Math.Min(c.Row - VisibleRows / 2, c.Items.Count - VisibleRows));
                int last = Math.Min(c.Items.Count, first + VisibleRows);
                for (int i = first; i < last; i++)
                    Row(sb, c.Items[i].Value.Length > 0 ? $"{c.Items[i].Label}: {c.Items[i].Value}" : c.Items[i].Label, i == c.Row);
                string hint = _capture != null ? "Press the new key    Escape: cancel"
                    : c.Name == "Keys" ? "W S: move    Enter: new key    C: back" : "W S: move    A D Enter: change    C: back";
                sb.Append($"\n<size=70%>{hint}</size>");
                rows = last - first;
            }
            _text.text = sb.ToString();

            int lines = rows + 4;
            float pad = Screen.height / 40f;
            _panel.sizeDelta = new Vector2(Math.Max(Screen.width * 0.42f, size * 20f), lines * size * 1.35f + pad * 2);
        }

        private static void Row(StringBuilder sb, string line, bool selected)
            => sb.Append(selected ? $"<color=#FFD54A>> {line}</color>\n" : $"   {line}\n");

        /// <summary>The font of a visible game text, so the window looks like the game's own; null if none is found.</summary>
        private static TMP_FontAsset GameFont()
        {
            TMP_FontAsset any = null;
            foreach (TMP_Text t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null || t.font == null)
                    continue;
                if (UiUtil.IsVisible(t))
                    return t.font;
                any ??= t.font;
            }
            return any;
        }

        // ---- The game's input, blocked while open ----

        private static bool ShiftHeld()
        {
            int frame = Time.frameCount;
            if (frame != _shiftFrame)
            {
                _shiftFrame = frame;
                _shiftHeld = UnityInput.GetKey(KeyCode.LeftShift) || UnityInput.GetKey(KeyCode.RightShift);
            }
            return _shiftHeld;
        }

        private static void AfterAction(SIActionName __0, ref bool __result)
        {
            if (!__result)
                return;
            if (Blocking || (__0 == SIActionName.FD_CmdMenu || __0 == SIActionName.PZL_Menu) && ShiftHeld())
                __result = false;
        }

        private static void AfterAnyKey(ref bool __result)
        {
            if (__result && Blocking)
                __result = false;
        }
    }
}
