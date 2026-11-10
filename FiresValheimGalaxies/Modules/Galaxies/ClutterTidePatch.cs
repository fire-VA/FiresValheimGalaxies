using HarmonyLib;
using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    /// <summary>
    /// Makes floating water clutter (lilypads, water plants, kelp — anything with
    /// ClutterSystem.Clutter.m_snapToWater) sit on the water surface Galaxies shapes.
    /// ClutterSystem.m_waterLevel is the single reference the vanilla placement uses BOTH
    /// for the altitude test (which terrain heights qualify) AND for the snap Y of
    /// snap-to-water clutter.
    ///
    /// 0.1.70 (Fire 11-07: "lillypad and water height dont agree", Meadows pond 1566,423, pads ~1 m under the surface):
    /// this used to shift m_waterLevel by the FULL tide every LateUpdate, everywhere, while the water you swim in and see
    /// (WaterVolumeTidePatch + the FiresWater bake) takes the tide x TideInfluence (1 only in Ocean, RiverTideFraction
    /// inland) x the ice hold, + RiverRaise. Inland pads rode the whole tide. Now each veg patch is built against the SAME
    /// formula at its own centre (a GenerateVegPatch prefix, High priority so FAT's ClutterSpread queue copies the level),
    /// and m_waterLevel is put back to its base afterwards and every LateUpdate.
    ///
    /// TERRAIN SAFETY is unchanged: the altitude band and the snap move together, so a low tide re-qualifies the newly
    /// shallow band and never snaps a lily beneath exposed ground.
    /// </summary>
    internal static class ClutterTidePatch
    {
        // 0.1.55 ([seasons] FAD direct hop): captured per ClutterSystem (each game scene builds a new one), not once per process
        private static ClutterSystem s_capturedFrom;
        private static float s_baseWaterLevel;
        private static bool s_set;
        private static float s_logAt;
        private static bool s_said;

        private static void Capture(ClutterSystem cs)
        {
            if (ReferenceEquals(s_capturedFrom, cs)) return;
            s_baseWaterLevel = cs.m_waterLevel;
            s_capturedFrom = cs;
        }

        // The same surface shift WaterVolumeTidePatch gives a water query at x,z (0 with the tide off).
        internal static float SurfaceOffsetAt(float x, float z)
        {
            float offset = TideSystem.SurfaceOffset;
            bool tideShape = GalaxiesConfig.TideShapingEnabled != null && GalaxiesConfig.TideShapingEnabled.Value;
            bool rivers    = GalaxiesConfig.ExtendedRiversEnabled != null && GalaxiesConfig.ExtendedRiversEnabled.Value;
            if (offset != 0f) offset *= IceTideHold.TideScale(x, z);
            if (!tideShape && !rivers) return offset;
            float influence = tideShape ? TideRiverField.TideInfluence(x, z) : 1f;
            float raise      = rivers    ? TideRiverField.RiverRaise(x, z)     : 0f;
            return offset * influence + raise;
        }

        [HarmonyPatch(typeof(ClutterSystem), "LateUpdate")]
        private static class LateUpdatePatch
        {
            // Outside a patch build the level is the base (vanilla's own value).
            private static void Prefix(ClutterSystem __instance)
            {
                if (__instance == null) return;
                Capture(__instance);
                __instance.m_waterLevel = s_baseWaterLevel;
            }
        }

        [HarmonyPatch(typeof(ClutterSystem), "GenerateVegPatch")]
        private static class GenerateVegPatchPatch
        {
            [HarmonyPriority(Priority.High)]
            private static void Prefix(ClutterSystem __instance, Vector2Int patchID)
            {
                s_set = false;
                if (__instance == null) return;
                try
                {
                    Capture(__instance);
                    Vector3 c = __instance.GetVegPatchCenter(patchID);
                    float off = SurfaceOffsetAt(c.x, c.z);
                    __instance.m_waterLevel = s_baseWaterLevel + off;
                    s_set = true;
                    if (!s_said)
                    {
                        s_said = true;
                        FiresLogger.LogInfo("[Galaxies] floating clutter follows the shaped water surface per grass patch (tide x tide influence x ice hold + river raise), not the full tide everywhere (0.1.70)");
                    }
                    if (Time.unscaledTime >= s_logAt)
                    {
                        s_logAt = Time.unscaledTime + 10f;
                        float full = TideSystem.SurfaceOffset;
                        FiresLogger.LogDebug($"[Galaxies] clutter water level at patch ({c.x:0},{c.z:0}): {__instance.m_waterLevel:0.00} (base {s_baseWaterLevel:0.00} + shaped {off:+0.00;-0.00}); the full tide would have put it at {s_baseWaterLevel + full:0.00} (0.1.70)");
                    }
                }
                catch (System.Exception e) { FiresLogger.LogDebug($"[Galaxies] clutter water level for patch {patchID} threw ({e.Message}); base kept"); }
            }

            private static System.Exception Finalizer(ClutterSystem __instance, System.Exception __exception)
            {
                if (s_set && __instance != null) { __instance.m_waterLevel = s_baseWaterLevel; s_set = false; }
                return __exception;
            }
        }
    }
}
