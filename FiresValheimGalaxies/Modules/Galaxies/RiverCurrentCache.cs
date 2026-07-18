using System.Collections.Generic;
using UnityEngine;
using FiresLogger = VerdantsAscent.FiresLogger;

namespace VerdantsAscent.Galaxies
{
    internal readonly struct RiverCellKey
    {
        public readonly int X;
        public readonly int Z;

        public RiverCellKey(int x, int z)
        {
            X = x;
            Z = z;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is RiverCellKey other && other.X == X && other.Z == Z;
        }

        public override string ToString()
        {
            return $"{X},{Z}";
        }
    }

    internal sealed class RiverCellData
    {
        public bool IsRiver;
        public Vector3 Direction;
        public float Strength;
        public float Depth;
        public float LastUsedTime;
    }

    // Caches a river-flow decision per (X, Z) grid cell. Cells expire after a
    // configurable idle time so the cache does not grow without bound.
    internal static class RiverCurrentCache
    {
        private static readonly Dictionary<RiverCellKey, RiverCellData> Cache = new();

        public static void Clear()
        {
            Cache.Clear();
        }

        public static bool TryGetCachedRiverCurrent(Vector3 pos, out Vector3 direction, out float strength)
        {
            direction = Vector3.zero;
            strength  = 0f;

            float cellSize = Mathf.Max(4f, GalaxiesConfig.RiverCacheCellSize.Value);

            if (GalaxiesConfig.RiverBilinearBlend.Value)
                return TryGetBlended(pos, cellSize, out direction, out strength);

            RiverCellData data = GetOrScan(pos, cellSize);
            if (!data.IsRiver)
                return false;

            direction = data.Direction;
            strength  = data.Strength;
            return true;
        }

        private static bool TryGetBlended(Vector3 pos, float cellSize, out Vector3 direction, out float strength)
        {
            direction = Vector3.zero;
            strength  = 0f;

            float fx = pos.x / cellSize - 0.5f;
            float fz = pos.z / cellSize - 0.5f;

            int x0 = Mathf.FloorToInt(fx);
            int z0 = Mathf.FloorToInt(fz);

            float tx = fx - x0;
            float tz = fz - z0;

            float w00 = (1f - tx) * (1f - tz);
            float w10 = tx       * (1f - tz);
            float w01 = (1f - tx) * tz;
            float w11 = tx       * tz;

            RiverCellData d00 = GetOrScanByKey(new RiverCellKey(x0,     z0),     cellSize, pos.y);
            RiverCellData d10 = GetOrScanByKey(new RiverCellKey(x0 + 1, z0),     cellSize, pos.y);
            RiverCellData d01 = GetOrScanByKey(new RiverCellKey(x0,     z0 + 1), cellSize, pos.y);
            RiverCellData d11 = GetOrScanByKey(new RiverCellKey(x0 + 1, z0 + 1), cellSize, pos.y);

            Vector3 dir =
                Contribution(d00) * w00 +
                Contribution(d10) * w10 +
                Contribution(d01) * w01 +
                Contribution(d11) * w11;

            float str =
                (d00.IsRiver ? d00.Strength : 0f) * w00 +
                (d10.IsRiver ? d10.Strength : 0f) * w10 +
                (d01.IsRiver ? d01.Strength : 0f) * w01 +
                (d11.IsRiver ? d11.Strength : 0f) * w11;

            if (dir.sqrMagnitude < 1e-6f || str < 1e-3f)
                return false;

            direction = dir.normalized;
            strength  = str;
            return true;
        }

        private static Vector3 Contribution(RiverCellData d)
        {
            if (!d.IsRiver) return Vector3.zero;
            return d.Direction * d.Strength;
        }

        private static RiverCellData GetOrScan(Vector3 pos, float cellSize)
        {
            RiverCellKey key = GetKey(pos, cellSize);
            return GetOrScanByKey(key, cellSize, pos.y);
        }

        private static RiverCellData GetOrScanByKey(RiverCellKey key, float cellSize, float yHint)
        {
            if (!Cache.TryGetValue(key, out RiverCellData data))
            {
                Vector3 cellCenter = GetCellCenter(key, cellSize, yHint);
                data = ScanRiverCell(cellCenter);
                Cache[key] = data;

                FiresLogger.LogVerbose(
                    $"Scanned river cell {key}: river={data.IsRiver}, dir={data.Direction}, depth={data.Depth:0.00}, strength={data.Strength:0.00}");
            }

            data.LastUsedTime = Time.time;
            return data;
        }

        private static RiverCellKey GetKey(Vector3 pos, float cellSize)
        {
            int x = Mathf.FloorToInt(pos.x / cellSize);
            int z = Mathf.FloorToInt(pos.z / cellSize);
            return new RiverCellKey(x, z);
        }

        private static Vector3 GetCellCenter(RiverCellKey key, float cellSize, float yHint)
        {
            return new Vector3(
                key.X * cellSize + cellSize * 0.5f,
                yHint,
                key.Z * cellSize + cellSize * 0.5f
            );
        }

