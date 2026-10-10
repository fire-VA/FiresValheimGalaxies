using System.Collections.Generic;
using UnityEngine;
using VerdantsAscent.Water.Core;
using FiresLogger = VerdantsAscent.FiresLogger;

namespace VerdantsAscent.Galaxies
{
    // World-wide index of vanilla river + stream paths from WorldGenerator.
    // Used by river-current detection to get the TRUE along-channel flow
    // direction (the tangent of the river's curve), instead of guessing it
    // from the local terrain slope — which for a channel cut through a bank
    // points across the river, not along it.
    //
    // Each segment is the straight axis p0..p1 plus the same sinusoidal
    // perpendicular displacement vanilla applies (curveWidth/curveWavelength).
    // Lookup is O(N) over the segment list; vanilla worlds have on the order
    // of tens of rivers/streams, so per-call cost is negligible and the cache
    // layer above (RiverCurrentCache) makes the amortized cost zero.
    internal static class WorldgenRivers
    {
        // Single segment record. HasCurve / CurveWidth / CurveWavelength
        // mirror the vanilla River class so tangent reconstruction is exact.
        private readonly struct Segment
        {
            public readonly Vector2 P0;
            public readonly Vector2 Axis;          // normalized (p1-p0) — straight-line direction
            public readonly Vector2 Perp;          // perpendicular to Axis
            public readonly float   Length;
            public readonly float   HalfWidth;     // widthMin/2 — half the narrow-end channel width
            public readonly bool    HasCurve;
            public readonly float   CurveWidth;
            public readonly float   CurveWavelength;

            public Segment(Vector2 p0, Vector2 p1, float halfWidth,
                           float curveWidth, float curveWavelength)
            {
                P0 = p0;
                Vector2 delta = p1 - p0;
                Length = delta.magnitude;
                Axis = Length > 0.001f ? delta / Length : Vector2.right;
                Perp = new Vector2(-Axis.y, Axis.x);
                HalfWidth = halfWidth;
                HasCurve = curveWidth > 0.01f && curveWavelength > 0.01f;
                CurveWidth = curveWidth;
                CurveWavelength = curveWavelength;
            }
        }

        private static readonly List<Segment> Segments = new();
        private static bool _built;

        public static bool Built => _built;
        public static int SegmentCount => Segments.Count;

        public static void Clear()
        {
            Segments.Clear();
            _built = false;
        }

        public static void Build()
        {
            Segments.Clear();
            _built = false;

            WorldGenerator wg = WorldGenerator.instance;
            if (wg == null)
            {
                FiresLogger.LogWarning("WorldgenRivers.Build: WorldGenerator.instance is null; skipping.");
                return;
            }

            try
            {
                var rivers = WorldGeneratorAccess.GetRivers(wg);
                if (rivers != null)
                    foreach (var r in rivers)
                        Add(r);

                var streams = WorldGeneratorAccess.GetStreams(wg);
                if (streams != null)
                    foreach (var s in streams)
                        Add(s);

                _built = true;
                FiresLogger.LogVerbose($"WorldgenRivers indexed: {Segments.Count} river/stream segments " +
                                       $"(rivers={rivers?.Count ?? 0}, streams={streams?.Count ?? 0}).");
            }
            catch (System.Exception ex)
            {
                FiresLogger.LogWarning($"WorldgenRivers.Build failed: {ex.Message}");
            }
        }

        private static void Add(WorldGenerator.River r)
        {
            if (r == null) return;
            // widthMin is the narrow end of the channel; halve for half-width.
            // Floor at 6m so very narrow worldgen streams still detect a swim.
            float halfWidth = Mathf.Max(6f, r.widthMin * 0.5f);
            Segments.Add(new Segment(r.p0, r.p1, halfWidth, r.curveWidth, r.curveWavelength));
        }

        // For a world XZ point, return (1) the curve-corrected flow direction
        // along the nearest river that contains it, and (2) a 0..1 width factor
        // that's 1 at centerline and 0 at the bank.
        public static bool TryGetFlow(Vector3 pos, out Vector3 flow, out float widthFactor)
        {
            flow = Vector3.zero;
            widthFactor = 0f;
            if (!_built || Segments.Count == 0) return false;

            Vector2 q = new Vector2(pos.x, pos.z);

            Segment bestSeg = default;
            float bestFactor = 0f;
            float bestT = 0f;
            bool found = false;

            for (int i = 0; i < Segments.Count; i++)
            {
                Segment s = Segments[i];

                // Project on the STRAIGHT axis to get path parameter t.
                Vector2 ap = q - s.P0;
                float t = Mathf.Clamp(Vector2.Dot(ap, s.Axis), 0f, s.Length);

                // Curve-corrected on-path point: straight axis position plus
                // perpendicular sinusoidal offset (matches vanilla AddRivers).
                float curveOff = s.HasCurve
                    ? s.CurveWidth * Mathf.Sin(t / s.CurveWavelength)
                    : 0f;
                Vector2 onPath = s.P0 + s.Axis * t + s.Perp * curveOff;

                float dist = Vector2.Distance(q, onPath);
                if (dist > s.HalfWidth) continue;

                float f = 1f - (dist / s.HalfWidth);
                if (f > bestFactor)
                {
                    bestFactor = f;
                    bestSeg = s;
                    bestT = t;
                    found = true;
                }
            }

            if (!found) return false;

            // Tangent to the curved path at parameter t. Straight-axis component
            // plus the derivative of the perpendicular sinusoid.
            Vector2 tangent = bestSeg.Axis;
            if (bestSeg.HasCurve)
                tangent += bestSeg.Perp
                         * (bestSeg.CurveWidth / bestSeg.CurveWavelength)
                         * Mathf.Cos(bestT / bestSeg.CurveWavelength);
            tangent.Normalize();

            flow = new Vector3(tangent.x, 0f, tangent.y);
            widthFactor = bestFactor;
            return true;
        }

        /// <summary>Channel geometry at a point for the extended-river raise: the curve-corrected
        /// centerline point, the unit perpendicular (bank direction), the half-width, and the
        /// centerline→bank width factor (1 at centre, 0 at the edge). Lets the raise sample the two
        /// banks at <c>onPath ± perp * halfWidth</c>. False if the point isn't inside any channel.</summary>
        public static bool TryGetChannel(Vector3 pos, out Vector2 onPath, out Vector2 perp,
                                         out float halfWidth, out float widthFactor)
        {
            onPath = Vector2.zero; perp = Vector2.right; halfWidth = 0f; widthFactor = 0f;
            if (!_built || Segments.Count == 0) return false;

            Vector2 q = new Vector2(pos.x, pos.z);
            float bestFactor = 0f;
            bool found = false;

            for (int i = 0; i < Segments.Count; i++)
            {
                Segment s = Segments[i];
                Vector2 ap = q - s.P0;
                float t = Mathf.Clamp(Vector2.Dot(ap, s.Axis), 0f, s.Length);
                float curveOff = s.HasCurve ? s.CurveWidth * Mathf.Sin(t / s.CurveWavelength) : 0f;
                Vector2 pt = s.P0 + s.Axis * t + s.Perp * curveOff;
                float dist = Vector2.Distance(q, pt);
                if (dist > s.HalfWidth) continue;

                float f = 1f - (dist / s.HalfWidth);
                if (f > bestFactor)
                {
                    bestFactor = f;
                    onPath = pt;
                    perp = s.Perp;
                    halfWidth = s.HalfWidth;
                    widthFactor = f;
                    found = true;
                }
            }
            return found;
        }
    }
}
