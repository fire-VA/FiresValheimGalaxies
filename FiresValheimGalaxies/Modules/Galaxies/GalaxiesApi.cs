using UnityEngine;
using VerdantsAscent.Water.Core;

namespace Galaxies
{
    /// <summary>
    /// 0.1.52 ([wishbone]: the Werewolf class's full-moon trigger and Lunar Hunger bonus, RPGClasses' LunarClock reads these by
    /// reflection). The moon's phase on the same cycle the sky moon and the tides use: Vedr's season over CyclesPerSeason when
    /// FollowVedrSeasons is on and Vedr runs, else SynodicDays. Computed from the synced world day + day fraction, so clients and
    /// the dedicated server agree. ADD-ONLY contract: never rename or re-sign these; new members may be added.
    /// </summary>
    public static class GalaxiesApi
    {
        /// <summary>The default full-moon window: within this fraction of the cycle either side of 0.5 (1/30 = a day of a 30-day cycle).</summary>
        public const float FullMoonWindow = 1f / 30f;

        /// <summary>The moon phase, 0..1: 0 = new, 0.25 = first quarter, 0.5 = full, 0.75 = last quarter. -1 before the world clock exists.</summary>
        public static float MoonPhase()
        {
            if (EnvMan.instance == null || ZNet.instance == null) return -1f;
            return FiresMoon.ComputePhase01();
        }

        /// <summary>True while the phase is within <see cref="FullMoonWindow"/> of full (0.5); false before the world clock exists.</summary>
        public static bool IsFullMoon() => IsFullMoon(FullMoonWindow);

        /// <summary>True while the phase is within <paramref name="window"/> (a fraction of the cycle) of full (0.5).</summary>
        public static bool IsFullMoon(float window)
        {
            float p = MoonPhase();
            return p >= 0f && Mathf.Abs(p - 0.5f) <= Mathf.Max(0f, window);
        }

        /// <summary>The cycle length in game days that MoonPhase runs on.</summary>
        public static float CycleDays() => FiresMoon.SynodicDays;

        /// <summary>0.1.53 ([generator]'s Core 0.2.523 minimap moon, Fire's ask: the real cratered texture). The sky moon's albedo
        /// texture, loaded from Galaxies' bundle on first call if the sky hasn't loaded it yet. Shared, read-only: never modify or
        /// destroy it. Null when the bundle has no moon (a dedicated server, or a failed load).</summary>
        public static Texture GetMoonTexture()
        {
            if (FiresMoon.MoonTexture == null && VerdantsAscent.FiresValheimGalaxies.assetBundle != null)
            {
                try { FiresMoon.TryLoadAssets(VerdantsAscent.FiresValheimGalaxies.assetBundle); } catch { }
            }
            return FiresMoon.MoonTexture;
        }

        /// <summary>0.1.71 (Fire 11-07: camera / fog / lily pads disagree with the water): metres to add to vanilla's sea level
        /// (ZoneSystem.m_waterLevel) for the water surface Galaxies shapes at x,z, with no waves: the tide x tide influence x ice hold
        /// + river raise. The SAME formula WaterVolumeTidePatch gives every vanilla water query (buoyancy, swimming) and floating
        /// clutter is built against; 0 with the tide off. Main thread.</summary>
        public static float WaterSurfaceOffsetAt(float x, float z) => VerdantsAscent.Galaxies.ClutterTidePatch.SurfaceOffsetAt(x, z);
    }
}
