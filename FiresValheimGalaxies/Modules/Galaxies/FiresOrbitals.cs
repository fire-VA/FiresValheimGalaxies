using System.Collections.Generic;
using UnityEngine;

namespace VerdantsAscent.Water.Core
{
    /// <summary>
    /// Configurable bodies (satellites, ships, moons) that orbit the WORLD on
    /// tilted egg-shaped paths. Unlike the skybox moon these sit at real world
    /// positions around the map centre, so they arc overhead, are reachable, and
    /// dip under the world on the far side. Each orbit is an ellipse in plan view
    /// (egg via <see cref="OrbitParams.EggRatio"/>) tilted so it rises to
    /// <see cref="OrbitParams.ApexHeight"/> over the map and falls to
    /// <see cref="OrbitParams.NadirHeight"/> beneath it. Orbital precession slowly
    /// sweeps the high side around; a separate nutation term adds a vertical nod.
    /// Bodies are visual-only (no collision/gravity) and keep orbiting while away.
    /// Driven by real time so periods are intuitive (seconds per lap).
    /// </summary>
    public struct OrbitParams
    {
        public bool   Enabled;
        public string PrefabName;     // bundle asset; empty/missing => placeholder sphere
        public bool   FollowVanillaMoon;  // ignore the egg-orbit params and ride the vanilla moon's sky direction (anti-sun) so the body lines up with the moon light + god-rays
        public float  OrbitWidth;     // full horizontal width (m); semi-major = /2
        public float  ApexHeight;     // peak Y over the map (m)
        public float  NadirHeight;    // lowest Y under the world (m)
        public float  PeriodSeconds;  // seconds for one full lap
        public float  EggRatio;       // horizontal minor/major axis (1 = round, <1 = egg)
        public float  WobbleAmplitude;// nutation nod height (m)
        public float  WobbleSeconds;  // nutation period (s)
        public float  PrecessionMinutes; // minutes for the orbit to rotate 360 around Y (0 = none)
        public float  PhaseOffsetDeg; // starting angle so bodies don't bunch up
        public float  SpinDegPerSec;  // body's own-axis spin
        public float  Scale;          // model scale multiplier
        public float  TiltDeg;        // orbital-plane inclination (tip the whole ring up/down)
        public float  YawDeg;         // static compass heading of the orbit's long axis (around Y)
        public OrbitMotion Motion;    // 0.1.32: how the body moves (Auto = Realistic for the moon prefab, Custom otherwise)
        public float  LockAzimuthDeg; // Locked: compass heading of the body's sky direction (0 = north, 90 = east)
        public float  LockElevationDeg; // Locked: height above the horizon (deg)
        public bool   TidalLock;      // the same face always toward the world centre (a real moon); off = SpinDegPerSec
    }

    // 0.1.32 (Fire 10-06: "we cannot stop the rotation to lock it in a position or slow the rotation ... it should follow a
    // realistic orbit around the planet"). The moon slot used to be pinned to the anti-sun (FollowVanillaMoon), which ignored
    // every orbit key.
    public enum OrbitMotion
    {
        Auto,       // Realistic for the moon prefab, Custom for anything else
        Realistic,  // the moon's synodic orbit on the game clock: [Galaxies - Moon] SynodicDays / OrbitInclinationDeg, plus this slot's PhaseOffsetDeg
        Custom,     // the slot's egg orbit (width, apex/nadir, PeriodSeconds, tilt, yaw, precession, wobble)
        Locked,     // held still in the sky at LockAzimuthDeg / LockElevationDeg
        AntiSun,    // the old behaviour: opposite the sun, where vanilla's moonlight comes from (always full)
    }

    public static class FiresOrbitals
    {
        public static bool Enabled = true;
        public static Vector3 Center = Vector3.zero;   // Valheim world centre
        private static readonly int s_propSkyboxSunDir = Shader.PropertyToID("_SkyboxSunDir");
        private static readonly int s_propMasterFxOff  = Shader.PropertyToID("_FiresMasterFxOff");

        private static OrbitParams[] s_params = new OrbitParams[0];
        private static readonly List<Body> s_bodies = new List<Body>();
        private static Transform s_parent;
        private static bool s_dirty;
        private static float s_nextPrefabRetry;

