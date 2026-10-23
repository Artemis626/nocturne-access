using System;
using System.Diagnostics;
using System.Text;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cpplibsdf_H;
using Il2Cpppipe_H;
using NocturneAccess.Core;
using NocturneAccess.Systems.Navigation;

namespace NocturneAccess.Capture
{
    /// <summary>
    /// Log only (0.0.110; Francesco, 22 Oct 2026: step 2 of automating the Labyrinth of Amala's falling minigame). The minigame
    /// runs as field 999 for about 30 s after "Jump in?" (the game's "pipe" code). Every SampleMs while in it: the player's
    /// state (pipeSection.myIndex, lrRad, udRad, myMaka, playerDAMAGE, myStatus, rBoxNum; pipeMain.pipeFlow), the directions
    /// really held (the minigame reads U, D, L, R with DDS3_PADCHECK_PRESS in pipeSection.ActionKey, read offline) and every
    /// enabled object of pipeObject.ObjWork (id, action, pos, vpos, w, status). Read offline: touching id 5, 6 or 9 adds
    /// macca (pipeObject.pipeMyMakaAdd), ids 1 and 2 put the player in a damage state that loses macca (pipeMyMakaSubRate).
    /// Nothing is said or changed; the log tells which numbers mean the player, coins and harm, and how input moves the player.
    /// </summary>
    internal static class PipeProbe
    {
        private const int PipeField = 999;
        private const long SampleMs = 100;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _lastMs = long.MinValue / 2;
        private static bool _in;
        private static int _samples;

        internal static void Tick()
        {
            Il2Cppfield_H.fldGlobalWork_t gb = fldGlobal.fldGb;
            bool inPipe = gb != null && gb.fieldID == PipeField;
            if (inPipe != _in)
            {
                _in = inPipe;
                Log.Info(inPipe ? "[Pipe] The falling minigame (field 999) started: sampling every 100 ms." : $"[Pipe] The minigame ended after {_samples} samples.");
                _samples = 0;
            }
            if (!inPipe)
                return;
            long now = Clock.ElapsedMilliseconds;
            if (now - _lastMs < SampleMs)
                return;
            _lastMs = now;
            try
            {
                var sb = new StringBuilder();
                sb.Append($"[Pipe] t {now} flow {pipeMain.pipeFlow} my {pipeSection.myIndex} lr {pipeSection.lrRad:0.###} ud {pipeSection.udRad:0.###} ");
                sb.Append($"maka {pipeSection.myMaka} dmg {pipeSection.playerDAMAGE} st {pipeSection.myStatus} rbox {pipeSection.rBoxNum} keys ");
                sb.Append(PadInput.Real(SDF_PADMAP.L, 0) ? "L" : "").Append(PadInput.Real(SDF_PADMAP.R, 0) ? "R" : "")
                  .Append(PadInput.Real(SDF_PADMAP.U, 0) ? "U" : "").Append(PadInput.Real(SDF_PADMAP.D, 0) ? "D" : "").Append($" me {pipeObject.ObjWork?[149]?.offset} pilot {PipePilot.Doing} |");
                Il2CppReferenceArray<tagPipeObject> objs = pipeObject.ObjWork;
                int n = objs == null ? 0 : objs.Length;
                for (int i = 0; i < n; i++)
                {
                    tagPipeObject o = objs[i];
                    if (o == null || o.enabled == 0)
                        continue;
                    sb.Append($" {i}:{o.id}/{o.action} d{o.offset} {o.pos.x:0.#},{o.pos.y:0.#} v{o.vpos.x:0.##},{o.vpos.y:0.##} w{o.w:0.#} s{o.status:X}");
                }
                Log.Info(sb.ToString());
                _samples++;
            }
            catch (Exception ex)
            {
                Log.Warn($"[Pipe] Sample failed: {ex.Message}");
                _lastMs = now + 5000;   // not every frame
            }
        }
    }
}
