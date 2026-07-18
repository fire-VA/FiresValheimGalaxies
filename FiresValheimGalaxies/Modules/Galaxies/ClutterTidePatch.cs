using HarmonyLib;
using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    /// <summary>
    /// Makes floating water clutter (lilypads, water plants, kelp — anything with
    /// ClutterSystem.Clutter.m_snapToWater) ride the moon tide instead of sitting at a
    /// fixed sea level. ClutterSystem.m_waterLevel is the single reference the vanilla
    /// placement uses BOTH for the altitude test (which terrain heights qualify) AND for
    /// the snap Y of snap-to-water clutter — and vanilla never rewrites it after load. So
    /// shifting it by the live tide offset each LateUpdate (before the patches regenerate)
    /// makes new clutter follow the water down/up.
    ///
    /// TERRAIN SAFETY (no below-ground phasing): snap-to-water clutter only passes the alt
    /// test where the terrain sits at/below m_waterLevel, so snapping it to m_waterLevel is
    /// already "water level, clamped to terrain" (== max(water, terrain) for those points).
    /// Lowering m_waterLevel with the tide moves BOTH the qualifying band and the snap
    /// together, so a low tide simply re-qualifies the newly-shallow band — it never snaps a
    /// lily beneath exposed ground.
    ///
    /// The base level is captured once so the tide is applied as an OFFSET (preserves any
    /// serialized non-30 value); tide 0 → unchanged.
    /// </summary>
    [HarmonyPatch(typeof(ClutterSystem), "LateUpdate")]
    internal static class ClutterTidePatch
    {
        private static bool  s_captured;
        private static float s_baseWaterLevel;

        private static void Prefix(ClutterSystem __instance)
        {
            if (__instance == null) return;
            if (!s_captured) { s_baseWaterLevel = __instance.m_waterLevel; s_captured = true; }
            __instance.m_waterLevel = s_baseWaterLevel + TideSystem.SurfaceOffset;
        }
    }
}