        private class Body
        {
            public int         SlotIndex;
            public OrbitParams P;
            public GameObject  Instance;
            public string      BuiltFrom;   // PrefabName the instance was built from
            public bool        IsPlaceholder;
            public float       UnitRadius;  // renderer radius at scale 1 (0 until measured); FollowVanillaMoon sizing
        }

        public static void SetParent(Transform parent) => s_parent = parent;

        // Called from config apply — store the desired slot params, flag a rebuild.
        public static void Configure(OrbitParams[] slots)
        {
            s_params = slots ?? new OrbitParams[0];
            s_dirty = true;
        }

        public static void Tick()
        {
            // Cross-mod master kill: FiresTossinShade's "00 Master" pushes _FiresMasterFxOff=1
            // for pure vanilla graphics — orbitals stand down like Enabled=false. Unset/0 = run.
            if (!Enabled || Shader.GetGlobalFloat(s_propMasterFxOff) > 0.5f) { TeardownAll(); return; }
            if (s_dirty) { Rebuild(); s_dirty = false; }

            // A prefab named in the config but not yet in the bundle at spawn time
            // shows the placeholder; once the bundle has it, upgrade in place.
            if (Time.time >= s_nextPrefabRetry)
            {
                s_nextPrefabRetry = Time.time + 2f;
                RetryPendingPrefabs();
            }

            float t = Time.time;
            Camera cam = Utils.GetMainCamera();
            foreach (var b in s_bodies)
            {
                if (b.Instance == null) continue;
                Vector3 pos = ComputePosition(b.P, t);
                b.Instance.transform.position = pos;
                // Tidally locked: the same face always toward the world centre, so from the map it never seems to turn.
                Vector3 toCentre = Center - pos;
                b.Instance.transform.rotation = b.P.TidalLock && toCentre.sqrMagnitude > 1f
                    ? Quaternion.LookRotation(toCentre, Vector3.up)
                    : Quaternion.Euler(0f, t * b.P.SpinDegPerSec, 0f);
                if (b.P.FollowVanillaMoon) SizeAsTheMoon(b, cam);
            }

            if (Time.time >= s_nextSightLog) { s_nextSightLog = Time.time + 30f; LogSight(); }
        }

        // 0.1.29 (Fire 10-05: "we still have no moon"). The body that rides the vanilla moon IS the moon (FiresMoon hides its
        // skybox disc while this one shows, and the vanilla moon is hidden too), but it was sized by the slot's model scale,
        // meant for the 1 m placeholder sphere: the FiresMoon prefab's mesh is ~2 cm across, so scale 50 made it 0.01 deg at
        // ~5.4 km, a dot (the sight line said so). It is now scaled every frame from its distance to the camera to look as big
        // as the moon is meant to (FiresMoon.TargetAngularDiameterDeg x SizeScale, the disc's own size); the slot's scale stays
        // the floor.
        private static void SizeAsTheMoon(Body b, Camera cam)
        {
            if (cam == null) return;
            float scale = Mathf.Max(0.01f, b.P.Scale);
            if (b.UnitRadius <= 0f)
            {
                float radius = 0f;
                foreach (var r in b.Instance.GetComponentsInChildren<Renderer>(true))
                    if (r != null) radius = Mathf.Max(radius, Mathf.Max(r.bounds.extents.x, Mathf.Max(r.bounds.extents.y, r.bounds.extents.z)));
                float current = b.Instance.transform.lossyScale.x;
                if (radius <= 0f || current <= 0f) return;
                b.UnitRadius = radius / current;
            }
            float dist = Vector3.Distance(cam.transform.position, b.Instance.transform.position);
            float wanted = dist * Mathf.Tan(FiresMoon.TargetAngularDiameterDeg * 0.5f * Mathf.Deg2Rad) * Mathf.Max(0.01f, FiresMoon.SizeScale);
            float parent = b.Instance.transform.parent != null ? b.Instance.transform.parent.lossyScale.x : 1f;
            scale = Mathf.Max(scale, wanted / b.UnitRadius / Mathf.Max(1e-4f, parent));
            b.Instance.transform.localScale = Vector3.one * scale;
        }

        private static float s_nextSightLog;

