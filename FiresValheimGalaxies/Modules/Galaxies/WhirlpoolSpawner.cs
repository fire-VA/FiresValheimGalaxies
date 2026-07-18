using System;
using System.Collections.Generic;
using UnityEngine;
using FiresLogger = VerdantsAscent.FiresLogger;

namespace VerdantsAscent.Galaxies
{
    /// <summary>
    /// Storm-gated whirlpool spawner — DETERMINISTIC, so it needs no networking at all.
    /// Every client hashes (world name, 64m zone coords, time block) and gets the same
    /// answer, and time comes from ZNet's synced clock — so all peers near the same ocean
    /// agree on where and when a whirlpool exists, the same trick worldgen itself relies
    /// on. Zones only spawn while a configured STORM environment is active, in Ocean-biome
    /// water deep enough to sell the effect, and they swell in / die down across their
    /// block lifetime instead of popping.
    /// </summary>
    internal static class WhirlpoolSpawner
    {
        private const float TickInterval = 1f;
        private const float ZoneSize = 64f;       // vanilla zone grid
        private const float RampSeconds = 20f;    // grow-in / die-down window

        private static float _nextTick;
        private static int _worldHash;
        private static bool _worldHashed;
        private static readonly HashSet<long> _activeBlocks = new HashSet<long>();

        public static void Tick()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickInterval;

            if (!GalaxiesConfig.WhirlpoolsEnabled.Value || ZNet.instance == null ||
                Player.m_localPlayer == null || WorldGenerator.instance == null)
            {
                RemoveManaged();
                return;
            }

            // Storm gate — whirlpools exist only while a listed environment is active.
            string env = EnvMan.instance != null && EnvMan.instance.GetCurrentEnvironment() != null
                ? EnvMan.instance.GetCurrentEnvironment().m_name : "";
            bool stormy = false;
            string[] storms = GalaxiesConfig.WhirlpoolStormEnvironments.Value.Split(',');
            for (int i = 0; i < storms.Length; i++)
                if (!string.IsNullOrEmpty(storms[i]) && string.Equals(storms[i].Trim(), env, StringComparison.OrdinalIgnoreCase))
                { stormy = true; break; }

            double now = ZNet.instance.GetTimeSeconds();
            float blockLen = Mathf.Max(GalaxiesConfig.WhirlpoolBlockSeconds.Value, 60f);
            long block = (long)(now / blockLen);
            float blockT = (float)(now - block * (double)blockLen);   // seconds into the block

            if (!_worldHashed)
            {
                try { _worldHash = ZNet.instance.GetWorldName().GetStableHashCode(); }
                catch { _worldHash = 1337; }
                _worldHashed = true;
            }

            // Scan the zone grid around the local player; each zone rolls its own hash for
            // this block. Deterministic — every client computes the identical set.
            Vector3 pPos = Player.m_localPlayer.transform.position;
            float scan = GalaxiesConfig.WhirlpoolScanRadius.Value;
            int zMinX = Mathf.FloorToInt((pPos.x - scan) / ZoneSize);
            int zMaxX = Mathf.FloorToInt((pPos.x + scan) / ZoneSize);
            int zMinZ = Mathf.FloorToInt((pPos.z - scan) / ZoneSize);
            int zMaxZ = Mathf.FloorToInt((pPos.z + scan) / ZoneSize);

            _activeBlocks.Clear();
            int active = CountManaged();
            float chance = Mathf.Clamp01(GalaxiesConfig.WhirlpoolSpawnChance.Value);

