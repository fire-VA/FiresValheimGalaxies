using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    // 0.1.71 (Fire 11-07, shots 537-539, 1602,424 / 1629,415: swimming by physics on what looks like dry sand; the underwater
    // view on in some spots, off in others): one Debug line every 2 s while the local player is in water or the camera is within
    // 3 m of a water level, with every level that decides something, so the disagreement is proven from the log:
    //   physics  = the player's liquid level (vanilla Character, set from WaterVolume.GetWaterSurface = WaterVolumeTidePatch)
    //   shaped   = sea + GalaxiesApi.WaterSurfaceOffsetAt (the same formula, no waves; what the drawn surface is meant to be)
    //   flat     = sea + the full tide (the _FiresTideOffset global)
    //   FTS      = FiresVolumetricFogPass.TrueWaterY at the camera (its kind) and SubmersionState's current decision
    // plus the ground under the player, the tide pieces (influence, raise, ice hold) and the camera y. Read-only, no behaviour change.
    [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
    internal static class WaterLevelProbe
    {
        private static float s_at;
        private static bool s_ftsBound;
        private static MethodInfo s_trueWaterY;
        private static FieldInfo s_submerged;
        private static readonly object[] s_args = new object[2];

        private static void BindFts()
        {
            if (s_ftsBound) return;
            s_ftsBound = true;
            try
            {
                var fog = AccessTools.TypeByName("VerdantsAscent.Water.Core.FiresVolumetricFogPass");
                s_trueWaterY = fog?.GetMethod("TrueWaterY", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Vector3), typeof(string).MakeByRefType() }, null);
                var sub = AccessTools.TypeByName("VerdantsAscent.Water.Core.SubmersionState");
                s_submerged = sub != null ? AccessTools.Field(sub, "s_submerged") : null;
            }
            catch (Exception) { s_trueWaterY = null; s_submerged = null; }
        }

        private static void Postfix()
        {
            try
            {
                if (Time.unscaledTime < s_at) return;
                s_at = Time.unscaledTime + 2f;
                var p = Player.m_localPlayer;
                var cam = Utils.GetMainCamera();
                if (p == null || cam == null || ZoneSystem.instance == null) return;
                Vector3 c = cam.transform.position, pp = p.transform.position;
                float sea = ZoneSystem.instance.m_waterLevel;
                float full = TideSystem.SurfaceOffset;
                float flat = sea + full;
                float shaped = sea + ClutterTidePatch.SurfaceOffsetAt(c.x, c.z);
                float shapedP = sea + ClutterTidePatch.SurfaceOffsetAt(pp.x, pp.z);
                float physics = p.GetLiquidLevel();
                bool inWater = p.InWater();
                bool near = inWater || Mathf.Abs(c.y - shaped) < 3f || Mathf.Abs(c.y - flat) < 3f;
                if (!near) return;
                BindFts();
                string fts = "FTS not found";
                if (s_trueWaterY != null)
                {
                    s_args[0] = c; s_args[1] = null;
                    float y = (float)s_trueWaterY.Invoke(null, s_args);
                    fts = $"FTS TrueWaterY {y:0.00} ({s_args[1]}), view {(s_submerged != null ? ((bool)s_submerged.GetValue(null) ? "UNDERWATER" : "above") : "?")}";
                }
                bool tideShape = GalaxiesConfig.TideShapingEnabled != null && GalaxiesConfig.TideShapingEnabled.Value;
                bool rivers = GalaxiesConfig.ExtendedRiversEnabled != null && GalaxiesConfig.ExtendedRiversEnabled.Value;
                float infl = tideShape ? TideRiverField.TideInfluence(c.x, c.z) : 1f;
                float raise = rivers ? TideRiverField.RiverRaise(c.x, c.z) : 0f;
                float ice = full != 0f ? IceTideHold.TideScale(c.x, c.z) : 1f;
                float ground = ZoneSystem.instance.GetGroundHeight(pp);
                FiresLogger.LogDebug($"[WaterProbe] player ({pp.x:0},{pp.y:0.00},{pp.z:0}) ground {ground:0.00}, in water {inWater}, physics level {physics:0.00} (shaped there {shapedP:0.00}); "
                    + $"camera y {c.y:0.00} ({(c.y < shaped ? "under" : "above")} shaped by {shaped - c.y:+0.00;-0.00}): shaped {shaped:0.00} = sea {sea:0.00} + tide {full:+0.00;-0.00} x influence {infl:0.00} x ice {ice:0.00} + river raise {raise:0.00}; "
                    + $"flat (sea + full tide) {flat:0.00}; {fts} (0.1.71)");
            }
            catch (Exception e) { FiresLogger.LogDebug($"[WaterProbe] threw: {e.Message}"); s_at = Time.unscaledTime + 30f; }
        }
    }
}
