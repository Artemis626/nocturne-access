using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using NocturneAccess.Core;
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
    /// (F is the game's own menu key) only in the field, on the overworld and in battle, never inside a game menu. First test:
    /// two settings; the full list follows if the design works (docs/SETTINGS-TUTORIAL-PLAN.md).
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
            public string Label => string.IsNullOrEmpty(Entry.DisplayName) ? Entry.Identifier : Entry.DisplayName;
            public string Value => Entry.Value ? "On" : "Off";
        }

        private static readonly List<Item> Items = new List<Item>();
        private static bool _open;
        private static bool _releaseHold;
        private static long _closedMs;
        private static int _row;
        private static GameObject _root;
        private static TextMeshProUGUI _text;
        private static RectTransform _panel;
        private static int _shiftFrame = -1;
        private static bool _shiftHeld;

        internal static bool IsOpen => _open;

        /// <summary>The game sees no key: the window is open, or a key pressed in it is still held.</summary>
        private static bool Blocking => _open || _releaseHold;

        internal static void Install()
        {
            Items.Clear();
            Items.Add(new Item { Entry = Config.WallBumpSound });
            Items.Add(new Item { Entry = Config.DoorSound });

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
            _row = Math.Min(_row, Items.Count - 1);
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
            SpeechManager.Say($"Mod settings. W and S: move. A, D or Enter: change. C: close. {RowLine()}", interrupt: true, force: true);
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

            bool shift = UnityInput.GetKey(KeyCode.LeftShift) || UnityInput.GetKey(KeyCode.RightShift);
            if (Down(KeyCode.C) || Down(KeyCode.Escape) || shift && Down(KeyCode.F))
            {
                Close();
                return true;
            }
            if (Down(KeyCode.W) || Down(KeyCode.UpArrow))
                Move(-1);
            else if (Down(KeyCode.S) || Down(KeyCode.DownArrow))
                Move(1);
            else if (Down(KeyCode.A) || Down(KeyCode.D) || Down(KeyCode.LeftArrow) || Down(KeyCode.RightArrow)
                     || Down(KeyCode.Return) || Down(KeyCode.KeypadEnter) || Down(KeyCode.Space))
                Change();
            else if (Down(KeyCode.F1))
                SpeechManager.Say(RowLine(), interrupt: true, force: true);
            else if (Down(KeyCode.F7))
                SpeechManager.Say("W and S: move. A, D or Enter: change. C, Escape or Shift+F: close. F1: repeat.", interrupt: true, force: true);
            return true;
        }

        private static bool Down(KeyCode key) => UnityInput.GetKeyDown(key);

        private static void Move(int step)
        {
            int next = _row + step;
            if (next < 0 || next >= Items.Count)
            {
                // No wrap: the edge says the same row again, so the player hears where the list ends.
                SpeechManager.Say(RowLine(), interrupt: true, force: true);
                return;
            }
            _row = next;
            Redraw();
            SpeechManager.Say(RowLine(), interrupt: true, force: true);
        }

        private static void Change()
        {
            Item item = Items[_row];
            item.Entry.Value = !item.Entry.Value;
            Config.Save(item.Entry);
            Log.Info($"{Tag} {item.Entry.Identifier} = {item.Entry.Value}.");
            Redraw();
            SpeechManager.Say($"{item.Label}, {item.Value}.", interrupt: true, force: true);
        }

        private static string RowLine()
        {
            Item item = Items[_row];
            return $"{item.Label}, {item.Value}, {_row + 1} of {Items.Count}.";
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
            sb.Append("<b>Nocturne Access settings</b>\n\n");
            for (int i = 0; i < Items.Count; i++)
            {
                string line = $"{Items[i].Label}: {Items[i].Value}";
                sb.Append(i == _row ? $"<color=#FFD54A>> {line}</color>\n" : $"   {line}\n");
            }
            sb.Append($"\n<size=70%>W S: move    A D Enter: change    C: close</size>");
            _text.text = sb.ToString();

            int lines = Items.Count + 4;
            float pad = Screen.height / 40f;
            _panel.sizeDelta = new Vector2(Math.Max(Screen.width * 0.42f, size * 18f), lines * size * 1.35f + pad * 2);
        }

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
