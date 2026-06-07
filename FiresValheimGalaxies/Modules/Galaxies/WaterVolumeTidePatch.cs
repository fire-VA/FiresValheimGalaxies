using HarmonyLib;
using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    // Shifts every vanilla water-surface query by the live tide offset so
    // buoyancy, swimming and depth follow the tide. The visual surface is
    // driven separately via the _FiresTideOffset shader global (see TideSystem).
    //
    // Self-gating: TideSystem keeps SurfaceOffset at zero whenever the mod,
    // tides, or height application are disabled, so this is a no-op then.
    [HarmonyPatch(typeof(WaterVolume), nameof(WaterVolume.GetWaterSurface), new[] { typeof(Vector3), typeof(float) })]
    internal static class WaterVolumeTidePatch
    {
        private static void Postfix(ref float __result)
        {
            float offset = TideSystem.SurfaceOffset;
            if (offset != 0f)
                __result += offset;
        }
    }
}
