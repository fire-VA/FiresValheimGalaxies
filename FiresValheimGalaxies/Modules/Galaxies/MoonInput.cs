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

        // No blood-moon event exists in the stack yet. Single seam: when one is
        // added, return its active state here and the blood-moon tide range engages.
        public static bool IsBloodMoon()
        {
            return false;
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
