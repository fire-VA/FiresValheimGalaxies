using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using FiresLogger = VerdantsAscent.FiresLogger;

namespace VerdantsAscent.Galaxies
{
    /// <summary>
    /// The two static spatial fields that shape the water surface, computed purely from
    /// worldgen (biome + terrain height + river geometry) so they are IDENTICAL on every
    /// client and on the headless server — no shader required. That is the whole reason we
    /// don't rework buoyancy: the server can compute a number to shift the surface by, it
    /// just can't run our shader.
    ///
    ///   • <see cref="TideInfluence"/> (feature A) — 1 in open ocean, <c>RiverTideFraction</c>
    ///     in rivers/swamps, smoothly blended at the mouth. Multiplies the tide offset so
    ///     rivers don't drain at low tide.
    ///   • <see cref="RiverRaise"/> (feature B) — a bank-SAFE upward bend of the river surface:
    ///     the raise is clamped below the sampled banks (minus a margin), so a river whose banks
    ///     can't hold it simply isn't raised there. No floods by construction.
    ///
    /// Both the FiresWater visual bake and the <see cref="WaterVolumeTidePatch"/> buoyancy weld
    /// read these SAME functions, so the rendered surface and the surface you float on stay
    /// locked together. Values are cached per grid cell — biome/terrain/rivers never change for a
    /// loaded world, so each cell computes once.
    /// </summary>
    internal static class TideRiverField
    {
        private const float CellSize = 2f;   // cache granularity (m)

        private readonly struct Cell
        {
            public readonly float Influence;   // tide multiplier 0..1
            public readonly float Raise;       // metres of upward bend (>= 0)
            public Cell(float influence, float raise) { Influence = influence; Raise = raise; }
        }

        private static readonly Dictionary<long, Cell> s_cache = new();

        public static void Clear() => s_cache.Clear();

        public static float TideInfluence(float x, float z) => Get(x, z).Influence;
        public static float RiverRaise(float x, float z) => Get(x, z).Raise;

        private static long Key(float x, float z)
        {
            long cx = Mathf.FloorToInt(x / CellSize);
            long cz = Mathf.FloorToInt(z / CellSize);
            return ((cx & 0xFFFFFFFFL) << 32) | (cz & 0xFFFFFFFFL);
        }

        private static Cell Get(float x, float z)
        {
            long k = Key(x, z);
            if (s_cache.TryGetValue(k, out Cell c)) return c;
            // Bound the cache so a long voyage can't grow it without limit; a wipe just
            // means the next few queries recompute (cheap, and the values are static).
            if (s_cache.Count > 200000) s_cache.Clear();
            c = Compute(x, z);
            s_cache[k] = c;
            return c;
        }

        private static Cell Compute(float x, float z)
        {
            var wg = WorldGenerator.instance;
            if (wg == null) return new Cell(1f, 0f);

            float influence = 1f;
            if (GalaxiesConfig.TideShapingEnabled != null && GalaxiesConfig.TideShapingEnabled.Value)
            {
                float frac  = Mathf.Clamp01(GalaxiesConfig.RiverTideFraction.Value);
                float blend = Mathf.Max(1f, GalaxiesConfig.TideMouthBlend.Value);
                // Smooth ocean fraction from a 5-tap biome sample (centre + 4 at the blend
                // radius): full ocean around → 1 → full tide; inland water → 0 → the fraction.
                int ocean = (IsOcean(wg, x, z) ? 1 : 0)
                          + (IsOcean(wg, x + blend, z) ? 1 : 0)
                          + (IsOcean(wg, x - blend, z) ? 1 : 0)
                          + (IsOcean(wg, x, z + blend) ? 1 : 0)
                          + (IsOcean(wg, x, z - blend) ? 1 : 0);
                influence = Mathf.Lerp(frac, 1f, ocean / 5f);
            }

            float raise = 0f;
            if (GalaxiesConfig.ExtendedRiversEnabled != null && GalaxiesConfig.ExtendedRiversEnabled.Value
                && WorldgenRivers.TryGetChannel(new Vector3(x, 0f, z), out _, out _, out float halfWidth, out float widthFactor)
                && halfWidth > 0.5f)
            {
                float bankMin = LowestContainingBank(wg, x, z, halfWidth);
                if (bankMin < float.MaxValue)
                {
                    float seaY   = WaterUtil.OceanBaselineY();
                    float margin = GalaxiesConfig.RiverBankMargin.Value;
                    float cap    = GalaxiesConfig.RiverMaxClimb.Value;
                    // Never rise above (lowest surrounding land - margin); never above the hard cap.
                    float safe = Mathf.Min(cap, (bankMin - margin) - seaY);
                    if (safe > 0f) raise = safe * widthFactor;   // taper the bulge back to Y30 at the edges
                }
            }

            return new Cell(influence, raise);
        }

        private const int RingSamples = 12;

        /// <summary>Winding-agnostic bank containment: sample terrain in a ring around the point
        /// at the channel edge; a sample that is STILL inside a river channel is the river's own
        /// path (a bend or its length ahead/behind), not a bank, so it's skipped — everything else
        /// is land that water could escape over, and the LOWEST such land caps how high we may
        /// safely raise. Returns <see cref="float.MaxValue"/> when no containing land is found in
        /// range (an open mouth, or a channel wider than the ring) → don't raise there.</summary>
        private static float LowestContainingBank(WorldGenerator wg, float x, float z, float halfWidth)
        {
            float widen = Mathf.Max(1f, GalaxiesConfig.RiverBankSampleWiden.Value);
            float r = halfWidth * widen;
            float lowest = float.MaxValue;
            for (int k = 0; k < RingSamples; k++)
            {
                float a = k * (Mathf.PI * 2f / RingSamples);
                float sx = x + Mathf.Cos(a) * r;
                float sz = z + Mathf.Sin(a) * r;
                if (WorldgenRivers.TryGetChannel(new Vector3(sx, 0f, sz), out _, out _, out _, out _))
                    continue;   // still river that way — not a bank
                float h = wg.GetHeight(sx, sz);
                if (h < lowest) lowest = h;
            }
            return lowest;
        }

