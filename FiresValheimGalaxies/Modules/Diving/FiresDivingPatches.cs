using HarmonyLib;
using UnityEngine;

namespace VerdantsAscent.Diving
{
    /// <summary>
    /// Harmony patches that turn vanilla surface-swimming into real diving.
    /// Ported from the Under-The-Sea mod (UnderTheSeaREF), integrated with our
    /// stack: we do NOT port their fog code (FiresUnderwaterEnv drives the
    /// underwater env by eye depth) or their water-surface flip hack (FiresWater
    /// is already two-sided). The camera follows underwater via
    /// FiresWaterCameraClampPatch (already shipped). Auto-registers via PatchAll.
    ///
    /// Controls while swimming: hold CROUCH to descend, JUMP to rise, RUN to
    /// swim faster. Dropped the ref's UpdateRotation body-pitch patch for v1
    /// (polish + a version-specific signature); diving is fully functional
    /// without it — the body just doesn't tilt to face the dive.
    /// </summary>
    [HarmonyPatch]
    internal static class FiresDivingPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Awake")]
        private static void Player_Awake(Player __instance)
        {
            if (__instance != null && __instance.GetComponent<Diver>() == null)
                __instance.gameObject.AddComponent<Diver>();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "UpdateMotion")]
        private static void UpdateMotion(Character __instance)
        {
            if (!FiresDiving.IsEnvAllowed() || !Diver.TryGet(__instance, out var diver)) return;
            diver.ResetSwimDepthIfNotInWater();
            if (!diver.IsUnderSurface() || !diver.IsInsideLiquid()) return;
            // Keep the game treating us as "swimming, recently grounded" so the
            // submerged body doesn't trip drown / stuck handling.
            diver.player.m_lastGroundTouch = 0.3f;
            diver.player.m_swimTimer = 0f;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "UpdateSwimming")]
        private static void UpdateSwimming_Prefix(Character __instance, float dt, out Vector3? __state)
        {
            __state = null;
            if (!FiresDiving.IsEnvAllowed() || !Diver.TryGet(__instance, out var diver)) return;

            diver.UpdateSwimSpeed(dt);
            bool jump   = ZInput.GetButton("Jump")   || ZInput.GetButton("JoyJump");
            bool crouch = ZInput.GetButton("Crouch") || ZInput.GetButton("JoyCrouch");

            if (jump && diver.IsUnderSurface())      diver.Dive(dt, true,  out __state);   // hold Jump = rise
            else if (crouch && diver.CanDive())      diver.Dive(dt, false, out __state);   // hold Crouch = descend
            else if (diver.IsUnderSurface())         diver.Coast();                        // diving & coasting → hold steady, bob less
            else                                     diver.RideSurface();                  // on the surface → ride the waves like a boat
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "UpdateSwimming")]
        private static void UpdateSwimming_Postfix(Character __instance, ref Vector3? __state)
        {
            if (!__state.HasValue) return;
            __instance.m_moveDir = __state.Value;   // restore the player's real input dir
            __state = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "OnSwimming")]
        private static void OnSwimming(Player __instance, Vector3 targetVel, float dt)
        {
            if (targetVel.magnitude >= 0.1f || !Diver.TryGet(__instance, out var diver)) return;
            if (diver.IsDiving()) { diver.DrainDivingStamina(dt); diver.UpdateSwimSkill(dt); }
            else if (diver.IsRestingInWater()) diver.RegenRestingStamina(dt);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "SetControls")]
        private static void SetControls(Player __instance, ref bool crouch)
        {
            // Crouch is repurposed as "dive" while swimming, so don't let it
            // toggle the sneak state. On land crouch works normally.
            if (__instance == Player.m_localPlayer && FiresDiving.Enabled && __instance.IsSwimming())
                crouch = false;
        }
    }
}
