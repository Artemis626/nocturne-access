using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cpplibsdf_H;
using Il2Cpppipe_H;
using NocturneAccess.Core;
using NocturneAccess.Speech;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>
    /// The Labyrinth of Amala's falling minigame steered for the player (0.0.112, step 3; Francesco, 23 Oct 2026: on by
    /// default, F4 switches it off and on while falling; a start line since 0.0.142). Field 999 ("pipe" in the game's code) runs
    /// about 30 s after "Jump in?"; coins (ids 5, 6, 9) add macca, ids 1 and 2 take a share of it.
    ///
    /// Read offline (GameAssembly.dll, read only): the player's keys are four DDS3_PADCHECK_PRESS checks in
    /// pipeSection.ActionKey (L 4, R 5, U 6, D 7), so the pilot holds those buttons through PadInput while ActionKey runs
    /// (PadInput.InPipeKey): the game's own input, collision and scoring stay in charge. pipeSection.chHitObj touches an
    /// object when |obj.offset - player.offset| * 40 is within obj.w + player.w (+ len, 0 in MyHitAction's main loop) and
    /// the plane distance of their pos within (int)(obj.w + player.w): offset is depth, pos a point on one screen plane
    /// (the 20:55 probe run, 22 Oct: player id 128, w 50; R and L push vpos.x, U lowers y, D raises it).
    ///
    /// Each frame: the objects coming nearer in depth, with the frames until they reach the player's depth (from how fast
    /// their gap closed since first seen); the soonest coin it can reach in time and whose spot no harmful object crosses
    /// then is the target; a harmful object about to arrive near the player pushes it away. Any real direction key lets go
    /// at once, and the pilot waits ManualMs after the last one.
    /// </summary>
    internal static class PipePilot
    {
        private const int PipeField = 999;
        private const int PlayerId = 128;
        private const float DepthScale = 40f;      // chHitObj: the offset gap times 40 against the two sizes
        private const float PlayerW = 50f;
        private const float MaxSpeed = 28f;        // plane units per frame the player reaches (probe: vpos.x up to 34)
        private const float Accel = 6f;            // per frame while a key is held (probe: steps of about 8)
        private const float Horizon = 150f;        // frames ahead considered
        private const float Rim = 470f;            // the target is kept within this radius of the tube's centre
        private const long ManualMs = 600;

        private static readonly SDF_PADMAP[] Keys = { SDF_PADMAP.L, SDF_PADMAP.R, SDF_PADMAP.U, SDF_PADMAP.D };
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Dictionary<int, (byte Id, int Gap, long Frame)> FirstSeen = new Dictionary<int, (byte, int, long)>();

        private static bool _on = true;            // in memory: on at every launch
        private static bool _in;
        private static long _frame;
        private static long _manualUntil;
        private static int _target = -2;
        private static int _errors;

        /// <summary>The target of the last frame, for the probe's line ("coin 137", "away from 140", "hold").</summary>
        internal static string Doing { get; private set; } = "";

        internal static bool Active
        {
            get
            {
                Il2Cppfield_H.fldGlobalWork_t gb = fldGlobal.fldGb;
                return gb != null && gb.fieldID == PipeField;
            }
        }

        internal static void Install()
        {
            if (!PadInput.Installed)
                return;
            Type[] args = { typeof(tagPipeObject) };
            Patcher.Prefix("Falling minigame", typeof(pipeSection), nameof(pipeSection.ActionKey), args,
                AccessTools.Method(typeof(PipePilot), nameof(BeforeKey)));
            Patcher.Postfix("Falling minigame", typeof(pipeSection), nameof(pipeSection.ActionKey), args,
                AccessTools.Method(typeof(PipePilot), nameof(AfterKey)));
            // No damage (0.0.137; Francesco, 30 Oct 2026: hits took the whole party down to 1 HP). The game counts hits in
            // pipeSection.playerDAMAGE (MyHitAction: +0x10000 a big hit, +1 a small one) and takes the HP once, at the end:
            // pipeResult.pipeResultDraw calls datCalc.datPartyDamagePipe(big, sml), its only caller (read offline). Both counts
            // are set to 0 before it runs; the game's function still runs.
            Patcher.Prefix("Falling minigame", typeof(datCalc), nameof(datCalc.datPartyDamagePipe), new[] { typeof(int), typeof(int) },
                AccessTools.Method(typeof(PipePilot), nameof(BeforePartyDamage)));
        }

        private static void BeforePartyDamage(ref int __0, ref int __1)
        {
            if (!(Config.PipeNoDamage?.Value ?? true))
            {
                Log.Info($"[Pipe] Party damage at the end: {__0} big hits, {__1} small (left as the game's: off in the config).");
                return;
            }
            Log.Info($"[Pipe] Party damage at the end: {__0} big hits, {__1} small, cancelled (PipeNoDamage).");
            __0 = 0;
            __1 = 0;
        }

        /// <summary>F4 while falling (Hotkeys): the pilot off or on.</summary>
        internal static void Toggle()
        {
            _on = !_on;
            Release();
            Log.Info($"[Pipe] Autopilot {(_on ? "on" : "off")}.");
            SpeechManager.Say(_on ? "Autopilot on." : "Autopilot off.", interrupt: true, force: true);
        }

        /// <summary>Per update: lets go of the buttons once the minigame is over.</summary>
        internal static void Tick()
        {
            // OnUpdate never runs inside ActionKey: a gate left open by an exception in the game's code is closed here (0.0.113).
            PadInput.InPipeKey = false;
            bool inPipe = Active;
            if (inPipe == _in)
                return;
            _in = inPipe;
            Release();
            FirstSeen.Clear();
            _target = -2;
            _manualUntil = 0;
            if (!inPipe)
                return;
            Log.Info($"[Pipe] Autopilot {(_on ? "on" : "off")} for this fall.");
            // The start line (0.0.142; Francesco, 2 Nov 2026, his words, the key named as the pilot really works: a held
            // direction key takes over at once, ManualMs; it replaces 0.0.112's silent start).
            bool noDamage = Config.PipeNoDamage?.Value ?? true;
            SpeechManager.Say("Pipe minigame: " + (_on ? "autoplay engaged" : "autoplay off") + (noDamage ? ", damage off" : ", damage on")
                              + (_on ? ". Press a direction key to take control." : ". F4 for autoplay."));
        }

        private static void Release()
        {
            foreach (SDF_PADMAP k in Keys)
                PadInput.Hold(k, false);
            Doing = "";
        }

        private static void AfterKey() => PadInput.InPipeKey = false;

        private static void BeforeKey(tagPipeObject __0)
        {
            PadInput.InPipeKey = false;
            try
            {
                if (!Active || __0 == null || __0.id != PlayerId)
                    return;
                _frame++;
                Track(__0);
                long now = Clock.ElapsedMilliseconds;
                bool manual = false;
                foreach (SDF_PADMAP k in Keys)
                    manual |= PadInput.Real(k, 0);
                if (manual)
                    _manualUntil = now + ManualMs;
                if (!_on || now < _manualUntil)
                {
                    Release();
                    Doing = _on ? "manual" : "off";
                    return;
                }
                Steer(__0);
                PadInput.InPipeKey = true;
            }
            catch (Exception ex)
            {
                Release();
                if (++_errors <= 3)
                    Log.Error($"[Pipe] Autopilot failed: {ex}");
            }
        }

        // ---- What comes ----

        private struct Coming
        {
            internal int Index;
            internal byte Id;
            internal float X, Y, W;
            internal float In, Out;    // frames until the object reaches and leaves the player's depth
        }

        /// <summary>Notes each object's depth gap when first seen, so its closing speed is known later.</summary>
        private static void Track(tagPipeObject me)
        {
            Il2CppReferenceArray<tagPipeObject> objs = pipeObject.ObjWork;
            int n = objs == null ? 0 : objs.Length;
            for (int i = 0; i < n; i++)
            {
                tagPipeObject o = objs[i];
                if (o == null || o.enabled == 0 || o.id == 255 || o.id == PlayerId)
                {
                    FirstSeen.Remove(i);
                    continue;
                }
                int gap = Math.Abs(o.offset - me.offset);
                if (!FirstSeen.TryGetValue(i, out var f) || f.Id != o.id || gap > f.Gap)
                    FirstSeen[i] = (o.id, gap, _frame);
            }
        }

        private static List<Coming> Ahead(tagPipeObject me)
        {
            var list = new List<Coming>();
            var rates = new List<float>();
            foreach (var kv in FirstSeen)
                if (_frame - kv.Value.Frame >= 4)
                {
                    tagPipeObject o = pipeObject.ObjWork[kv.Key];
                    float r = (kv.Value.Gap - Math.Abs(o.offset - me.offset)) / (float)(_frame - kv.Value.Frame);
                    if (r > 0)
                        rates.Add(r);
                }
            if (rates.Count == 0)
                return list;
            rates.Sort();
            float common = rates[rates.Count / 2];
            foreach (var kv in FirstSeen)
            {
                tagPipeObject o = pipeObject.ObjWork[kv.Key];
                bool coin = o.id == 5 || o.id == 6 || o.id == 9;
                bool harm = o.id == 1 || o.id == 2;
                if (!coin && !harm || coin && o.action == 100)   // action 100: a coin being taken
                    continue;
                int gap = Math.Abs(o.offset - me.offset);
                long frames = _frame - kv.Value.Frame;
                float rate = frames >= 4 && kv.Value.Gap > gap ? (kv.Value.Gap - gap) / (float)frames : common;
                float zone = (o.w + PlayerW) / DepthScale;
                float tIn = Math.Max(0f, gap - zone) / rate;
                if (tIn > Horizon)
                    continue;
                list.Add(new Coming
                {
                    Index = kv.Key, Id = o.id, X = o.pos.x, Y = o.pos.y, W = o.w,
                    In = tIn, Out = (gap + zone) / rate,
                });
            }
            return list;
        }

        // ---- Steering ----

        private static void Steer(tagPipeObject me)
        {
            float px = me.pos.x, py = me.pos.y;
            List<Coming> ahead = Ahead(me);
            float tx = px, ty = py;
            string doing = "hold";
            int target = -1;

            Coming? best = null;
            foreach (Coming c in ahead)
            {
                if (c.Id == 1 || c.Id == 2)
                    continue;
                float reach = Dist(px, py, c.X, c.Y) - (c.W + PlayerW) * 0.6f;
                if (reach > MaxSpeed * c.In * 0.8f + 10f || Unsafe(ahead, c.X, c.Y, c.In, c.Out))
                    continue;
                if (best == null || c.In < best.Value.In - 3f || Math.Abs(c.In - best.Value.In) <= 3f
                    && Dist(px, py, c.X, c.Y) < Dist(px, py, best.Value.X, best.Value.Y))
                    best = c;
            }
            if (best != null)
            {
                tx = best.Value.X;
                ty = best.Value.Y;
                target = best.Value.Index;
                doing = $"coin {target}";
            }

            // A harmful object about to reach the player's depth near the player, or near the way to the target: away from it.
            foreach (Coming h in ahead)
            {
                if (h.Id != 1 && h.Id != 2 || h.In > 25f)
                    continue;
                float r = h.W + PlayerW + 45f;
                if (Dist(px, py, h.X, h.Y) >= r && Dist(tx, ty, h.X, h.Y) >= r)
                    continue;
                float dx = px - h.X, dy = py - h.Y, d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < 1f)
                {
                    dx = -h.X;   // on top of it: toward the centre (or sideways when it is the centre)
                    dy = -h.Y;
                    d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d < 1f)
                    {
                        dx = 1f;
                        dy = 0f;
                        d = 1f;
                    }
                }
                tx = h.X + dx / d * (r + 30f);
                ty = h.Y + dy / d * (r + 30f);
                target = 1000 + h.Index;
                doing = $"away from {h.Index}";
                break;
            }

            float rim = Dist(0, 0, tx, ty);
            if (rim > Rim)
            {
                tx *= Rim / rim;
                ty *= Rim / rim;
            }
            if (target != _target)
            {
                _target = target;
                Log.Info($"[Pipe] Autopilot: {doing} at {tx:0} {ty:0} (player {px:0} {py:0}, {ahead.Count} ahead).");
            }
            Doing = doing;

            // R raises x, L lowers it; D raises y, U lowers it (the probe run).
            Axis(tx - px, me.vpos.x, SDF_PADMAP.R, SDF_PADMAP.L);
            Axis(ty - py, me.vpos.y, SDF_PADMAP.D, SDF_PADMAP.U);
        }

        /// <summary>Holds the key toward the speed that stops at the target: the stopping distance v² / 2a.</summary>
        private static void Axis(float error, float speed, SDF_PADMAP up, SDF_PADMAP down)
        {
            float want = Math.Abs(error) < 12f ? 0f : Math.Sign(error) * Math.Min(MaxSpeed, (float)Math.Sqrt(2f * Accel * Math.Abs(error)) * 0.8f);
            float dv = want - speed;
            PadInput.Hold(up, dv > 3f);
            PadInput.Hold(down, dv < -3f);
        }

        /// <summary>Whether a harmful object reaches the player's depth at about the same time near this spot.</summary>
        private static bool Unsafe(List<Coming> ahead, float x, float y, float tIn, float tOut)
        {
            foreach (Coming h in ahead)
                if ((h.Id == 1 || h.Id == 2) && h.In <= tOut + 8f && h.Out >= tIn - 8f && Dist(x, y, h.X, h.Y) < h.W + PlayerW + 40f)
                    return true;
            return false;
        }

        private static float Dist(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx, dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