        /// <summary>
        /// Debug, every 30 s: where each orbiting body is from the camera, how big it looks and whether it is drawn,
        /// so "I can't see the moon" can be read off the log (behind the far plane, under the horizon, sub-pixel,
        /// renderer off) instead of guessed.
        /// </summary>
        private static void LogSight()
        {
            if (s_bodies.Count == 0) return;
            Camera cam = Utils.GetMainCamera();
            if (cam == null) return;
            Vector3 eye = cam.transform.position;
            foreach (var b in s_bodies)
            {
                if (b.Instance == null) continue;
                var r = b.Instance.GetComponentInChildren<Renderer>(true);
                Vector3 to = b.Instance.transform.position - eye;
                float dist = to.magnitude;
                float radius = r != null ? Mathf.Max(r.bounds.extents.x, Mathf.Max(r.bounds.extents.y, r.bounds.extents.z)) : 0f;
                float angularDeg = dist > 1f ? 2f * Mathf.Atan(radius / dist) * Mathf.Rad2Deg : 0f;
                float elevationDeg = dist > 1f ? Mathf.Asin(Mathf.Clamp(to.y / dist, -1f, 1f)) * Mathf.Rad2Deg : 0f;
                FiresLogger.LogDebug($"[FiresOrbitals] sight '{b.Instance.name}': dist={dist:F0}m (far clip {cam.farClipPlane:F0}m), "
                    + $"elevation={elevationDeg:F1}deg, apparentSize={angularDeg:F2}deg, active={b.Instance.activeInHierarchy}, "
                    + $"renderer={(r == null ? "none" : r.enabled + " shader='" + (r.sharedMaterial != null ? r.sharedMaterial.shader.name : "none") + "'")}, "
                    + $"layer={b.Instance.layer}, culledByCamera={(cam.cullingMask & (1 << b.Instance.layer)) == 0}.");
            }
        }

        // Auto: the moon prefab rides its realistic orbit, anything else the slot's egg orbit.
        public static OrbitMotion EffectiveMotion(OrbitParams p) =>
            p.Motion != OrbitMotion.Auto ? p.Motion : p.FollowVanillaMoon ? OrbitMotion.Realistic : OrbitMotion.Custom;

        public static Vector3 ComputePosition(OrbitParams p, float t)
        {
            OrbitMotion motion = EffectiveMotion(p);
            float skyDistance = Mathf.Max(500f, p.OrbitWidth * 0.5f);

            // Locked: a fixed sky direction (azimuth from north, elevation above the horizon), world-anchored at Center.
            if (motion == OrbitMotion.Locked)
            {
                float az = p.LockAzimuthDeg * Mathf.Deg2Rad, el = Mathf.Clamp(p.LockElevationDeg, -90f, 90f) * Mathf.Deg2Rad;
                return Center + new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el)) * skyDistance;
            }

            // Sun-relative motions read the sky's sun direction (-_SkyboxSunDir is where vanilla's moonlight comes from).
            //   AntiSun: opposite the sun (the pre-0.1.32 moon): lines up with the moonlight and god-rays, always full.
            //   Realistic: FiresMoon's own orbit, on the game clock: it crosses the sky once a day like the sun, rises a little
            //   later each day and goes through its phases over SynodicDays, on a plane tilted by OrbitInclinationDeg.
            //   The slot's PhaseOffsetDeg shifts it along that orbit (90 = a quarter of the cycle).
            if (motion == OrbitMotion.AntiSun || motion == OrbitMotion.Realistic)
            {
                Vector4 s = Shader.GetGlobalVector(s_propSkyboxSunDir);
                Vector3 sun = new Vector3(s.x, s.y, s.z);
                if (sun.sqrMagnitude > 1e-4f)
                {
                    Vector3 dir = motion == OrbitMotion.AntiSun ? -sun.normalized
                        : FiresMoon.ComputeMoonDirection(sun, Mathf.Repeat(FiresMoon.ComputePhase01() + p.PhaseOffsetDeg / 360f, 1f));
                    return Center + dir.normalized * skyDistance;
                }
                // _SkyboxSunDir not ready yet — fall through to the egg orbit this frame.
            }

            float w     = 2f * Mathf.PI / Mathf.Max(1f, p.PeriodSeconds);
            float theta = p.PhaseOffsetDeg * Mathf.Deg2Rad + t * w;

            float a     = p.OrbitWidth * 0.5f;
            float b     = a * Mathf.Clamp(p.EggRatio, 0.05f, 1f);
            float midY  = (p.ApexHeight + p.NadirHeight) * 0.5f;
            float halfY = (p.ApexHeight - p.NadirHeight) * 0.5f;

