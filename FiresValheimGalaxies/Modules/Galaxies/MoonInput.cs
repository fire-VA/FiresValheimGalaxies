using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    // Single integration point with the moon driver. Everything tide-related
    // reads from here. Orbit angle and phase are pure functions of server-
    // authoritative world time, so every client arrives at the same tide with
    // no sync needed.
    //
    // Orbit angle is clocked by EnvMan's day fraction (one lap per day/night
    // cycle), matching FiresMoon's own orbit clock. Phase comes straight from
    // FiresMoon.ComputePhase01() (source-linked from FiresAdminTerrain) so the
    // tides and the visible 3D moon stay in lockstep.
    //
    // All angles are in radians.
    internal static class MoonInput
    {
        private const float FallbackDayLengthSeconds = 1800f;
        private const float TwoPi = Mathf.PI * 2f;

        public static float GetOrbitAngleRadians()
        {
            if (EnvMan.instance != null)
                return Mathf.Repeat(EnvMan.instance.GetDayFraction(), 1f) * TwoPi;
            return GetFallbackOrbitAngle();
        }

        public static float GetPhaseAngleRadians()
        {
            // Compute01: 0 = new moon, 0.5 = full moon, wraps at 1. Lives in FUC
            // so the tides and the visible 3D moon (FiresTossinShade) share one
            // source — see FiresCore.Services.MoonPhase.
            return Mathf.Repeat(FiresCore.Services.MoonPhase.Compute01(), 1f) * TwoPi;
        }

        // 0.1.59 (Fire 10-20: Galaxies adds its own blood moon; this was the seam). The night of a full moon - phase within
        // 0.75 day of full on the same clock as FiresCore.Services.MoonPhase (so tides, the visible moon and Vedr's lunar month agree) -
        // is a blood moon for about 1 in BloodMoonOneIn cycles. Which cycles is a hash of the moon-cycle number and the world seed:
        // deterministic, identical on server and every client (the two keys are server-synced), no RPC or saved state needed.
        public static bool IsBloodMoon()
        {
            try
            {
                if (GalaxiesConfig.BloodMoonEnabled == null || !GalaxiesConfig.BloodMoonEnabled.Value) return false;
                if (FiresCore.Services.MoonPhase.Override >= 0f || EnvMan.instance == null || ZNet.instance == null) return false;
                int day = EnvMan.instance.GetDay(ZNet.instance.GetTimeSeconds());
                float synodic = Mathf.Max(0.1f, FiresCore.Services.MoonPhase.SynodicDays);
                double total = (day + EnvMan.instance.GetDayFraction() + FiresCore.Services.MoonPhase.PhaseOffsetDays) / synodic;
                long cycle = (long)System.Math.Floor(total);
                double fromFull = System.Math.Abs((total - cycle) - 0.5) * synodic;   // days from this cycle's full moon
                if (fromFull > 0.75) return false;
                return BloodCycle(cycle, WorldSeed(), Mathf.Max(2, GalaxiesConfig.BloodMoonOneIn != null ? GalaxiesConfig.BloodMoonOneIn.Value : 10));
            }
            catch { return false; }
        }

        internal static bool BloodCycle(long cycle, int seed, int oneIn)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;   // FNV-1a over (seed, cycle): stable on every machine
                foreach (long v in new long[] { seed, cycle }) for (int i = 0; i < 8; i++) { h ^= (byte)(v >> (i * 8)); h *= 1099511628211UL; }
                return h % (ulong)oneIn == 0UL;
            }
        }

        private static int WorldSeed()
        {
            try { if (WorldGenerator.instance != null) return WorldGenerator.instance.GetSeed(); } catch { }
            return 0;
        }

        public static float GetPhase01()
        {
            return Mathf.Repeat(GetPhaseAngleRadians(), TwoPi) / TwoPi;
        }

        // Spring-tide weighting in [0, 1]. 1 at new and full moon, 0 at quarters.
        public static float GetSpringTideWeight()
        {
            return Mathf.Abs(Mathf.Cos(GetPhaseAngleRadians()));
        }

        private static float GetFallbackOrbitAngle()
        {
            float cycles = Time.time / FallbackDayLengthSeconds;
            return Mathf.Repeat(cycles, 1f) * TwoPi;
        }
    }
}
