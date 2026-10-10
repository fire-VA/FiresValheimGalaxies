using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VerdantsAscent.Water.Core;
// FUC infrastructure (canonical async) + the mod's own tagged logger.
using FiresLogger = VerdantsAscent.FiresLogger;
using FrameBudgetScheduler = FiresCore.Async.FrameBudgetScheduler;
using WorkPriority = FiresCore.Async.WorkPriority;

namespace VerdantsAscent.Galaxies
{
    // World-space list of river mouths. Built once at world load, queried per
    // frame by TidalCurrentField to apply swirl. A mouth is where a river meets
    // ocean; the stored direction is the river's outflow, pointing into the ocean.
    internal static class RiverMouthRegistry
    {
        public struct Mouth
        {
            public Vector2 Position;
            public Vector2 OutflowDir;
        }

        private static readonly List<Mouth> Mouths = new();
        private static bool _built;
        private static bool _buildRequested;

        public static IReadOnlyList<Mouth> All => Mouths;

        public static void Clear()
        {
            Mouths.Clear();
            _built = false;
            _buildRequested = false;
        }

        public static void RequestBuild()
        {
            if (_built || _buildRequested) return;
            _buildRequested = true;

            FrameBudgetScheduler.ScheduleCoroutine(WorkPriority.Low, BuildCoroutine(), onComplete: () =>
            {
                _built = true;
                FiresLogger.LogVerbose($"River mouth registry built. {Mouths.Count} mouths.");
            });
        }

        public static void MaintenanceTick()
        {
            // Reserved for periodic refresh as zones load.
        }

        private static IEnumerator BuildCoroutine()
        {
            yield return null;
            yield return ScanCoroutine();
        }

        private static IEnumerator ScanCoroutine()
        {
            float started = Time.realtimeSinceStartup;
            float maxSeconds = GalaxiesConfig.RiverMouthMaxBuildSeconds.Value;
            float cell = Mathf.Max(16f, GalaxiesConfig.RiverMouthScanCellSize.Value);

            const float Half = 10500f;

            int x0 = Mathf.FloorToInt(-Half / cell);
            int x1 = Mathf.CeilToInt (+Half / cell);
            int z0 = Mathf.FloorToInt(-Half / cell);
            int z1 = Mathf.CeilToInt (+Half / cell);

            int processed = 0;
            for (int xi = x0; xi <= x1; xi++)
            {
                for (int zi = z0; zi <= z1; zi++)
                {
                    Vector2 here = new Vector2(xi * cell, zi * cell);
                    if (here.sqrMagnitude > Half * Half) continue;
                    if (!TryClassifyCell(here, out CellKind kind)) continue;
                    if (kind != CellKind.River) continue;

                    if (TryFindOceanNeighbor(here, cell, out Vector2 outflow))
                        Mouths.Add(new Mouth { Position = here, OutflowDir = outflow });

                    if (++processed % 256 == 0)
                    {
                        yield return null;
                        if (Time.realtimeSinceStartup - started > maxSeconds)
                        {
                            FiresLogger.LogWarning(
                                $"River-mouth scan exceeded {maxSeconds:0.0}s. Stopping with {Mouths.Count} mouths found.");
                            yield break;
                        }
                    }
                }
            }

            FiresLogger.LogVerbose($"River-mouth scan completed: {Mouths.Count} mouths.");
        }

        private enum CellKind { Unknown, Land, River, Ocean }

        // Classify a world cell from worldgen: ocean biome, then the exact river-
        // weight function worldgen uses to carve channels, then submerged land.
        private static bool TryClassifyCell(Vector2 xz, out CellKind kind)
        {
            kind = CellKind.Unknown;

            WorldGenerator wg = WorldGenerator.instance;
            if (wg == null)
                return false;

            Heightmap.Biome biome;
            try { biome = wg.GetBiome(xz.x, xz.y); }
            catch { return false; }

            if (biome == Heightmap.Biome.Ocean)
            {
                kind = CellKind.Ocean;
                return true;
            }

            if (WorldGeneratorAccess.GetRiverWeight(wg, xz.x, xz.y, out float weight, out _) && weight > 0f)
            {
                kind = CellKind.River;
                return true;
            }

            float h = wg.GetHeight(xz.x, xz.y);
            float waterLevel = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            kind = h <= waterLevel ? CellKind.River : CellKind.Land;
            return true;
        }

        private static bool TryFindOceanNeighbor(Vector2 here, float cell, out Vector2 outflowDir)
        {
            outflowDir = Vector2.zero;
            Vector2 best = Vector2.zero;
            bool found = false;

            Vector2[] offsets =
            {
                new( 1, 0), new(-1, 0), new(0,  1), new(0, -1),
                new( 1, 1), new(-1, 1), new(1, -1), new(-1,-1),
            };

            foreach (Vector2 o in offsets)
            {
                Vector2 n = here + o * cell;
                if (!TryClassifyCell(n, out CellKind k)) continue;
                if (k != CellKind.Ocean) continue;
                best  += o;
                found = true;
            }

            if (!found || best.sqrMagnitude < 0.01f)
                return false;

            outflowDir = best.normalized;
            return true;
        }
    }
}
