using System.Collections;
using System.Reflection;
using UnityEngine;
using VerdantsAscent.Water.Core;
using FiresLogger = VerdantsAscent.FiresLogger;

namespace VerdantsAscent.Galaxies
{
    // GL-based debug overlay for the tide + current system. Single component on
    // the plugin root. Draws every frame from OnRenderObject (per-camera hook)
    // but filtered to the main game camera, so the skybox / orbital cameras
    // don't double-render. Each toggle is independent; both off = no work.
    //
    //   - Currents: streamlines walked from a grid of seed points. Each line is
    //     traced N steps along the local current direction so swirls and bends
    //     are visible. Segment color = local strength via white -> yellow ->
    //     orange -> red ramp.
    //
    //   - Orbits: full path ring for every active FiresOrbitals body. Read via
    //     reflection from FAT's private s_bodies list, then ComputePosition
    //     (public) sweeps t across the body's PeriodSeconds to trace the loop.
    //     If no bodies are configured, falls back to drawing the moon's own
    //     camera-relative orbit so the toggle always has something to show.
    internal sealed class GalaxiesDebugVisualizer : MonoBehaviour
    {
        private static readonly Color OrangeMid = new Color(1.00f, 0.55f, 0.00f, 1f);
        private static readonly Color MoonRing  = new Color(0.85f, 0.85f, 1.00f, 1f);

        private Material _lineMat;

        // Cached reflection — FiresOrbitals.s_bodies (private List<Body>) +
        // Body.P / Body.SlotIndex. Nested Body class is private; flags catch
        // both Public + NonPublic so the fields resolve regardless of access.
        private static FieldInfo _orbitalsBodiesField;
        private static FieldInfo _bodyParamsField;
        private static FieldInfo _bodySlotField;
        private static bool _orbitalsReflectionResolved;

        // FiresMoon.MoonOrbit() returns the OrbitParams the moon rides each
        // day. Private static method, reflected here so we can draw the moon's
        // path around the camera as a fallback when no orbital slots exist.
        private static MethodInfo _moonOrbitMethod;
        private static bool _moonReflectionResolved;

        // One-time diagnostic state so toggling Orbits on logs once instead of
        // every frame.
        private static int _lastOrbitDiagBodyCount = int.MinValue;

        private void OnRenderObject()
        {
            if (GalaxiesConfig.EnableMod == null) return;

            bool drawCurrents = GalaxiesConfig.DebugVisualizeCurrents != null
                                && GalaxiesConfig.DebugVisualizeCurrents.Value;
            bool drawOrbits   = GalaxiesConfig.DebugVisualizeOrbits != null
                                && GalaxiesConfig.DebugVisualizeOrbits.Value;
            if (!drawCurrents && !drawOrbits) return;

            // Skip non-main cameras (skybox, orbital, reflection probes) so we
            // draw exactly once per frame at the player's view.
            var cam = Camera.current;
            if (cam == null) return;
            var mainCam = Utils.GetMainCamera();
            if (mainCam != null && cam != mainCam) return;

            EnsureMaterial();
            if (_lineMat == null) return;
            _lineMat.SetPass(0);

            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            if (drawCurrents) DrawCurrentField();
            if (drawOrbits)   DrawOrbitPaths(mainCam);

            GL.End();
            GL.PopMatrix();
        }

        // ---- Currents -----------------------------------------------------

        private static void DrawCurrentField()
        {
            Player local = Player.m_localPlayer;
            if (local == null) return;

            Vector3 center = local.transform.position;
            float spacing    = Mathf.Max(2f,      GalaxiesConfig.DebugCurrentsGridSpacing.Value);
            float radius     = Mathf.Max(spacing, GalaxiesConfig.DebugCurrentsGridRadius.Value);
            float arrowScale = Mathf.Max(0.5f,    GalaxiesConfig.DebugCurrentsArrowScale.Value);
            int steps        = Mathf.Max(1,       GalaxiesConfig.DebugCurrentsStreamlineSteps.Value);
            float radiusSq   = radius * radius;

            // Draw a touch above the live water surface so lines sit on the
            // ocean without z-fighting terrain at depth.
            float waterY = WaterUtil.OceanBaselineY() + TideSystem.SurfaceOffset + 0.5f;

            // Snap seeds to the world grid (independent of player position) so
            // streamlines don't crawl across the screen as you move.
            int cells = Mathf.CeilToInt(radius / spacing);
            float baseX = Mathf.Round(center.x / spacing) * spacing;
            float baseZ = Mathf.Round(center.z / spacing) * spacing;

            for (int xi = -cells; xi <= cells; xi++)
            {
                for (int zi = -cells; zi <= cells; zi++)
                {
                    Vector3 seed = new Vector3(
                        baseX + xi * spacing,
                        waterY,
                        baseZ + zi * spacing);

                    Vector2 d = new Vector2(seed.x - center.x, seed.z - center.z);
                    if (d.sqrMagnitude > radiusSq) continue;

                    TraceStreamline(seed, steps, arrowScale, waterY);
                }
            }
        }

