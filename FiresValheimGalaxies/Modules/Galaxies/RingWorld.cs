using System;
using System.Reflection;
using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    // 0.1.62 (halo ring, lead 10-24: "FTS + FAT own the ring sky"): on a ring world (Core FiresCore.World.GravityUp.Active, the ONE ring authority, by
    // reflection) the ring has no sea, so Galaxies' tides, currents and whirlpools and its moon and orbitals all stand down.
    // Re-checked at most every 2 s (FAT syncs the ring after load). Without FAT, or with no ring, Active is false and nothing changes.
    internal static class RingWorld
    {
        private static PropertyInfo s_haloActive;
        private static bool s_resolved, s_active, s_logged;
        private static float s_nextCheck = -1f;

        public static bool Active
        {
            get
            {
                if (Time.realtimeSinceStartup < s_nextCheck) return s_active;
                s_nextCheck = Time.realtimeSinceStartup + 2f;
                if (!s_resolved)
                {
                    s_resolved = true;
                    try { s_haloActive = Type.GetType("FiresCore.World.GravityUp, FiresUnifiedCore", false)?.GetProperty("Active", BindingFlags.Public | BindingFlags.Static); }
                    catch { s_haloActive = null; }
                }
                bool now = false;
                if (s_haloActive != null)
                    try { now = (bool)s_haloActive.GetValue(null); } catch { now = false; }
                if (now != s_active || (now && !s_logged))
                {
                    s_logged = now;
                    Debug.Log(now ? "[FiresValheimGalaxies] [Galaxies] halo ring world: tides, currents, whirlpools, moon and orbitals stand down (FTS + FAT own the ring sky)"
                                  : "[FiresValheimGalaxies] [Galaxies] no halo ring: tides, currents, whirlpools, moon and orbitals run");
                }
                s_active = now;
                return s_active;
            }
        }
    }
}
