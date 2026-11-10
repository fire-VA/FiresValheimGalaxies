using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    // 0.1.65 (Fire 10-29: "waves foam and tides should be hidden under ice"; [saddles] traced the tide motion to Galaxies): where FAT
    // reports ice on the water (FatApi.TryIceAt, the add-only API Vedr's ice cubes use), the tide rise / fall is held: the surface offset
    // is scaled by (1 - ice coverage), so a full sheet does not move and the edge fades back to the full tide. Looked up per 4 m cell,
    // cached 1 s (GetWaterSurface is asked many times a frame). The rendered sea's tide global is one value for the whole world; under
    // the ice it is hidden by FAT's ice cover (_FiresIceCover, [saddles]) and FTS's waves / foam ([seasons]).
    internal static class IceTideHold
    {
        private delegate bool TryIceFn(Vector3 p, out float coverage, out float thickness);
        private static TryIceFn s_tryIce; private static bool s_looked;
        private static readonly Dictionary<long, (float until, float cover)> s_cells = new Dictionary<long, (float, float)>();
        private static float s_nextLog; private static int s_held, s_asked;

        private static void Bind()
        {
            if (s_looked) return; s_looked = true;
            try
            {
                var t = Type.GetType("VerdantsAscent.FatApi, FiresAdminTerrain", false);
                var m = t?.GetMethod("TryIceAt", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Vector3), typeof(float).MakeByRefType(), typeof(float).MakeByRefType() }, null);
                if (m != null) s_tryIce = (TryIceFn)Delegate.CreateDelegate(typeof(TryIceFn), m);
            }
            catch (Exception e) { FiresLogger.LogDebug($"[Galaxies] ice tide hold: FAT ice API bind failed ({e.Message})"); }
            FiresLogger.LogDebug($"[Galaxies] ice tide hold: FAT FatApi.TryIceAt {(s_tryIce != null ? "bound - the tide is held under ice" : "not found - tides move everywhere")}");
        }

        /// <summary>1 = full tide, 0 = held (full ice sheet).</summary>
        internal static float TideScale(float x, float z)
        {
            Bind();
            if (s_tryIce == null) return 1f;
            long key = ((long)Mathf.FloorToInt(x / 4f) << 32) ^ (uint)Mathf.FloorToInt(z / 4f);
            float now = Time.time;
            if (!s_cells.TryGetValue(key, out var c) || now >= c.until)
            {
                float cover = 0f;
                try { if (s_tryIce(new Vector3(x, 0f, z), out float cov, out float thick) && thick > 0.001f) cover = Mathf.Clamp01(cov); }
                catch { cover = 0f; }
                if (s_cells.Count > 4096) s_cells.Clear();
                c = (now + 1f, cover);
                s_cells[key] = c;
            }
            s_asked++;
            if (c.cover > 0f) s_held++;
            if (now >= s_nextLog && s_asked > 0)
            {
                s_nextLog = now + 30f;
                if (s_held > 0) FiresLogger.LogDebug($"[Galaxies] ice tide hold: {s_held} of {s_asked} water-surface queries under ice in the last 30 s had the tide held (offset x (1 - cover))");
                s_held = s_asked = 0;
            }
            return 1f - c.cover;
        }
    }
}
