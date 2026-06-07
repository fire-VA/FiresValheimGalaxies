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
    }

    public static class FiresOrbitals
    {
        public static bool Enabled = true;
        public static Vector3 Center = Vector3.zero;   // Valheim world centre

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
            if (!Enabled) { TeardownAll(); return; }
            if (s_dirty) { Rebuild(); s_dirty = false; }

            // A prefab named in the config but not yet in the bundle at spawn time
            // shows the placeholder; once the bundle has it, upgrade in place.
            if (Time.time >= s_nextPrefabRetry)
            {
                s_nextPrefabRetry = Time.time + 2f;
                RetryPendingPrefabs();
            }

            float t = Time.time;
            foreach (var b in s_bodies)
            {
                if (b.Instance == null) continue;
                b.Instance.transform.position = ComputePosition(b.P, t);
                b.Instance.transform.rotation = Quaternion.Euler(0f, t * b.P.SpinDegPerSec, 0f);
            }
        }

        public static Vector3 ComputePosition(OrbitParams p, float t)
        {
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
            // Galaxies-owned orbitals resolve from Valheim's live prefab registry
            // only (vanilla pieces, mod pieces, FAP-baked blueprints) — no shader-
            // mod bundle dependency. Custom orbital models can ship in a Galaxies
            // bundle later and be added here.
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
                Debug.Log($"[FiresOrbitals] Slot {b.SlotIndex}: upgraded placeholder to prefab '{b.P.PrefabName}'.");
            }
        }

        private static void TeardownAll()
        {
            foreach (var b in s_bodies)
                if (b.Instance != null) Object.Destroy(b.Instance);
            s_bodies.Clear();
        }
    }

    public class FiresOrbitalsDriver : MonoBehaviour
    {
        private void Awake() => FiresOrbitals.SetParent(transform);
        private void Update() => FiresOrbitals.Tick();
    }
}
