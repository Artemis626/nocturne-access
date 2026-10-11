using System;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cppfield_H;
using UnityEngine;

namespace NocturneAccess.Systems.Navigation
{
    /// <summary>One read of the player's field state (docs/class-index.md "Field navigation").</summary>
    internal struct PlayerSnapshot
    {
        public bool Valid;

        // fldGlobal.fldGb (fldGlobalWork_t)
        public int FieldId, AreaId, BlockId, AmSBlock, AmFloor, ElvIdx, CamMode, CamMode2, SyukanCamReset;
        public float X, Y, Z, Kakudo, KakudoGoal;

        // fldPlayer statics
        public int Act, KeyInputDir, SyukanDly;
        public bool Run;

        // Unity objects, to compare spaces and angle units with fldGb
        public bool HasObject;
        public Vector3 ObjPos;
        public float ObjYaw;
        public bool HasCamera;
        public Vector3 CamPos;
        public float CamYaw;
    }

    /// <summary>
    /// Reads the field's own data: the player and the game's place-name tables.
    /// Every read is guarded: outside a field these statics can be null or stale.
    /// </summary>
    internal static class FieldData
    {
        /// <summary>A door volume's name in fldEveHit.gEvnRng ("01d_15"); one pattern for every navigation class (0.0.56).</summary>
        internal static readonly System.Text.RegularExpressions.Regex DoorVolume = new System.Text.RegularExpressions.Regex(@"^\d+d_\d+$");

        /// <summary>A story or event volume's name ("01eve_03").</summary>
        internal static readonly System.Text.RegularExpressions.Regex EventVolume = new System.Text.RegularExpressions.Regex(@"^\d+eve_\d+$");

        /// <summary>
        /// A volume box's horizontal facing (gEvnRngBox.boxnml, unit length in X and Z; doors point out to the side the player
        /// stands on), or 0 0 when it has none (PointsOfInterest, WarpMap, ExploredDoors).
        /// </summary>
        internal static void BoxFacing(putevebox_s box, out float nx, out float nz)
        {
            nx = nz = 0f;
            if (box == null)
                return;
            Vector4 n = box.boxnml;
            float len = (float)Math.Sqrt(n.x * n.x + n.z * n.z);
            if (len > 0.5f && Math.Abs(n.y) < 0.5f)
            {
                nx = n.x / len;
                nz = n.z / len;
            }
        }

        internal static PlayerSnapshot ReadPlayer()
        {
            var s = new PlayerSnapshot();
            fldGlobalWork_t gb = fldGlobal.fldGb;
            if (gb == null)
                return s;

            s.Valid = true;
            s.FieldId = gb.fieldID;
            s.AreaId = gb.areaID;
            s.BlockId = gb.blockID;
            s.AmSBlock = gb.AmSBlock;
            s.AmFloor = gb.AmFloor;
            s.ElvIdx = gb.ElvIdx;
            s.CamMode = gb.cammode;
            s.CamMode2 = gb.cammode2;
            s.SyukanCamReset = gb.syukan_cam_reset_flg;
            s.X = gb.playerX;
            s.Y = gb.playerY;
            s.Z = gb.playerZ;
            s.Kakudo = gb.PlayerKakudo;
            s.KakudoGoal = gb.PlayerKakudoGoal;

            s.Act = fldPlayer.fldPlayerAct;
            s.Run = fldPlayer.playerRun;
            s.KeyInputDir = fldPlayer.gKeyInputDir;
            s.SyukanDly = fldPlayer.gFldPlayerSyukanDly;

            GameObject player = fldPlayer.fldPlayerObj;
            if (player != null)
            {
                Transform t = player.transform;
                s.HasObject = true;
                s.ObjPos = t.position;
                s.ObjYaw = t.eulerAngles.y;
            }

            GameObject camera = fldCamera.fldCameraObj;
            if (camera != null)
            {
                Transform t = camera.transform;
                s.HasCamera = true;
                s.CamPos = t.position;
                s.CamYaw = t.eulerAngles.y;
            }

            return s;
        }

        /// <summary>fldGlobal.area_name entry with _fld == field and _area == area, or "".</summary>
        internal static string AreaName(int field, int area)
        {
            Il2CppReferenceArray<fldAreaName_t> table = fldGlobal.area_name;
            if (table == null)
                return "";
            for (int i = 0; i < table.Length; i++)
            {
                fldAreaName_t e = table[i];
                if (e != null && e._fld == field && e._area == area)
                    return e._name ?? "";
            }
            return "";
        }

        /// <summary>fldGlobal.basyo_name entry with _fld == field and _basyo == basyo, or "".</summary>
        internal static string BasyoName(int field, int basyo)
        {
            Il2CppReferenceArray<fldBasyoName_t> table = fldGlobal.basyo_name;
            if (table == null)
                return "";
            for (int i = 0; i < table.Length; i++)
            {
                fldBasyoName_t e = table[i];
                if (e != null && e._fld == field && e._basyo == basyo)
                    return e._name ?? "";
            }
            return "";
        }

        /// <summary>fldGlobal.floor_name entry with _fld == field and _floor == floor, or "".</summary>
        internal static string FloorName(int field, int floor)
        {
            Il2CppReferenceArray<fldFloorName_t> table = fldGlobal.floor_name;
            if (table == null)
                return "";
            for (int i = 0; i < table.Length; i++)
            {
                fldFloorName_t e = table[i];
                if (e != null && e._fld == field && e._floor == floor)
                    return e._name ?? "";
            }
            return "";
        }

        internal static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        internal static string V(Vector3 v) => $"{F(v.x)} {F(v.y)} {F(v.z)}";

        internal static string V(Vector4 v) => $"{F(v.x)} {F(v.y)} {F(v.z)} {F(v.w)}";

        /// <summary>A game string quoted on one log line ("null" for none); line breaks are written as \n (0.0.67: they were kept).</summary>
        internal static string Q(string s) => s == null ? "null" : "\"" + s.Replace("\r", "").Replace("\n", "\\n") + "\"";
    }
}
