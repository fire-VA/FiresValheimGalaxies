using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using FiresLogger = VerdantsAscent.FiresLogger;

namespace VerdantsAscent.Galaxies
{
    /// <summary>
    /// A live whirlpool: a rotating current ring with an inward pull, a sink core that traps
    /// ships, and a funnel-shaped depression in the water surface. Force model and funnel
    /// shape informed by Norger's Whirlpool reference mod (used as a guide, reimplemented
    /// against our systems): the HORIZONTAL swirl is served through <see cref="CurrentSystem"/>
    /// so ships, swimmers and every <c>FiresCore WaterState.TryGetFlow</c> consumer reacts
    /// through the same pipeline as tides/rivers; only the vertical sink and the surface
    /// funnel need their own patches here.
    /// </summary>
    internal sealed class WhirlpoolZone
    {
        public Vector3 Center;
        public float OuterRadius;   // beyond this, no effect
        public float CoreRadius;    // inside this the sink dominates
        public bool Clockwise = true;
        public bool Manual;         // console-spawned; spawner never removes these
        public long BlockId;        // spawner block that owns this zone (managed spawns)

        // 0..1 grow/shrink ramp so whirlpools swell in and die down instead of popping.
        public float LifeScale = 1f;

        public float EffectiveOuter => OuterRadius * Mathf.Clamp01(LifeScale);
    }

    internal static class WhirlpoolSystem
    {
        public static readonly List<WhirlpoolZone> Zones = new List<WhirlpoolZone>();

        // Shader-visual contract: up to 4 zones as globals, read by FiresWater's vertex
        // funnel (FTS side). Pushed every frame from GalaxiesModule.Update — same pattern
        // as TideSystem's _FiresTideOffset global, so FTS needs no Galaxies reference.
        private const int MaxShaderZones = 4;
        private static readonly Vector4[] s_posRad = new Vector4[MaxShaderZones];
        private static readonly Vector4[] s_shape = new Vector4[MaxShaderZones];
        private static readonly int Prop_WhirlCount = Shader.PropertyToID("_FiresWhirlCount");
        // Per-slot SCALAR globals, not SetGlobalVectorArray — plain vector globals are
        // the only push mechanism with working precedent against this family's bundle
        // shaders (tide offset, SSS, wind). Mirrors the shader's _FiresWhirlPosRad0..3.
        private static readonly int[] Prop_WhirlPosRadN =
        {
            Shader.PropertyToID("_FiresWhirlPosRad0"),
            Shader.PropertyToID("_FiresWhirlPosRad1"),
            Shader.PropertyToID("_FiresWhirlPosRad2"),
            Shader.PropertyToID("_FiresWhirlPosRad3"),
        };
        private static readonly int[] Prop_WhirlShapeN =
        {
            Shader.PropertyToID("_FiresWhirlShape0"),
            Shader.PropertyToID("_FiresWhirlShape1"),
            Shader.PropertyToID("_FiresWhirlShape2"),
            Shader.PropertyToID("_FiresWhirlShape3"),
        };

        /// <summary>Horizontal whirlpool flow (unit direction + speed in m/s) for
        /// <see cref="CurrentSystem"/>. Strongest zone wins where zones overlap. The spin
        /// ramps up toward the core then eases at dead centre (angular-momentum feel), the
        /// inward pull never quite reaches zero at the rim — both from the reference model.</summary>
        public static bool TryGetFlow(Vector3 pos, out Vector3 direction, out float speed)
        {
            direction = Vector3.zero;
            speed = 0f;
            if (Zones.Count == 0 || !GalaxiesConfig.WhirlpoolsEnabled.Value) return false;

            for (int i = 0; i < Zones.Count; i++)
            {
                WhirlpoolZone z = Zones[i];
                float outer = z.EffectiveOuter;
                Vector3 toCenter = z.Center - pos;
                toCenter.y = 0f;
                float dist = toCenter.magnitude;
                if (dist > outer || dist < 0.01f || outer < 1f) continue;

                Vector3 inward = toCenter / dist;
                Vector3 tangent = new Vector3(-inward.z, 0f, inward.x);   // clockwise seen from above
                if (!z.Clockwise) tangent = -tangent;

                float t = dist / outer;                                   // 0 core .. 1 rim
                float coreT = Mathf.Clamp01(dist / Mathf.Max(z.CoreRadius, 0.1f));
                float tanSpeed = GalaxiesConfig.WhirlpoolTangentialSpeed.Value * Mathf.Lerp(1.4f, 0.3f, t) * coreT;
                float radSpeed = GalaxiesConfig.WhirlpoolRadialSpeed.Value * Mathf.Lerp(0.35f, 1f, 1f - t);

                Vector3 flow = tangent * tanSpeed + inward * radSpeed;
                float mag = flow.magnitude * z.LifeScale;
                if (mag > speed)
                {
                    speed = mag;
                    direction = flow / Mathf.Max(flow.magnitude, 0.001f);
                }
            }
            return speed > 0.05f;
        }