            float xL = a * Mathf.Cos(theta);
            float zL = b * Mathf.Sin(theta);
            float y  = midY + halfY * Mathf.Cos(theta);   // apex at theta=0, nadir at theta=pi

            // Orbital precession — rotate the ellipse (and so the high side) about Y.
            if (p.PrecessionMinutes > 0.01f)
            {
                float pa = t * (2f * Mathf.PI / (p.PrecessionMinutes * 60f));
                float c = Mathf.Cos(pa), s = Mathf.Sin(pa);
                float xr = xL * c - zL * s;
                float zr = xL * s + zL * c;
                xL = xr; zL = zr;
            }

            // Nutation — small vertical nod superimposed on the main arc.
            if (p.WobbleSeconds > 0.01f && p.WobbleAmplitude != 0f)
                y += p.WobbleAmplitude * Mathf.Sin(t * (2f * Mathf.PI / p.WobbleSeconds));

            // Orient the whole orbital plane: TiltDeg tips the ring up/down,
            // YawDeg swings its long axis to a compass heading. Applied to the
            // local position so the body rides the re-oriented path.
            Vector3 local = new Vector3(xL, y, zL);
            if (p.TiltDeg != 0f || p.YawDeg != 0f)
                local = Quaternion.Euler(p.TiltDeg, p.YawDeg, 0f) * local;