            for (int zx = zMinX; zx <= zMaxX; zx++)
            {
                for (int zz = zMinZ; zz <= zMaxZ; zz++)
                {
                    long key = ZoneBlockKey(zx, zz, block);
                    uint h = Hash(_worldHash, zx, zz, block);
                    if ((h & 0xFFFFF) / (float)0xFFFFF >= chance) continue;   // no whirlpool rolled here

                    // Jitter the centre inside the zone so it doesn't sit on the grid.
                    float jx = ((h >> 8 & 0xFF) / 255f - 0.5f) * (ZoneSize - 16f);
                    float jz = ((h >> 16 & 0xFF) / 255f - 0.5f) * (ZoneSize - 16f);
                    float wx = (zx + 0.5f) * ZoneSize + jx;
                    float wz = (zz + 0.5f) * ZoneSize + jz;

                    // Ocean biome + enough depth for the funnel to make sense.
                    if (WorldGenerator.instance.GetBiome(wx, wz) != Heightmap.Biome.Ocean) continue;
                    float ground = WorldGenerator.instance.GetHeight(wx, wz);
                    if (WaterUtil.OceanBaselineY() - ground < GalaxiesConfig.WhirlpoolMinOceanDepth.Value) continue;

                    if (!stormy) continue;                 // rolled a spot, but no storm running
                    if (active >= GalaxiesConfig.WhirlpoolMaxActive.Value && !HasManaged(key)) continue;

                    _activeBlocks.Add(key);
                    WhirlpoolZone zone = FindManaged(key);
                    if (zone == null)
                    {
                        zone = new WhirlpoolZone
                        {
                            Center = new Vector3(wx, WaterUtil.GetWaterLevel(new Vector3(wx, 0f, wz)), wz),
                            OuterRadius = GalaxiesConfig.WhirlpoolOuterRadius.Value,
                            CoreRadius = GalaxiesConfig.WhirlpoolCoreRadius.Value,
                            Clockwise = (h & 1) == 0,
                            BlockId = key,
                        };
                        WhirlpoolSystem.Zones.Add(zone);
                        active++;
                        FiresLogger.LogInfo($"Storm whirlpool spawned at ({wx:F0},{wz:F0}) r={zone.OuterRadius:F0}m env='{env}' block={block}");
                    }

                    // Swell in at block start, die down at block end.
                    float ramp = Mathf.Min(blockT / RampSeconds, (blockLen - blockT) / RampSeconds);
                    zone.LifeScale = Mathf.Clamp01(ramp);
                    zone.Center.y = WaterUtil.GetWaterLevel(zone.Center);   // track the tide
                }
            }

            // Retire managed zones whose block ended, moved out of scan range, or storm passed.
            for (int i = WhirlpoolSystem.Zones.Count - 1; i >= 0; i--)
            {
                var z = WhirlpoolSystem.Zones[i];
                if (z.Manual) continue;
                if (!_activeBlocks.Contains(z.BlockId))
                {
                    z.LifeScale -= Time.deltaTime * 5f;   // quick fade for orphaned zones
                    if (z.LifeScale <= 0f)
                    {
                        WhirlpoolSystem.Zones.RemoveAt(i);
                        FiresLogger.LogVerbose($"Storm whirlpool retired at ({z.Center.x:F0},{z.Center.z:F0})");
                    }
                }
            }
        }

        private static long ZoneBlockKey(int zx, int zz, long block)
            => ((long)(zx & 0xFFFF) << 48) | ((long)(zz & 0xFFFF) << 32) | (block & 0xFFFFFFFFL);

        private static uint Hash(int world, int zx, int zz, long block)
        {
            unchecked
            {
                uint h = (uint)world * 2654435761u;
                h ^= (uint)zx * 73856093u; h = (h << 13) | (h >> 19);
                h ^= (uint)zz * 19349663u; h = (h << 7) | (h >> 25);
                h ^= (uint)block * 83492791u; h *= 2246822519u;
                return h ^ (h >> 15);
            }
        }

        private static int CountManaged()
        {
            int n = 0;
            for (int i = 0; i < WhirlpoolSystem.Zones.Count; i++)
                if (!WhirlpoolSystem.Zones[i].Manual) n++;
            return n;
        }

        private static bool HasManaged(long key) => FindManaged(key) != null;

        private static WhirlpoolZone FindManaged(long key)
        {
            for (int i = 0; i < WhirlpoolSystem.Zones.Count; i++)
                if (!WhirlpoolSystem.Zones[i].Manual && WhirlpoolSystem.Zones[i].BlockId == key)
                    return WhirlpoolSystem.Zones[i];
            return null;
        }

        private static void RemoveManaged()
        {
            for (int i = WhirlpoolSystem.Zones.Count - 1; i >= 0; i--)
                if (!WhirlpoolSystem.Zones[i].Manual)
                    WhirlpoolSystem.Zones.RemoveAt(i);
        }
    }
}
