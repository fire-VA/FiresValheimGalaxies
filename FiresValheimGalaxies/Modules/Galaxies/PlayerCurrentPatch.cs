using HarmonyLib;
using UnityEngine;
using FiresLogger = VerdantsAscent.FiresLogger;

namespace VerdantsAscent.Galaxies
{
    // Current-drift for the local player AND owned creatures while in water.
    //
    // Why we modify rb.velocity directly instead of AddForce:
    //   Character resolves walking/swimming inside UpdateMotion and SETS
    //   m_body.velocity each fixed step. An AddForce impulse stacked on top
    //   gets wiped the next frame before it can integrate into visible drift.
    //   Adding the current bias TO velocity after Character set it means
    //   physics integrates the combined velocity for this frame; next frame
    //   Character resets, and we re-add. Position keeps marching.
    //
    // Why we patch UpdateMotion (private):
    //   It runs once per fixed step and is the outermost motion entry —
    //   walking, swimming, flying are all dispatched from here, so a single
    //   postfix covers every Character motion state.
    //
    // Network ownership: applying force only on the owner avoids both clients
    // fighting over a creature's physics and producing rubber-banding.
    [HarmonyPatch(typeof(Character), "UpdateMotion")]
    internal static class PlayerCurrentPatch
    {
        private static void Postfix(Character __instance)
        {
            if (GalaxiesConfig.EnableMod == null || !GalaxiesConfig.EnableMod.Value || !GalaxiesConfig.GalaxiesOwnsOcean) return;
            if (__instance == null || __instance.m_body == null || __instance.m_body.isKinematic) return;

            // Universal skips (apply to every Character, no config gate):
            //   - god / ghost mode: the character is intentionally invulnerable/
            //     ignoring physics; drift would fight that intent.
            //   - flying: airborne characters (debug-fly cheat, valkyries,
            //     flying creatures) shouldn't be dragged by ocean current.
            if (__instance.InGodMode() || __instance.InGhostMode()) return;
            if (__instance.IsFlying()) return;

            if (!__instance.InWater()) return;

            bool isLocalPlayer = (__instance == Player.m_localPlayer);

            if (isLocalPlayer)
            {
                if (!GalaxiesConfig.EnablePlayerCurrents.Value) return;
                // Admin bypass: opt-out for admins who don't want to be pushed
                // while surveying / building — covers ALL current sources,
                // whirlpools included (the toggle means what it says; flip it
                // off to feel whirlpools as admin). God-mode already covers
                // most admin flows; this catches admin activity outside it.
                if (GalaxiesConfig.AdminBypassCurrents.Value
                    && ZNet.instance != null
                    && ZNet.instance.LocalPlayerIsAdminOrHost())
                    return;
            }
            else
            {
                if (!GalaxiesConfig.EnableCreatureCurrents.Value) return;
                // Only the network owner applies physics. Non-owned clients
                // see the result via ZDO sync from the owner.
                var nv = __instance.m_nview;
                if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            }

            Vector3 pos = __instance.transform.position;
            if (!CurrentSystem.TryGetCurrent(pos, out CurrentSample sample)) return;

            float scale = Mathf.Max(0f, GalaxiesConfig.CharacterCurrentScale.Value);
            // Treat current strength as drift VELOCITY (m/s), not acceleration.
            // For ships we use acceleration because ship rigidbodies accumulate
            // momentum against water drag; Character resets velocity each frame
            // so an acceleration-only push never accumulates. Adding velocity
            // bias directly translates to immediate per-frame position drift,
            // matching how a real current carries a swimmer.
            Vector3 bias = sample.Direction * sample.Strength * scale;
            if (bias.sqrMagnitude < 1e-6f) return;

            Vector3 vel = __instance.m_body.velocity;
            vel.x += bias.x;
            vel.z += bias.z;
            __instance.m_body.velocity = vel;

            FiresLogger.LogVerbose(
                $"Character current actor={(isLocalPlayer ? "Player" : __instance.name)} " +
                $"src={sample.Source} str={sample.Strength:0.00} bias={bias}");
        }
    }
}