            return Center + local;
        }

        private static void Rebuild()
        {
            // Drop instances whose slot is gone, disabled, or changed prefab.
            for (int i = s_bodies.Count - 1; i >= 0; i--)
            {
                var b = s_bodies[i];
                bool stillWanted = b.SlotIndex < s_params.Length
                                && s_params[b.SlotIndex].Enabled
                                && SamePrefab(s_params[b.SlotIndex].PrefabName, b.BuiltFrom);
                if (!stillWanted)
                {
                    if (b.Instance != null) Object.Destroy(b.Instance);
                    s_bodies.RemoveAt(i);
                }
            }

            for (int i = 0; i < s_params.Length; i++)
            {
                var p = s_params[i];
                if (!p.Enabled) continue;
                if (s_bodies.Exists(x => x.SlotIndex == i)) { UpdateExisting(i, p); continue; }

                var body = new Body { P = p, SlotIndex = i };
                body.Instance = CreateInstance(p, i, out body.IsPlaceholder);
                body.BuiltFrom = p.PrefabName ?? string.Empty;
                if (body.Instance != null)
                {
                    body.Instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, p.Scale);
                    if (s_parent != null) body.Instance.transform.SetParent(s_parent, true);
                }
                s_bodies.Add(body);
            }
        }

        private static void UpdateExisting(int slot, OrbitParams p)
        {
            var b = s_bodies.Find(x => x.SlotIndex == slot);
            if (b == null) return;
            b.P = p;   // live orbit-param changes apply next Tick
            if (b.Instance != null)
                b.Instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, p.Scale);
        }

        private static GameObject CreateInstance(OrbitParams p, int slot, out bool placeholder)
        {
            placeholder = false;
            GameObject prefab = ResolvePrefab(p.PrefabName);

            GameObject go;
            if (prefab != null)
            {
                go = InstantiateOrbitalBody(prefab, $"FiresOrbital_{slot}_{p.PrefabName}");
                Debug.Log($"[FiresOrbitals] Slot {slot}: orbiting prefab '{p.PrefabName}' (collidable, travelable).");
            }
            else
            {
                go = CreatePlaceholder(slot);
                placeholder = true;
                Debug.Log(string.IsNullOrEmpty(p.PrefabName)
                    ? $"[FiresOrbitals] Slot {slot}: no PrefabName set — orbiting a placeholder sphere."
                    : $"[FiresOrbitals] Slot {slot}: prefab '{p.PrefabName}' not found in bundle OR ZNetScene — placeholder sphere (retrying).");
            }
            return go;
        }

        // Resolve a prefab by name from OUR bundle first, then Valheim's live
        // prefab registry (ZNetScene). The registry covers vanilla pieces, mod
        // pieces, and FAP-baked blueprints (e.g. piece_Phalanx) — anything the
        // game knows by name — so the orbital isn't limited to bundle assets.
        private static GameObject ResolvePrefab(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            // The moon MODEL ships in the firesgalaxies bundle (NOT ZNetScene), so resolve it
            // from FiresMoon — this is what lets an orbital slot orbit the real moon prefab (a
            // reachable body) instead of the placeholder sphere. TryLoadAssets is idempotent, so
            // it's safe to poke it here if the bundle loaded but the moon driver hasn't yet.
            if (name.Equals(FiresMoon.PrefabName, System.StringComparison.OrdinalIgnoreCase))
            {
                if (FiresMoon.MoonPrefab == null && VerdantsAscent.FiresValheimGalaxies.assetBundle != null)
                    FiresMoon.TryLoadAssets(VerdantsAscent.FiresValheimGalaxies.assetBundle);
                if (FiresMoon.MoonPrefab != null) return FiresMoon.MoonPrefab;
            }
            // Everything else resolves from Valheim's live prefab registry (vanilla pieces, mod
            // pieces, FAP-baked blueprints) — no shader-mod bundle dependency.
            if (ZNetScene.instance != null)
            {
                var fromScene = ZNetScene.instance.GetPrefab(name);
                if (fromScene != null) return fromScene;
            }
            return null;
        }

        // Clone a prefab as a travelable orbital BODY — full visuals AND colliders
        // (so you can fly out and stand on it), but no networking. Ghost-init so any
        // ZNetView wakes as a ghost (no real ZDO), then strip the networking +
        // gameplay scripts. The orbit is DETERMINISTIC — every client computes the
        // exact same path from world time — so the body needs no ZDO sync; a real
        // ZNetView teleporting every frame would just spam the network.
        private static GameObject InstantiateOrbitalBody(GameObject prefab, string name)
        {
            GameObject go;
            ZNetView.StartGhostInit();
            try { go = Object.Instantiate(prefab); }
            finally { ZNetView.FinishGhostInit(); }
            go.name = name;
            StripToBody(go);
            // The moon prefab's own material is a broken stand-in (Unity 'Standard', texture lost in bundling): unlit by
            // night it rendered as nothing, so the HD moon looked gone (2026-10-05). Give the reachable moon the same
            // Custom/FiresMoon material + albedo the sky disc uses.
            if (prefab == FiresMoon.MoonPrefab)
            {
                int renderers = FiresMoon.ApplyMoonLook(go, out Bounds bounds);
                FiresLogger.LogDebug($"[FiresOrbitals] '{name}' takes the HD moon look: renderers={renderers}, "
                    + $"shader='{(FiresMoon.MoonMaterial != null ? FiresMoon.MoonMaterial.shader.name : "none")}', "
                    + $"albedo='{(FiresMoon.MoonTexture != null ? FiresMoon.MoonTexture.name : "none")}', unitBounds={bounds.size}.");
            }
            LogBodyVisual(go, name);
            go.transform.position = new Vector3(0f, -100000f, 0f);   // park off-screen until the first Tick positions it
            return go;
        }

        private static void StripToBody(GameObject go)
        {
            // KEEP colliders + renderers (visible + landable). Strip only the
            // networking / placement / decay scripts so the moving body can't spawn
            // a ZDO, be hammered, or crumble from "no support" up in the sky.
            foreach (var c in go.GetComponentsInChildren<WearNTear>(true)) { try { Object.DestroyImmediate(c); } catch { } }
            foreach (var c in go.GetComponentsInChildren<Piece>(true))     { try { Object.DestroyImmediate(c); } catch { } }
            foreach (var c in go.GetComponentsInChildren<ZNetView>(true))  { try { Object.DestroyImmediate(c); } catch { } }
        }

        // One-shot visual report so we can SEE why a body renders wrong (flat /
        // unlit / low-detail) instead of guessing: the layer it landed on (lit
        // world layer vs an unlit one), whether it actually has renderers, how
        // many LODGroups it had, and the shader its first material uses (vanilla
        // lit piece shader vs a flat stand-in). Logged once per (re)build.
        private static void LogBodyVisual(GameObject go, string name)
        {
            var mrs  = go.GetComponentsInChildren<MeshRenderer>(true);
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var lods = go.GetComponentsInChildren<LODGroup>(true);
            string firstShader = "none";
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null)
                { firstShader = r.sharedMaterial.shader.name; break; }
            Debug.Log($"[FiresOrbitals] body '{name}' visual: layer={go.layer} ('{LayerMask.LayerToName(go.layer)}'), "
                    + $"meshRenderers={mrs.Length}, skinned={smrs.Length}, lodGroups={lods.Length}, firstShader='{firstShader}'.");
        }

        private static GameObject CreatePlaceholder(int slot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"FiresOrbital_{slot}_placeholder";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            // CreatePrimitive assigns Unity's "Standard" shader, which Valheim's
            // build strips → magenta. Re-skin with a vanilla lit shader if one is
            // loaded (no shader-mod dependency). If none resolve the placeholder
            // keeps the default — it only shows for a slot enabled with no/invalid
            // prefab name (a misconfig), so that's an acceptable fallback.
            var mr = go.GetComponent<MeshRenderer>();
            var ph = FindPlaceholderShader();
            if (mr != null && ph != null)
                mr.sharedMaterial = new Material(ph) { name = "FiresOrbital_placeholder" };
            return go;
        }

        // A vanilla lit shader for the placeholder sphere — no shader-mod bundle.
        private static Shader FindPlaceholderShader()
        {
            foreach (var n in new[] { "Custom/Piece", "Custom/Creature", "Standard" })
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return null;
        }

        private static bool SamePrefab(string a, string b)
            => (a ?? string.Empty) == (b ?? string.Empty);

        // Swap any placeholder whose named prefab has since become loadable
        // (e.g. ZNetScene finished registering FAP-baked pieces after we spawned).
        private static void RetryPendingPrefabs()
        {
            foreach (var b in s_bodies)
            {
                if (!b.IsPlaceholder || string.IsNullOrEmpty(b.P.PrefabName)) continue;
                var prefab = ResolvePrefab(b.P.PrefabName);
                if (prefab == null) continue;

                Vector3 pos = b.Instance != null ? b.Instance.transform.position : Center;
                if (b.Instance != null) Object.Destroy(b.Instance);
                var go = InstantiateOrbitalBody(prefab, $"FiresOrbital_{b.SlotIndex}_{b.P.PrefabName}");
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * Mathf.Max(0.01f, b.P.Scale);
                if (s_parent != null) go.transform.SetParent(s_parent, true);
                b.Instance = go;
                b.IsPlaceholder = false;
                b.UnitRadius = 0f;   // a new mesh: measured again on the next tick
                Debug.Log($"[FiresOrbitals] Slot {b.SlotIndex}: upgraded placeholder to prefab '{b.P.PrefabName}'.");
            }
        }

        private static void TeardownAll()
        {
            foreach (var b in s_bodies)
                if (b.Instance != null) Object.Destroy(b.Instance);
            s_bodies.Clear();
        }

        // fires_moondiag support — report every slot's params, its live body (or
        // lack of one), and where the orbit math puts it RIGHT NOW, so a body
        // that exists but sits beyond the main camera's far plane (or on a layer
        // no camera draws) is visible in numbers.
        public static void DumpState(System.Action<string> L, Vector3 playerPos)
        {
            L($"FiresOrbitals.Enabled={Enabled}  configuredSlots={s_params.Length}  liveBodies={s_bodies.Count}");
            float t = Time.time;
            for (int i = 0; i < s_params.Length; i++)
            {
                var p = s_params[i];
                if (!p.Enabled) { L($"  slot {i}: disabled"); continue; }
                Vector3 orbitPos = ComputePosition(p, t);
                L($"  slot {i}: prefab='{p.PrefabName}' followVanillaMoon={p.FollowVanillaMoon} motion={EffectiveMotion(p)}{(p.Motion == OrbitMotion.Auto ? " (auto)" : "")} " +
                  $"tidalLock={p.TidalLock} scale={p.Scale:F0} " +
                  $"orbitPos=({orbitPos.x:F0},{orbitPos.y:F0},{orbitPos.z:F0}) distFromPlayer={Vector3.Distance(orbitPos, playerPos):F0}");
                var b = s_bodies.Find(x => x.SlotIndex == i);
                if (b == null || b.Instance == null) { L("         body: NONE (not built)"); continue; }
                var go = b.Instance;
                L($"         body: '{go.name}' placeholder={b.IsPlaceholder} active={go.activeInHierarchy} " +
                  $"layer={go.layer} ('{LayerMask.LayerToName(go.layer)}') " +
                  $"pos=({go.transform.position.x:F0},{go.transform.position.y:F0},{go.transform.position.z:F0}) " +
                  $"scale={go.transform.localScale.x:F1}");
            }
        }
    }

    public class FiresOrbitalsDriver : MonoBehaviour
    {
        private void Awake() => FiresOrbitals.SetParent(transform);
        private void Update() => FiresOrbitals.Tick();
    }
}