        // Walk forward from <seed> N steps along the current. Each step's length
        // = strength * scale * 0.3 (so strong flow stretches the line, weak flow
        // tightens it). Segments colored by the strength sampled at each step.
        // Terminates early if TryGetCurrent returns false (e.g. crossed land).
        private static void TraceStreamline(Vector3 seed, int steps, float scale, float waterY)
        {
            Vector3 cursor = seed;
            CurrentSample last = default;
            bool any = false;

            for (int i = 0; i < steps; i++)
            {
                if (!CurrentSystem.TryGetCurrent(cursor, out CurrentSample s)) break;

                float stepLen = Mathf.Max(0.5f, s.Strength * scale * 0.3f);
                Vector3 next = cursor + s.Direction * stepLen;
                next.y = waterY;

                GL.Color(StrengthToColor(s.Strength));
                GL.Vertex(cursor);
                GL.Vertex(next);

                cursor = next;
                last = s;
                any = true;
            }

            if (!any) return;

            // Arrowhead at the streamline tip, colored by the tip's strength.
            Vector3 dir = last.Direction;
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
            Vector3 back = -dir;
            float headLen = Mathf.Max(0.3f, last.Strength * scale * 0.22f);
            GL.Color(StrengthToColor(last.Strength));
            GL.Vertex(cursor); GL.Vertex(cursor + (back + perp).normalized * headLen);
            GL.Vertex(cursor); GL.Vertex(cursor + (back - perp).normalized * headLen);
        }

        // Strength -> color ramp:
        //   0.0 white -> 0.5 yellow -> 1.0 orange -> 2.0+ red
        // Smoothly lerped between stops so the gradient is readable and a flow
        // gradually warming up doesn't snap categories.
        private static Color StrengthToColor(float s)
        {
            if (s <= 0.5f) return Color.Lerp(Color.white,  Color.yellow, Mathf.Clamp01(s / 0.5f));
            if (s <= 1.0f) return Color.Lerp(Color.yellow, OrangeMid,    Mathf.Clamp01((s - 0.5f) / 0.5f));
            if (s <= 2.0f) return Color.Lerp(OrangeMid,    Color.red,    Mathf.Clamp01((s - 1.0f) / 1.0f));
            return Color.red;
        }

        // ---- Orbits -------------------------------------------------------

        private static void DrawOrbitPaths(Camera mainCam)
        {
            int resolution = Mathf.Max(16, GalaxiesConfig.DebugOrbitsResolution.Value);

            // 1. FiresOrbitals bodies (FAT configures these as orbital slots).
            int drawnBodies = 0;
            if (EnsureOrbitalsReflection())
            {
                IList bodies = _orbitalsBodiesField.GetValue(null) as IList;
                if (bodies != null)
                {
                    for (int bi = 0; bi < bodies.Count; bi++)
                    {
                        object body = bodies[bi];
                        if (body == null) continue;

                        object pObj = _bodyParamsField.GetValue(body);
                        if (pObj == null) continue;
                        OrbitParams p = (OrbitParams)pObj;
                        if (p.PeriodSeconds < 0.01f) continue;

                        int slot = (int)_bodySlotField.GetValue(body);
                        GL.Color(Color.HSVToRGB(((slot * 0.6180339f) + 0.07f) % 1f, 0.75f, 1f));

                        Vector3 prev = FiresOrbitals.ComputePosition(p, 0f);
                        for (int i = 1; i <= resolution; i++)
                        {
                            float t = (i / (float)resolution) * p.PeriodSeconds;
                            Vector3 cur = FiresOrbitals.ComputePosition(p, t);
                            GL.Vertex(prev);
                            GL.Vertex(cur);
                            prev = cur;
                        }
                        drawnBodies++;
                    }
                }

                if (drawnBodies != _lastOrbitDiagBodyCount)
                {
                    _lastOrbitDiagBodyCount = drawnBodies;
                    FiresLogger.LogInfo(drawnBodies == 0
                        ? "DebugVisualizeOrbits: no FiresOrbitals bodies are currently configured " +
                          "(s_bodies is empty). Falling back to the moon's orbit. To populate " +
                          "bodies, configure FAT's orbital slots."
                        : $"DebugVisualizeOrbits: tracing {drawnBodies} FiresOrbitals " +
                          (drawnBodies == 1 ? "body." : "bodies."));
                }
            }
            else if (_lastOrbitDiagBodyCount != -999)
            {
                _lastOrbitDiagBodyCount = -999;
                FiresLogger.LogWarning("DebugVisualizeOrbits: reflection into FiresOrbitals " +
                                       "failed; FAT may not be loaded or the API changed.");
            }

            // (Moon-orbit ring removed — the visible moon moved to FiresTossinShade.
            //  Galaxies visualizes its own orbital bodies only.)
        }

        private static bool EnsureOrbitalsReflection()
        {
            if (_orbitalsReflectionResolved) return _orbitalsBodiesField != null;
            _orbitalsReflectionResolved = true;

            var orbitalsType = typeof(FiresOrbitals);
            _orbitalsBodiesField = orbitalsType.GetField("s_bodies",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (_orbitalsBodiesField == null) return false;

            var bodyType = orbitalsType.GetNestedType("Body",
                BindingFlags.NonPublic | BindingFlags.Public);
            if (bodyType == null) { _orbitalsBodiesField = null; return false; }

            const BindingFlags InstFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _bodyParamsField = bodyType.GetField("P", InstFlags);
            _bodySlotField   = bodyType.GetField("SlotIndex", InstFlags);
            if (_bodyParamsField == null || _bodySlotField == null)
            {
                _orbitalsBodiesField = null;
                return false;
            }
            return true;
        }

        private void EnsureMaterial()
        {
            if (_lineMat != null) return;
            Shader s = Shader.Find("Hidden/Internal-Colored");
            if (s == null) return;
            _lineMat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
            _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
            _lineMat.SetInt("_ZWrite",   0);
            // Always-on-top so debug arrows and orbit rings stay visible
            // through water and terrain.
            _lineMat.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        private void OnDestroy()
        {
            if (_lineMat != null) { Destroy(_lineMat); _lineMat = null; }
        }
    }
}
