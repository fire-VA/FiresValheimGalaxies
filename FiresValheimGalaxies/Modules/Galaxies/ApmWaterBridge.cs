using System;
using System.Reflection;
using UnityEngine;
using FiresLogger = VerdantsAscent.FiresLogger;

namespace VerdantsAscent.Galaxies
{
    /// <summary>
    /// 0.1.76 ([generator], Fire 11-16: the water code leaves Core for Advanced Player Movement): Galaxies publishes its tide / current
    /// answers into APM's FiresAPM.Water.WaterState by NAME only (no reference), so Galaxies loads and runs without APM; then nothing
    /// is published (no swim / flow consumers exist anyway). One Info line says which.
    /// </summary>
    internal static class ApmWaterBridge
    {
        private const string TypeName = "FiresAPM.Water.WaterState, FiresAdvancedPlayerMovement";
        private static bool s_looked;
        private static Type s_type;
        private static FieldInfo s_sampler, s_version;

        private static Type T
        {
            get
            {
                if (s_looked) return s_type;
                s_looked = true;
                try { s_type = Type.GetType(TypeName, false); } catch { s_type = null; }
                if (s_type != null)
                {
                    var fl = BindingFlags.Public | BindingFlags.Static;
                    s_sampler = s_type.GetField("SurfaceFieldSampler", fl);
                    s_version = s_type.GetField("SurfaceFieldVersion", fl);
                }
                FiresLogger.LogInfo(s_type != null
                    ? "[Water] Galaxies publishes tide / currents into Advanced Player Movement's WaterState"
                    : "[Water] Advanced Player Movement not installed: Galaxies' tide / current answers are not published (no swim / flow users)");
                return s_type;
            }
        }

        public static void Register(GalaxiesWaterState provider)
        {
            var t = T; if (t == null) return;
            try
            {
                Func<Vector3, float, float> surface = provider.GetWaterSurface;
                Func<Vector3, Vector4> flow = p => provider.TryGetFlow(p, out Vector3 f) ? new Vector4(f.x, f.y, f.z, 1f) : Vector4.zero;
                Func<float> phase = provider.GetTidePhase01;
                Func<float> vel = provider.GetTideVelocity;
                t.GetMethod("RegisterDelegates", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { surface, flow, phase, vel });
            }
            catch (Exception e) { FiresLogger.LogWarning("[Water] APM WaterState register failed: " + e.Message); }
        }

        public static void SetSampler(Func<float, float, Vector2> sampler)
        {
            if (T == null || s_sampler == null) return;
            try { s_sampler.SetValue(null, sampler); } catch { }
        }

        public static void BumpVersion()
        {
            if (T == null || s_version == null) return;
            try { s_version.SetValue(null, (int)s_version.GetValue(null) + 1); } catch { }
        }

        public static void Clear()
        {
            if (T == null) return;
            try { T.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null); } catch { }
            SetSampler(null);
        }
    }
}