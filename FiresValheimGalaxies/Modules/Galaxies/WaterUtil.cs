using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    // Terrain/water sampling helpers shared by the current and tide systems.
    // Ground height comes from WorldGenerator so it resolves world-wide, not
    // just in loaded zones. Water level is the vanilla ocean baseline shifted
    // by the live tide offset so depth queries follow the tide.
    internal static class WaterUtil
    {
        private const float DefaultOceanLevel = 30f;

        public static bool TryGetGroundY(Vector3 pos, out float groundY)
        {
            groundY = 0f;
            WorldGenerator wg = WorldGenerator.instance;
            if (wg == null)
                return false;

            groundY = wg.GetHeight(pos.x, pos.z);
            return true;
        }

        public static float OceanBaselineY()
        {
            return ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : DefaultOceanLevel;
        }

        // Current surface y at a position: ocean baseline plus the active tide
        // offset. Used for river/ocean depth and ship surface proximity.
        public static float GetWaterLevel(Vector3 pos)
        {
            return OceanBaselineY() + TideSystem.SurfaceOffset;
        }

        public static bool IsNearWaterSurface(Vector3 pos, float tolerance)
        {
            return Mathf.Abs(pos.y - GetWaterLevel(pos)) <= tolerance;
        }
    }
}