        /// <summary>Water-surface offset (negative = depression) at a position: the funnel
        /// bowl plus rotating spiral ridges that fade at both the core and the rim. Applied
        /// to every WaterVolume height query below, so buoyancy, swimming and the FiresCore
        /// WaterState provider all agree on the bowl.</summary>
        public static float FunnelOffset(Vector3 pos)
        {
            if (Zones.Count == 0 || !GalaxiesConfig.WhirlpoolsEnabled.Value) return 0f;

            float offset = 0f;
            for (int i = 0; i < Zones.Count; i++)
            {
                WhirlpoolZone z = Zones[i];
                float outer = z.EffectiveOuter;
                Vector3 flat = pos - z.Center;
                flat.y = 0f;
                float dist = flat.magnitude;
                if (dist > outer || outer < 1f) continue;

                float t = Mathf.Clamp01(dist / outer);
                float bowl = Mathf.Pow(1f - t, 3f) * GalaxiesConfig.WhirlpoolFunnelDepth.Value * z.LifeScale;

                float angle = Mathf.Atan2(flat.z, flat.x);
                float spin = z.Clockwise ? -1f : 1f;
                float phase = angle * GalaxiesConfig.WhirlpoolSpiralArms.Value
                            + spin * WaterVolume.s_waterTime * GalaxiesConfig.WhirlpoolSpiralSpeed.Value;
                float ridgeFade = Mathf.Sin(t * Mathf.PI);
                float ridge = Mathf.Sin(phase) * GalaxiesConfig.WhirlpoolRidgeHeight.Value * ridgeFade * z.LifeScale;

                offset += ridge - bowl;
            }
            return offset;
        }

        /// <summary>Publish the nearest zones to the FiresWater shader globals (visual funnel).</summary>
        private static int s_lastPushedCount = -1;

        public static void PushShaderGlobals()
        {
            int n = 0;
            for (int i = 0; i < Zones.Count && n < MaxShaderZones; i++)
            {
                WhirlpoolZone z = Zones[i];
                if (z.EffectiveOuter < 1f) continue;
                s_posRad[n] = new Vector4(z.Center.x, z.Center.y, z.Center.z, z.EffectiveOuter);
                s_shape[n] = new Vector4(
                    GalaxiesConfig.WhirlpoolFunnelDepth.Value * z.LifeScale,
                    GalaxiesConfig.WhirlpoolSpiralArms.Value,
                    GalaxiesConfig.WhirlpoolSpiralSpeed.Value * (z.Clockwise ? -1f : 1f),
                    GalaxiesConfig.WhirlpoolRidgeHeight.Value * z.LifeScale);
                n++;
            }
            for (int i = n; i < MaxShaderZones; i++) { s_posRad[i] = Vector4.zero; s_shape[i] = Vector4.zero; }
            Shader.SetGlobalFloat(Prop_WhirlCount, n);
            for (int i = 0; i < MaxShaderZones; i++)
            {
                Shader.SetGlobalVector(Prop_WhirlPosRadN[i], s_posRad[i]);
                Shader.SetGlobalVector(Prop_WhirlShapeN[i], s_shape[i]);
            }

            // State-change breadcrumb: a zone count flip is rare (storm spawn/despawn or
            // console), and this line in the log is the proof the C#→shader handoff ran.
            if (n != s_lastPushedCount)
            {
                s_lastPushedCount = n;
                FiresLogger.LogInfo($"Whirlpool shader globals -> {n} zone(s)"
                    + (n > 0 ? $", [0] centre=({s_posRad[0].x:F0},{s_posRad[0].z:F0}) r={s_posRad[0].w:F0}m depth={s_shape[0].x:F1}m" : ""));
            }
        }

        public static void Clear() { Zones.Clear(); s_nextDamage.Clear(); PushShaderGlobals(); }

        // ── Patches ──────────────────────────────────────────────────────────

        /// <summary>The funnel in every water-height query — buoyancy probes, swim level,
        /// floating items and the WaterState provider all ride the bowl.</summary>
        [HarmonyPatch(typeof(WaterVolume), nameof(WaterVolume.GetWaterSurface))]
        private static class WaterVolume_GetWaterSurface_Funnel
        {
            private static void Postfix(Vector3 point, ref float __result)
            {
                if (Zones.Count == 0 || !GalaxiesConfig.GalaxiesOwnsOcean) return;   // 0.1.38: stands down when Vedr owns the ocean
                __result += FunnelOffset(point);
            }
        }

        /// <summary>Vertical SINK on ships inside the core (the horizontal swirl reaches
        /// ships through ShipCurrentPatch via CurrentSystem). Owner-only, water-borne only,
        /// quadratic ramp so the outer ring is just current and the core is the trap.</summary>
        private static readonly Dictionary<int, float> s_nextDamage = new Dictionary<int, float>();