        private static RiverCellData ScanRiverCell(Vector3 pos)
        {
            RiverCellData data = new RiverCellData
            {
                IsRiver = false,
                Direction = Vector3.zero,
                Strength = 0f,
                Depth = 0f,
                LastUsedTime = Time.time
            };

            if (!WaterUtil.TryGetGroundY(pos, out float centerGroundY))
                return data;

            float waterY = WaterUtil.GetWaterLevel(pos);
            float depth  = waterY - centerGroundY;

            data.Depth = depth;

            if (depth < GalaxiesConfig.RiverMinDepth.Value || depth > GalaxiesConfig.RiverMaxDepth.Value)
                return data;

            // Preferred path: vanilla WorldGenerator river/stream segments give
            // the true along-channel tangent (with curve correction). Slope-
            // derived direction at a riverbank points across the channel, not
            // along it — the visible "streamlines cross the riverbed sideways"
            // bug. Worldgen segments fix that.
            Vector3 flowDir;
            float widthFactor;
            if (WorldgenRivers.TryGetFlow(pos, out Vector3 segDir, out float wf))
            {
                flowDir = segDir;
                widthFactor = wf;
            }
            else
            {
                // Fallback: slope-based detection for points worldgen doesn't
                // classify as river (Better Continents heightmap-only rivers,
                // hand-modified terrain channels, etc.). Same gates as before:
                // minimum slope + bank confirmation to reject sloping beaches.
                if (!TryCalculateSlopeFlow(pos, centerGroundY, out Vector3 slopeFlowDir, out float slopeMagnitude))
                    return data;

                if (slopeMagnitude < GalaxiesConfig.RiverMinSlopeMagnitude.Value)
                    return data;

                if (!ConfirmRiverBanks(pos, waterY, slopeFlowDir))
                    return data;

                flowDir = slopeFlowDir;
                widthFactor = 1f;
            }

            data.IsRiver   = true;
            data.Direction = flowDir;

            // Strength scales by depth (deeper = stronger) and by proximity to
            // centerline (1 at center, taper toward bank).
            float depthT = Mathf.InverseLerp(GalaxiesConfig.RiverMinDepth.Value, GalaxiesConfig.RiverMaxDepth.Value, depth);
            float baseStr = Mathf.Lerp(GalaxiesConfig.RiverCurrentMinStrength.Value, GalaxiesConfig.RiverCurrentMaxStrength.Value, depthT);
            data.Strength = baseStr * Mathf.Clamp01(widthFactor);

            return data;
        }

        // Estimate downhill flow from 4 cardinal samples (flow = -gradient). Scan
        // radius should be wide enough that channel tilt outweighs bank-to-bank slope.
        private static bool TryCalculateSlopeFlow(Vector3 pos, float centerGroundY, out Vector3 flowDir, out float slopeMagnitude)
        {
            flowDir        = Vector3.zero;
            slopeMagnitude = 0f;

            float radius = Mathf.Max(8f, GalaxiesConfig.RiverScanRadius.Value);

            if (!WaterUtil.TryGetGroundY(pos + Vector3.right   * radius, out float yPosX)) return false;
            if (!WaterUtil.TryGetGroundY(pos + Vector3.left    * radius, out float yNegX)) return false;
            if (!WaterUtil.TryGetGroundY(pos + Vector3.forward * radius, out float yPosZ)) return false;
            if (!WaterUtil.TryGetGroundY(pos + Vector3.back    * radius, out float yNegZ)) return false;

            float dhdx = (yPosX - yNegX) / (2f * radius);
            float dhdz = (yPosZ - yNegZ) / (2f * radius);

            Vector3 gradient = new Vector3(dhdx, 0f, dhdz);
            slopeMagnitude   = gradient.magnitude;

            if (slopeMagnitude < 1e-5f)
                return false;

            flowDir = (-gradient).normalized;
            return true;
        }

        private static bool ConfirmRiverBanks(Vector3 pos, float waterY, Vector3 flowDir)
        {
            Vector3 perp = new Vector3(-flowDir.z, 0f, flowDir.x).normalized;
            float probe = Mathf.Max(4f, GalaxiesConfig.RiverCacheCellSize.Value * 0.25f);
            float minDepth = GalaxiesConfig.RiverBankConfirmDepth.Value;

            return SideIsWet(pos + perp * probe, waterY, minDepth) ||
                   SideIsWet(pos - perp * probe, waterY, minDepth);
        }

        private static bool SideIsWet(Vector3 samplePos, float waterY, float minDepth)
        {
            if (!WaterUtil.TryGetGroundY(samplePos, out float groundY))
                return false;
            return (waterY - groundY) >= minDepth;
        }

        public static void Cleanup(float maxAgeSeconds)
        {
            if (Cache.Count == 0)
                return;

            float now = Time.time;
            List<RiverCellKey> remove = null;

            foreach (KeyValuePair<RiverCellKey, RiverCellData> pair in Cache)
            {
                if (now - pair.Value.LastUsedTime > maxAgeSeconds)
                {
                    remove ??= new List<RiverCellKey>();
                    remove.Add(pair.Key);
                }
            }

            if (remove == null)
                return;

            foreach (RiverCellKey key in remove)
                Cache.Remove(key);

            FiresLogger.LogVerbose($"Cleaned {remove.Count} river cache cells. Remaining={Cache.Count}");
        }
    }
}