        private static bool IsOcean(WorldGenerator wg, float x, float z)
            => wg.GetBiome(x, z) == Heightmap.Biome.Ocean;

        // Full diagnostic at a point (banks + safety math) for the console — recomputes
        // rather than reading the cache so it always reflects the current config.
        public static string Describe(Vector3 pos)
        {
            var wg = WorldGenerator.instance;
            if (wg == null) return "[river] no WorldGenerator";
            float seaY = WaterUtil.OceanBaselineY();
            string biome = wg.GetBiome(pos.x, pos.z).ToString();
            float infl = TideInfluence(pos.x, pos.z);

            // Ground truth: vanilla's own river-carve weight (covers rivers AND streams exactly
            // as the terrain is cut). Shown alongside the segment-index result so we can see when
            // the two disagree — segment index sparse vs a channel that IS carved here.
            VerdantsAscent.Water.Core.WorldGeneratorAccess.GetRiverWeight(wg, pos.x, pos.z, out float rw, out float rwWidth);
            string gt = $"vanillaRiverWeight={rw:F2} width={rwWidth:F0}";

            if (!WorldgenRivers.TryGetChannel(pos, out _, out _, out float halfWidth, out float widthFactor))
                return $"[river] biome={biome} tideInfluence={infl:F2} | {gt} | segments={WorldgenRivers.SegmentCount} — not in a segment-index channel (raise=0)";

            float bankMin = LowestContainingBank(wg, pos.x, pos.z, halfWidth);
            float cap = GalaxiesConfig.RiverMaxClimb.Value, margin = GalaxiesConfig.RiverBankMargin.Value;
            string bankStr = bankMin < float.MaxValue ? $"{bankMin:F1}" : "none-in-range";
            float safe = bankMin < float.MaxValue ? Mathf.Min(cap, (bankMin - margin) - seaY) : 0f;
            float raise = safe > 0f ? safe * widthFactor : 0f;
            bool on = GalaxiesConfig.ExtendedRiversEnabled != null && GalaxiesConfig.ExtendedRiversEnabled.Value;

            return $"[river] biome={biome} tideInfluence={infl:F2} | channel halfW={halfWidth:F0}m widthFactor={widthFactor:F2} "
                 + $"lowestBank(ring)={bankStr} seaY={seaY:F0} cap={cap:F0} margin={margin:F1} "
                 + $"=> raise={raise:F2}m {(on ? "" : "(ExtendedRivers OFF — physics not applied)")}";
        }

        // Sweep a grid around the player using vanilla's GetRiverWeight (the exact river/stream
        // carve function) to find the NEAREST actual channel — answers "are there rivers here at
        // all, and where?" independent of our segment index.
        public static string Scan(Vector3 pos)
        {
            var wg = WorldGenerator.instance;
            if (wg == null) return "[river-scan] no WorldGenerator";
            if (!VerdantsAscent.Water.Core.WorldGeneratorAccess.HasRiverWeightAccess)
                return "[river-scan] GetRiverWeight reflection unavailable — can't ground-truth rivers";

            const float R = 600f, step = 12f;
            float bestSq = float.MaxValue; float bx = 0, bz = 0, bw = 0, bwid = 0; int hits = 0;
            for (float dx = -R; dx <= R; dx += step)
            for (float dz = -R; dz <= R; dz += step)
            {
                float sx = pos.x + dx, sz = pos.z + dz;
                VerdantsAscent.Water.Core.WorldGeneratorAccess.GetRiverWeight(wg, sx, sz, out float w, out float wid);
                if (w > 0.02f)
                {
                    hits++;
                    float d = dx * dx + dz * dz;
                    if (d < bestSq) { bestSq = d; bx = sx; bz = sz; bw = w; bwid = wid; }
                }
            }

            string head = $"[river-scan] segIndex={WorldgenRivers.SegmentCount} seg(built={WorldgenRivers.Built}) | ground-truth carved-channel points in {R:F0}m = {hits}";
            if (hits == 0)
                return head + " — NONE. Vanilla rivers/streams are genuinely absent here; a raise needs a channel to sit in, so this is where terrain-carving a valley would come in.";
            float dist = Mathf.Sqrt(bestSq);
            return head + $" | NEAREST at ({bx:F0},{bz:F0}) = {dist:F0}m away, weight={bw:F2} width={bwid:F0}. Head there and `galaxies_river here`.";
        }

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class Terminal_InitTerminal_River
        {
            private static bool s_reg;
            private static void Postfix()
            {
                if (s_reg) return;
                s_reg = true;
                new Terminal.ConsoleCommand("galaxies_river",
                    "Tide/river shaper diagnostics. Usage: galaxies_river [here|scan]. 'scan' sweeps for the nearest carved river/stream channel.",
                    args =>
                    {
                        var p = Player.m_localPlayer;
                        if (p == null) { args.Context.AddString("[river] no local player"); return; }
                        string mode = args.Length > 1 ? args[1].ToLowerInvariant() : "here";
                        string s = mode == "scan" ? Scan(p.transform.position) : Describe(p.transform.position);
                        args.Context.AddString(s);
                        FiresLogger.LogInfo(s);
                    }, isCheat: true);   // 0.1.34: admin / devcommands only, as the help says
            }
        }
    }
}
