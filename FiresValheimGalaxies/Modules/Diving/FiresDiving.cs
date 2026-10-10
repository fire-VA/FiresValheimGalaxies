namespace VerdantsAscent.Diving
{
    /// <summary>
    /// Player swimming is Core's (FiresUnifiedCore 0.2.298, FiresCore.Movement.PlayerSwim; Fire 10-05: "it should be moved to core like
    /// crawling and climbing"). Its [Swimming] setting is Auto (on when Galaxies is installed), On or Off; Off is vanilla swimming.
    /// Galaxies keeps no player diving of its own. What it still needs from Core is one answer, read by reflection so Galaxies loads on
    /// any Core: is the local player's head under the surface (the camera may then follow it down, FiresWaterCameraClampPatch).
    /// Creature diving (CreatureDiving) is Galaxies' own and does not depend on this.
    /// </summary>
    public static class FiresDiving
    {
        private static System.Reflection.PropertyInfo s_coreHeadUnderwater;
        private static bool s_coreLookedUp;

        /// <summary>Core's PlayerSwim.HeadUnderwater; false on a Core without it (vanilla swimming keeps the head up anyway).</summary>
        public static bool CoreHeadUnderwater
        {
            get
            {
                if (!s_coreLookedUp)
                {
                    s_coreLookedUp = true;
                    var swim = System.Type.GetType("FiresCore.Movement.PlayerSwim, FiresUnifiedCore");
                    s_coreHeadUnderwater = swim?.GetProperty("HeadUnderwater",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    FiresLogger.LogDebug(s_coreHeadUnderwater != null
                        ? "[Diving] player swimming is Core's PlayerSwim; the camera follows the head under the surface when Core says so."
                        : "[Diving] this Core has no PlayerSwim (older than 0.2.298): vanilla swimming, and the camera stays above the water.");
                }
                if (s_coreHeadUnderwater == null) return false;
                try { return (bool)s_coreHeadUnderwater.GetValue(null, null); }
                catch { return false; }
            }
        }
    }
}
