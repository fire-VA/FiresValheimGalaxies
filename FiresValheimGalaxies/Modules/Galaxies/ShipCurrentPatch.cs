using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using FiresLogger = VerdantsAscent.FiresLogger;

namespace VerdantsAscent.Galaxies
{
    // Patches Ship.CustomFixedUpdate — vanilla's per-fixed-step entry point
    // where the ship resolves sail/rudder/buoyancy. Patching plain FixedUpdate
    // would fail to bind (it doesn't exist on Ship; vanilla drives the ship
    // physics through this public CustomFixedUpdate instead).
    [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
    internal static class ShipCurrentPatch
    {
        private sealed class SmoothingEntry
        {
            public Vector3 Direction;
            public float LastUpdate;
        }

        private static readonly Dictionary<int, SmoothingEntry> Smoothed = new();

        public static void ClearSmoothing()
        {
            Smoothed.Clear();
        }

        public static void PruneStaleSmoothing()
        {
            if (Smoothed.Count == 0)
                return;

            float now = Time.time;
            List<int> remove = null;

            foreach (KeyValuePair<int, SmoothingEntry> pair in Smoothed)
            {
                if (now - pair.Value.LastUpdate > 30f)
                {
                    remove ??= new List<int>();
                    remove.Add(pair.Key);
                }
            }

            if (remove == null)
                return;

            foreach (int id in remove)
                Smoothed.Remove(id);

            FiresLogger.LogVerbose($"Pruned {remove.Count} ship smoothing entries. Remaining={Smoothed.Count}");
        }

        private static void Postfix(Ship __instance)
        {
            if (!GalaxiesConfig.EnableMod.Value || !GalaxiesConfig.EnableShipCurrents.Value || !GalaxiesConfig.GalaxiesOwnsOcean)
                return;

            if (__instance == null || __instance.m_body == null)
                return;

            if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
                return;

            // Admin bypass: ship owned by the local admin/host gets no current force
            // AT ALL — including whirlpools. The toggle means what it says; an admin
            // who wants to feel whirlpools turns AdminBypassCurrents off (user call,
            // 2026-07-07 — an earlier build special-cased whirlpools past the bypass
            // and it was reverted). Only applies to ships THIS client owns —
            // remote-owned ships driven by non-admin players are unaffected.
            if (GalaxiesConfig.AdminBypassCurrents.Value
                && ZNet.instance != null
                && ZNet.instance.LocalPlayerIsAdminOrHost())
                return;

            Vector3 pos = __instance.transform.position;

            if (!WaterUtil.IsNearWaterSurface(pos, GalaxiesConfig.ShipWaterHeightTolerance.Value))
                return;

            if (!CurrentSystem.TryGetCurrent(pos, out CurrentSample sample))
                return;

            int id = __instance.GetInstanceID();
            Vector3 currentDir = SmoothDirection(id, sample.Direction, GalaxiesConfig.CurrentDirectionSmoothing.Value);
            if (currentDir.sqrMagnitude < 1e-4f)
                return;

            ApplyShipCurrent(__instance, currentDir, sample.Strength, sample.Source);
        }

        private static Vector3 SmoothDirection(int shipId, Vector3 targetDir, float lerpSpeed)
        {
            targetDir.y = 0f;
            if (targetDir.sqrMagnitude < 1e-4f)
                return Vector3.zero;
            targetDir.Normalize();

            if (!Smoothed.TryGetValue(shipId, out SmoothingEntry entry) || entry.Direction.sqrMagnitude < 1e-4f)
            {
                entry = new SmoothingEntry { Direction = targetDir, LastUpdate = Time.time };
                Smoothed[shipId] = entry;
                return targetDir;
            }

            float t = Time.fixedDeltaTime * Mathf.Max(0.1f, lerpSpeed);
            Vector3 next = Vector3.Slerp(entry.Direction, targetDir, t);
            next.y = 0f;
            if (next.sqrMagnitude < 1e-6f)
                next = targetDir;
            else
                next.Normalize();

            entry.Direction  = next;
            entry.LastUpdate = Time.time;
            return next;
        }

        private static void ApplyShipCurrent(Ship ship, Vector3 currentDir, float baseStrength, string source)
        {
            Rigidbody rb = ship.m_body;

            Vector3 velocity     = rb.velocity;
            Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);

            Vector3 shipForward = ship.transform.forward;
            shipForward.y = 0f;
            shipForward.Normalize();

            Vector3 windDir = Vector3.zero;
            if (EnvMan.instance != null)
            {
                windDir = EnvMan.instance.GetWindDir();
                windDir.y = 0f;
                if (windDir.sqrMagnitude > 1e-4f)
                    windDir.Normalize();
                else
                    windDir = Vector3.zero;
            }

            float windAlignment = windDir != Vector3.zero ? Vector3.Dot(shipForward, windDir) : 0f;

            float strength = baseStrength;

            if (windAlignment < GalaxiesConfig.WindAgainstThreshold.Value)
            {
                strength *= GalaxiesConfig.BadWindCurrentMultiplier.Value;
                ApplyAdverseDrag(rb, flatVelocity, currentDir);
            }

            float speedAlongCurrent = Vector3.Dot(flatVelocity, currentDir);
            float speedLimit        = Mathf.Max(0.1f, GalaxiesConfig.MaxCurrentSpeedContribution.Value);
            float speedFactor       = Mathf.Clamp01(1f - Mathf.Max(0f, speedAlongCurrent) / speedLimit);

            Vector3 force = currentDir * strength * speedFactor;
            rb.AddForce(force, ForceMode.Acceleration);

            FiresLogger.LogVerbose(
                $"Ship current src={source} str={strength:0.00} windAlign={windAlignment:0.00} speedAlong={speedAlongCurrent:0.00} force={force}");
        }

        private static void ApplyAdverseDrag(Rigidbody rb, Vector3 flatVelocity, Vector3 currentDir)
        {
            if (flatVelocity.sqrMagnitude <= 0.01f)
                return;

            Vector3 flatVelDir   = flatVelocity.normalized;
            float currentVsMotion = Vector3.Dot(flatVelDir, currentDir);

            if (currentVsMotion < -0.25f)
            {
                rb.AddForce(-flatVelDir * GalaxiesConfig.AgainstCurrentDrag.Value, ForceMode.Acceleration);
            }
            else if (Mathf.Abs(currentVsMotion) <= 0.25f)
            {
                rb.AddForce(-flatVelDir * GalaxiesConfig.CrossCurrentDrag.Value, ForceMode.Acceleration);
            }
        }
    }
}
