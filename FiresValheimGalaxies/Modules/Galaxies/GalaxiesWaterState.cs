using FiresCore.Services;
using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    /// <summary>
    /// Galaxies' implementation of the FiresCore water-state contract — the single place other
    /// mods (swimming, ships, NPC AI, FAT ponds) get tide-and-current-aware water answers from,
    /// via <c>FiresCore.Services.WaterState</c>. Registered by <see cref="GalaxiesModule"/>.
    ///
    /// Height: vanilla <c>Floating.GetWaterLevel</c> first (includes waves AND the tide shift
    /// WaterVolumeTidePatch applies to the live volumes), falling back to the tide-adjusted
    /// ocean baseline where no volume is loaded. Flow: <see cref="CurrentSystem"/> (ocean/tidal/
    /// river currents). Tide phase/velocity: <see cref="TideSystem"/>.
    /// </summary>
    internal sealed class GalaxiesWaterState : IWaterStateProvider
    {
        private WaterVolume _volumeCache;

        public float GetWaterSurface(Vector3 worldPos, float waveFactor)
        {
            try
            {
                var volume = _volumeCache;
                float level = Floating.GetWaterLevel(worldPos, ref volume);
                _volumeCache = volume;
                if (volume != null) return level;
            }
            catch { }
            return WaterUtil.GetWaterLevel(worldPos);
        }

        public bool TryGetFlow(Vector3 worldPos, out Vector3 flow)
        {
            if (CurrentSystem.TryGetCurrent(worldPos, out CurrentSample sample) && sample.Strength > 0f)
            {
                flow = sample.Direction * sample.Strength;
                return true;
            }
            flow = Vector3.zero;
            return false;
        }

        public float GetTidePhase01() => TideSystem.Current.TidePhase01;

        public float GetTideVelocity() => TideSystem.Current.TideVelocity;
    }
}