        [HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
        private static class Ship_CustomFixedUpdate_Sink
        {
            private static WaterVolume s_volumeCache;

            private static void Postfix(Ship __instance, float fixedDeltaTime)
            {
                if (Zones.Count == 0 || !GalaxiesConfig.WhirlpoolsEnabled.Value || !GalaxiesConfig.GalaxiesOwnsOcean) return;
                if (__instance.m_body == null) return;
                if (__instance.m_nview != null && __instance.m_nview.IsValid() && !__instance.m_nview.IsOwner()) return;

                Vector3 shipPos = __instance.m_body.worldCenterOfMass;
                float waterLevel = Floating.GetWaterLevel(shipPos, ref s_volumeCache);
                if (shipPos.y - waterLevel - __instance.m_waterLevelOffset > __instance.m_disableLevel) return;

                for (int i = 0; i < Zones.Count; i++)
                {
                    WhirlpoolZone z = Zones[i];
                    Vector3 flat = z.Center - shipPos;
                    flat.y = 0f;
                    float dist = flat.magnitude;
                    if (dist > z.CoreRadius || z.EffectiveOuter < 1f) continue;

                    float sinkT = Mathf.Clamp01(1f - dist / Mathf.Max(z.CoreRadius, 0.1f));
                    float strength = GalaxiesConfig.WhirlpoolShipSinkForce.Value * sinkT * sinkT * z.LifeScale;
                    __instance.m_body.AddForceAtPosition(
                        Vector3.down * strength * fixedDeltaTime, shipPos, ForceMode.VelocityChange);

                    // Structural damage while held in the core — the maelstrom grinds the
                    // hull, it doesn't just tug it. Owner-side (we're already owner-gated),
                    // 1s cadence, scaled by core depth and the spawn/despawn ramp.
                    float dps = GalaxiesConfig.WhirlpoolShipDamage.Value;
                    if (dps > 0.01f)
                    {
                        int id = __instance.GetInstanceID();
                        if (!s_nextDamage.TryGetValue(id, out float next) || Time.time >= next)
                        {
                            s_nextDamage[id] = Time.time + 1f;
                            WearNTear wnt = __instance.GetComponent<WearNTear>();
                            if (wnt != null) wnt.ApplyDamage(dps * sinkT * z.LifeScale);
                        }
                    }
                }
            }
        }

        // ── Console (testing without waiting for a storm) ───────────────────

        private static bool s_reg;

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class Terminal_InitTerminal_Whirl
        {
            private static void Postfix()
            {
                if (s_reg) return;
                s_reg = true;
                new Terminal.ConsoleCommand("galaxies_whirl",
                    "Whirlpool testing. Usage: galaxies_whirl here|clear|status",
                    args =>
                    {
                        string a = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
                        switch (a)
                        {
                            case "here":
                                var p = Player.m_localPlayer;
                                if (p == null) { args.Context.AddString("[whirl] no local player"); return; }
                                Vector3 c = p.transform.position + p.transform.forward * 40f;
                                c.y = WaterUtil.GetWaterLevel(c);
                                Zones.Add(new WhirlpoolZone
                                {
                                    Center = c,
                                    OuterRadius = GalaxiesConfig.WhirlpoolOuterRadius.Value,
                                    CoreRadius = GalaxiesConfig.WhirlpoolCoreRadius.Value,
                                    Manual = true,
                                    LifeScale = 1f,
                                });
                                args.Context.AddString($"[whirl] spawned manual whirlpool at {c} (r={GalaxiesConfig.WhirlpoolOuterRadius.Value}m)");
                                FiresLogger.LogInfo($"Manual whirlpool spawned at {c} (r={GalaxiesConfig.WhirlpoolOuterRadius.Value}m)");
                                break;
                            case "clear":
                                int had = Zones.Count;
                                Clear();
                                args.Context.AddString($"[whirl] cleared {had} zone(s)");
                                break;
                            default:
                                // Read the globals BACK from the shader pipeline — splits "C# never
                                // pushed" from "shader never consumed" in one line, no debugger needed.
                                float shaderCount = Shader.GetGlobalFloat(Prop_WhirlCount);
                                Vector4 shaderPos0 = Shader.GetGlobalVector(Prop_WhirlPosRadN[0]);
                                string readback = $"[whirl] zones={Zones.Count} enabled={GalaxiesConfig.WhirlpoolsEnabled.Value} "
                                    + $"shaderCount={shaderCount:F0} shader[0]={shaderPos0}";
                                args.Context.AddString(readback);
                                FiresLogger.LogInfo(readback);
                                foreach (var z in Zones)
                                {
                                    string line = $"[whirl]  at {z.Center} r={z.EffectiveOuter:F0} life={z.LifeScale:F2} manual={z.Manual}";
                                    args.Context.AddString(line);
                                    FiresLogger.LogInfo(line);
                                }
                                break;
                        }
                    }, isCheat: true);   // 0.1.34: admin / devcommands only, as the help says
            }
        }
    }
}
