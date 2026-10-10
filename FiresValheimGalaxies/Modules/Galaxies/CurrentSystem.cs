using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    internal readonly struct CurrentSample
    {
        public readonly Vector3 Direction;
        public readonly float Strength;
        public readonly string Source;

        public CurrentSample(Vector3 direction, float strength, string source)
        {
            Direction = direction;
            Strength = strength;
            Source = source;
        }
    }

    // Picks the current sample for a world position, in priority order:
    //   1. River currents (downhill flow; independent of tide)
    //   2. Tidal currents (incoming swirl / outgoing push)
    //   3. Open-ocean noise field (baseline)
    internal static class CurrentSystem
    {
        public static bool TryGetCurrent(Vector3 pos, out CurrentSample sample)
        {
            // Whirlpools first — the strongest local phenomenon overrides river/tide/ocean
            // flow inside its ring. Routing it through here means ships, swimmers and every
            // FiresCore WaterState.TryGetFlow consumer get grabbed with zero extra wiring.
            if (WhirlpoolSystem.TryGetFlow(pos, out Vector3 whirlDir, out float whirlSpeed))
            {
                sample = new CurrentSample(whirlDir, whirlSpeed, "Whirlpool");
                return true;
            }

            if (GalaxiesConfig.EnableRiverCurrents.Value &&
                RiverCurrentCache.TryGetCachedRiverCurrent(pos, out Vector3 riverDir, out float riverStrength))
            {
                sample = new CurrentSample(riverDir, riverStrength, "River");
                return true;
            }

            if (TidalCurrentField.TryGetCurrent(pos, out Vector3 tidalDir, out float tidalStrength))
            {
                sample = new CurrentSample(tidalDir, tidalStrength, "Tidal");
                return true;
            }

            if (GalaxiesConfig.EnableOceanCurrents.Value &&
                TryGetOceanCurrent(pos, out Vector3 oceanDir))
            {
                sample = new CurrentSample(oceanDir, GalaxiesConfig.OceanCurrentStrength.Value, "Ocean");
                return true;
            }

            sample = default;
            return false;
        }

        // Ocean current from the 2D curl of a Perlin scalar field. Unit vector in XZ.
        public static bool TryGetOceanCurrent(Vector3 pos, out Vector3 direction)
        {
            direction = Vector3.zero;

            float scale = Mathf.Max(50f, GalaxiesConfig.OceanNoiseScale.Value);
            float seed  = GalaxiesConfig.OceanNoiseSeed.Value;

            float h = scale * 0.01f;

            float u = pos.x / scale + seed;
            float v = pos.z / scale + seed;
            float hN = h / scale;

            float psiXp = Mathf.PerlinNoise(u + hN, v);
            float psiXm = Mathf.PerlinNoise(u - hN, v);
            float psiZp = Mathf.PerlinNoise(u, v + hN);
            float psiZm = Mathf.PerlinNoise(u, v - hN);

            float dPsiDx = (psiXp - psiXm) / (2f * h);
            float dPsiDz = (psiZp - psiZm) / (2f * h);

            Vector3 flow = new Vector3(dPsiDz, 0f, -dPsiDx);

            if (flow.sqrMagnitude < 1e-8f)
                return false;

            direction = flow.normalized;
            return true;
        }
    }
}
