namespace VerdantsAscent.Diving
{
    /// <summary>
    /// Settings + environment gate for the diving system. Values are pushed
    /// from ConfigManager by FiresAdminTerrain.ApplyFiresWaterConfig (same
    /// live-config pattern as the water tuning). Approach + defaults adapted
    /// from the Under-The-Sea mod (saved as UnderTheSeaREF), integrated with
    /// our FiresUnderwaterEnv (fog/color) and two-sided FiresWater shader so we
    /// DON'T duplicate their fog code or their water-surface-flip hack.
    /// </summary>
    public static class FiresDiving
    {
        public static bool  Enabled                 = true;
        public static float MaxSwimSpeed            = 3.0f;  // peak sprint-swim speed
        public static float UnderwaterStaminaDrain  = 0.5f;  // x the default swim drain while floating
        public static float RestingStaminaRegenDelay = 2.0f; // x the default delay while treading
        public static float RestingStaminaRegenRate  = 0.5f; // x the default regen rate while treading

        /// <summary>
        /// Diving is allowed everywhere except the SunkenCrypt dungeon (its
        /// "water" is a flooded interior, not a divable body) — matches
        /// Under-The-Sea. Also the master on/off.
        /// </summary>
        public static bool IsEnvAllowed()
        {
            if (!Enabled) return false;
            if (EnvMan.instance == null) return true;
            var env = EnvMan.instance.GetCurrentEnvironment();
            return env == null || env.m_name != "SunkenCrypt";
        }
    }
}
