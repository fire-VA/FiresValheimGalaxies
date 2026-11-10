namespace VerdantsAscent.Diving
{
    /// <summary>
    /// Player swimming is Core's (FiresUnifiedCore 0.2.298, FiresCore.Movement.PlayerSwim; Fire 10-05: "it should be moved to core like
    /// crawling and climbing"). Its [Swimming] setting is Auto (on when Galaxies is installed), On or Off; Off is vanilla swimming.
    /// Galaxies keeps no player diving of its own. What it still needs from Core is one answer, read by reflection so Galaxies loads on
    /// any Core: is the local player's head under the surface (the camera may then follow it down, FiresWaterCameraClampPatch).
    /// Creature diving lives in FiresMonsterOverhaul (0.1.68: Galaxies keeps none).
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

        // 0.1.69 ([seasons] + [wishbone], Fire 10-30: "Advanced player movement is getting our swimming and swimming configs"): Core
        // 0.2.1047+ PlayerSwim.CameraOwned = Core's own swim camera (APM installed, swim on, APM's "Camera follows you underwater" on)
        // handles the head-under case, so Galaxies' clamp patch keeps only its ship branch. A Core without the property: false (as before).
        private static System.Reflection.PropertyInfo s_cameraOwned;
        private static System.Reflection.FieldInfo s_cameraOwnedField;
        private static bool s_ownedLookedUp, s_ownedLogged;
        public static bool CoreCameraOwned
        {
            get
            {
                if (!s_ownedLookedUp)
                {
                    s_ownedLookedUp = true;
                    var swim = System.Type.GetType("FiresCore.Movement.PlayerSwim, FiresUnifiedCore");
                    var fl = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
                    s_cameraOwned = swim?.GetProperty("CameraOwned", fl);
                    if (s_cameraOwned == null) s_cameraOwnedField = swim?.GetField("CameraOwned", fl);
                }
                bool v = false;
                try { v = s_cameraOwned != null ? (bool)s_cameraOwned.GetValue(null, null) : s_cameraOwnedField != null && (bool)s_cameraOwnedField.GetValue(null); }
                catch { v = false; }
                if (v && !s_ownedLogged) { s_ownedLogged = true; FiresLogger.LogDebug("[Diving] Core's swim camera owns the head-under case (APM); Galaxies keeps only the ship clamp"); }
                return v;
            }
        }
    }
}
