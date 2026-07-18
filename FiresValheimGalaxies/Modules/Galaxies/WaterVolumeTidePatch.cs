using HarmonyLib;
using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    // Shifts every vanilla water-surface query so buoyancy, swimming and depth follow the
    // shaped water surface. The matching VISUAL bend lives in the FiresWater shader (tide
    // global + the _FiresTideRiverField texture), so the surface you float on and the surface
    // you see stay welded.
    //
    //   • Tide: SurfaceOffset scaled by TideRiverField.TideInfluence — full in open ocean,
    //     a fraction in rivers/swamps so they don't drain at low tide (feature A).
    //   • River raise: TideRiverField.RiverRaise, a bank-safe upward bend (feature B).
    //
    // Fast path: when neither shaping feature is on this is the original uniform tide shift,
    // so it costs nothing extra. Self-gating: TideSystem keeps SurfaceOffset at zero whenever
    // the mod/tides are disabled.
    [HarmonyPatch(typeof(WaterVolume), nameof(WaterVolume.GetWaterSurface), new[] { typeof(Vector3), typeof(float) })]
    internal static class WaterVolumeTidePatch
    {
        private static void Postfix(Vector3 point, ref float __result)
        {
            float offset = TideSystem.SurfaceOffset;

            bool tideShape = GalaxiesConfig.TideShapingEnabled != null && GalaxiesConfig.TideShapingEnabled.Value;
            bool rivers    = GalaxiesConfig.ExtendedRiversEnabled != null && GalaxiesConfig.ExtendedRiversEnabled.Value;

            if (!tideShape && !rivers)
            {
                if (offset != 0f) __result += offset;   // original uniform tide
                return;
            }

            float influence = tideShape ? TideRiverField.TideInfluence(point.x, point.z) : 1f;
            float raise      = rivers    ? TideRiverField.RiverRaise(point.x, point.z)     : 0f;
            __result += offset * influence + raise;
        }
    }
}
