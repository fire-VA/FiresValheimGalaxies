using System;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;
using VerdantsAscent.Water.Core;

namespace VerdantsAscent.Galaxies
{
    /// <summary>
    /// `fires_moondiag` — one-shot dump of the ENTIRE moon pipeline so "we never
    /// see our moon" can be diagnosed from numbers instead of guesses: bundle
    /// contents, per-asset load results, every gate (enabled/suppress/master-fx),
    /// the live MoonInstance (layer/position/scale/renderers/material), every
    /// camera's layer-19 + clip-plane state, the active skybox material, and all
    /// orbital slots. Writes to console + BepInEx/FiresMoonDiag.txt.
    /// </summary>
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    internal static class FiresMoonDiagCommand
    {
        private static bool s_reg;

        private static void Postfix()
        {
            if (s_reg) return;
            s_reg = true;

            new Terminal.ConsoleCommand("fires_moondiag",
                "Dump the full moon/orbitals pipeline state to console + BepInEx/FiresMoonDiag.txt.",
                args =>
                {
                    var sb = new StringBuilder(8192);
                    void L(string s) { args.Context.AddString(s); sb.AppendLine(s); }

                    try { Dump(L); }
                    catch (Exception ex) { L("[moondiag] EXCEPTION: " + ex); }

                    string path;
                    try { path = Path.GetFullPath(Path.Combine(BepInEx.Paths.ConfigPath, "..", "FiresMoonDiag.txt")); }
                    catch { path = "FiresMoonDiag.txt"; }
                    try { File.WriteAllText(path, sb.ToString()); args.Context.AddString("[moondiag] wrote " + path); }
                    catch (Exception e) { args.Context.AddString("[moondiag] FILE WRITE FAILED: " + e.Message); }
                },
                isCheat: true);   // 0.1.34: admin / devcommands only, as the help says
        }

        private static string V(Vector3 v) => $"({v.x:F1},{v.y:F1},{v.z:F1})";

        private static void Dump(Action<string> L)
        {
            var player = Player.m_localPlayer;
            Vector3 ppos = player != null ? player.transform.position : Vector3.zero;
            var cam = Utils.GetMainCamera();

            L($"=== MOON DIAG @ player{V(ppos)} ===");

            // ---- 1. Bundle ----
            var bundle = VerdantsAscent.FiresValheimGalaxies.assetBundle;
            L("--- BUNDLE ---");
            if (bundle == null)
            {
                L("assetBundle = NULL — LoadAssets never ran or the embedded resource is missing. Moon cannot load.");
            }
            else
            {
                L($"assetBundle = '{bundle.name}'");
                try { foreach (var n in bundle.GetAllAssetNames()) L("  asset: " + n); }
                catch (Exception ex) { L("  GetAllAssetNames threw: " + ex.Message); }
            }

            // ---- 2. Asset load results ----
            L("--- MOON ASSETS ---");
            bool loaded = FiresMoon.TryLoadAssets(bundle);
            L($"TryLoadAssets => {loaded}");
            L("MoonShader  = " + (FiresMoon.MoonShader == null ? "NULL  <-- moon silently disabled"
                : $"'{FiresMoon.MoonShader.name}' isSupported={FiresMoon.MoonShader.isSupported}"));
            L("MoonPrefab  = " + (FiresMoon.MoonPrefab == null ? "NULL  <-- moon silently disabled"
                : $"'{FiresMoon.MoonPrefab.name}'"));
            L("MoonTexture = " + (FiresMoon.MoonTexture == null ? "NULL  <-- moon silently disabled"
                : $"'{FiresMoon.MoonTexture.name}' {FiresMoon.MoonTexture.width}x{FiresMoon.MoonTexture.height}"));

            // ---- 3. Gates ----
            L("--- GATES ---");
            float masterOff = Shader.GetGlobalFloat("_FiresMasterFxOff");
            L($"FiresMoon.Enabled={FiresMoon.Enabled}  SuppressDisc={FiresMoon.SuppressDisc}  VisitMode={FiresMoon.VisitMode}");
            L($"_FiresMasterFxOff={masterOff:F0}  (1 = FTS master switch OFF -> moon+orbitals stand down)");
            L($"Distance={FiresMoon.Distance:F0}  SizeScale={FiresMoon.SizeScale:F2}  " +
              $"TargetAngularDiameterDeg={FiresMoon.TargetAngularDiameterDeg:F2}  PhaseOverride={FiresMoon.PhaseOverride:F2}");
            L($"cfg MoonEnabled={(GalaxiesConfig.MoonEnabled != null ? GalaxiesConfig.MoonEnabled.Value.ToString() : "?")}  " +
              $"(ConfigSync may have pushed the server's value)");

            var root = VerdantsAscent.FiresValheimGalaxies.RootObject;
            var drv  = root != null ? root.GetComponent<FiresMoonDriver>() : null;
            var odrv = root != null ? root.GetComponent<FiresOrbitalsDriver>() : null;
            L($"RootObject={(root == null ? "NULL" : root.activeInHierarchy ? "active" : "INACTIVE")}  " +
              $"FiresMoonDriver={(drv == null ? "MISSING" : drv.enabled ? "enabled" : "DISABLED")}  " +
              $"FiresOrbitalsDriver={(odrv == null ? "MISSING" : odrv.enabled ? "enabled" : "DISABLED")}");

            // ---- 4. Sky math ----
            L("--- SKY MATH ---");
            Vector4 sunVec = Shader.GetGlobalVector("_SkyboxSunDir");
            Vector3 sunDir = new Vector3(sunVec.x, sunVec.y, sunVec.z);
            L($"_SkyboxSunDir={V(sunDir)} mag={sunDir.magnitude:F3} (all-zero = EnvMan not driving it)");
            float phase = FiresMoon.ComputePhase01();
            Vector3 moonDir = FiresMoon.ComputeMoonDirection(sunDir.sqrMagnitude > 1e-6f ? sunDir.normalized : Vector3.right, phase);
            L($"phase01={phase:F3} (0=new 0.5=full)  moonDir={V(moonDir)}  " +
              $"{(moonDir.y > 0f ? "ABOVE horizon (should be visible)" : "BELOW horizon (not visible right now — normal by day)")}");
            if (cam != null)
                L($"expected disc pos={V(cam.transform.position + moonDir * FiresMoon.Distance)}");
            string envName = "?";
            float dayFrac = -1f;
            if (EnvMan.instance != null)
            {
                var env = EnvMan.instance.GetCurrentEnvironment();
                if (env != null) envName = env.m_name;
                dayFrac = EnvMan.instance.GetDayFraction();
            }
            L($"env='{envName}'  dayFraction={dayFrac:F3} (~0.25 sunrise, ~0.75 sunset)");

            // ---- 5. Force a tick, then instance state ----
            L("--- TICK + INSTANCE ---");
            if (cam == null) L("main camera = NULL — Tick can't run");
            else
            {
                try { FiresMoon.Tick(cam); L("FiresMoon.Tick(cam) invoked OK"); }
                catch (Exception ex) { L("FiresMoon.Tick THREW: " + ex); }
            }
            var mi = FiresMoon.MoonInstance;
            if (mi == null)
            {
                L(FiresMoon.SuppressDisc
                    ? "MoonInstance = NULL by design: SuppressDisc=True hides the skybox disc, and the orbital body that follows the vanilla moon (ORBITALS below) is the moon."
                    : "MoonInstance = NULL - EnsureInstance never ran; see gates/assets above for which check failed.");
            }
            else
            {
                L($"MoonInstance '{mi.name}' activeSelf={mi.activeSelf} activeInHierarchy={mi.activeInHierarchy} " +
                  $"layer={mi.layer} ('{LayerMask.LayerToName(mi.layer)}')");
                L($"  pos={V(mi.transform.position)}  distFromCam={(cam != null ? Vector3.Distance(mi.transform.position, cam.transform.position) : -1f):F0}  " +
                  $"scale={mi.transform.localScale.x:F2}");
                var rends = mi.GetComponentsInChildren<Renderer>(true);
                L($"  renderers={rends.Length}{(rends.Length == 0 ? "  <-- FBX has NO renderers, nothing can draw" : "")}");
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    var mat = r.sharedMaterial;
                    string sh = mat != null && mat.shader != null ? mat.shader.name : "none";
                    bool supported = mat != null && mat.shader != null && mat.shader.isSupported;
                    var b = r.bounds.size;
                    L($"    r '{r.gameObject.name}': enabled={r.enabled} isVisible={r.isVisible} " +
                      $"bounds=({b.x:F1},{b.y:F1},{b.z:F1}) mat='{(mat != null ? mat.name : "NULL")}' " +
                      $"shader='{sh}' supported={supported}");
                }
            }

            // ---- 6. Cameras: who renders layer 19? ----
            L("--- CAMERAS (moon lives on layer 19 'skybox') ---");
            var cams = UnityEngine.Object.FindObjectsOfType<Camera>(true);
            bool anyRenders19 = false;
            foreach (var c in cams)
            {
                if (c == null) continue;
                bool r19 = (c.cullingMask & (1 << 19)) != 0;
                if (r19 && c.isActiveAndEnabled) anyRenders19 = true;
                L($"  '{c.gameObject.name}': enabled={c.isActiveAndEnabled} depth={c.depth:F0} " +
                  $"near={c.nearClipPlane:F1} far={c.farClipPlane:F0} rendersLayer19={r19} pos={V(c.transform.position)}");
            }
            if (!anyRenders19)
                L("  !! NO active camera renders layer 19 — the disc moon can NEVER appear. This is the bug.");

            // ---- 7. Skybox material ----
            L("--- SKYBOX MATERIAL ---");
            var sky = RenderSettings.skybox;
            if (sky == null) L("RenderSettings.skybox = NULL");
            else
            {
                L($"skybox mat='{sky.name}' shader='{(sky.shader != null ? sky.shader.name : "NULL")}'");
                foreach (var prop in new[] { "_HideVanillaMoon", "_MoonSize", "_MoonColor" })
                {
                    if (!sky.HasProperty(prop)) { L($"  {prop}: (absent)"); continue; }
                    if (prop == "_MoonColor") { var mc = sky.GetColor(prop); L($"  {prop}=({mc.r:F2},{mc.g:F2},{mc.b:F2})"); }
                    else L($"  {prop}={sky.GetFloat(prop):F3}");
                }
            }

            // ---- 8. Orbitals ----
            L("--- ORBITALS ---");
            FiresOrbitals.DumpState(L, ppos);
        }
    }
}
