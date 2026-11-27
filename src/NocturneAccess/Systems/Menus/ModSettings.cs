using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private sealed class Item
        {
            public MelonPreferences_Entry<bool> Entry;
            public string Note = "";          // said after a change ("Takes effect at the next start.")
            public Action<bool> Changed;      // what the game needs besides the new value (routes: cancel a journey)
            public string Label => string.IsNullOrEmpty(Entry.DisplayName) ? Entry.Identifier : Entry.DisplayName;
            public string Value => Entry.Value ? "On" : "Off";
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
            return $"{item.Label}, {item.Value}, {cat.Row + 1} of {cat.Items.Count}.";
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
                for (int i = 0; i < c.Items.Count; i++)
                    Row(sb, $"{c.Items[i].Label}: {c.Items[i].Value}", i == c.Row);
                sb.Append("\n<size=70%>W S: move    A D Enter: change    C: back</size>");
                rows = c.Items.Count;
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
