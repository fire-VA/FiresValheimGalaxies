using HarmonyLib;
using UnityEngine;

namespace VerdantsAscent.Diving
{
    /// <summary>
    /// Stops vanilla's camera-above-water clamp from fighting the player while
    /// riding a boat through rough swells, and while swimming underwater.
    ///
    /// Vanilla <c>GameCamera.UpdateCamera</c> forces the camera to stay
    /// <c>m_minWaterDistance</c> (0.3m) above the liquid surface
    /// (assembly_valheim/GameCamera.cs ~245-248: it overwrites end.y to
    /// liquidLevel + m_minWaterDistance and sets m_waterClipping). In rough seas
    /// a wave crest's surface rises above the 3rd-person camera while you're on
    /// a boat, so the clamp snaps the camera UP every frame the crest passes —
    /// the jitter + the hard "straight line across the screen" (the near-clip
    /// flips to its min via m_waterClipping and slices the water at the forced
    /// waterline) that the player reported.
    ///
    /// Fix: while STANDING ON A SHIP, or swimming with the head under the
    /// surface (Core's PlayerSwim, 0.1.30), set m_minWaterDistance to a large
    /// negative — cached and restored — so the clamp can never engage and the
    /// camera rides the boat smoothly through troughs, or follows the swimmer
    /// down. FiresWater is two-sided (its underwater pass)
    /// and FiresUnderwaterEnv drives the underwater fog by the player's eye
    /// depth, so a brief dip reads as a clean underwater glimpse, not a bug.
    ///
    /// Self-registers via the plugin's Harmony.PatchAll.
    /// </summary>
    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    internal static class FiresWaterCameraClampPatch
    {
        public static bool Enabled = true;

        // Large negative => the vanilla "end.y < liquidLevel + m_minWaterDistance"
        // test is effectively never true, so the camera is never clamped up.
        private const float SubmergedMinWaterDistance = -1000f;

        // The vanilla value, captured the first time we override so we can put
        // it back exactly when the condition clears. Null = not currently overridden.
        private static float? s_cachedMinWaterDistance;

        [HarmonyPrefix]
        private static void Prefix(GameCamera __instance)
        {
            if (__instance == null) return;

            var player = Player.m_localPlayer;
            // Release the clamp (let the camera follow under) only when the head
            // is actually under (Core's PlayerSwim says so) or riding a ship — NOT
            // for plain surface treading. Treading keeps vanilla's above-water
            // camera so the view can't flicker between under/over water as waves
            // wash over a surface swimmer.
            bool allow = Enabled
                      && player != null
                      && (FiresDiving.CoreHeadUnderwater || player.GetStandingOnShip() != null);

            if (allow)
            {
                if (!s_cachedMinWaterDistance.HasValue)
                    s_cachedMinWaterDistance = __instance.m_minWaterDistance;
                __instance.m_minWaterDistance = SubmergedMinWaterDistance;
            }
            else if (s_cachedMinWaterDistance.HasValue)
            {
                __instance.m_minWaterDistance = s_cachedMinWaterDistance.Value;
                s_cachedMinWaterDistance = null;
            }
        }
    }
}
