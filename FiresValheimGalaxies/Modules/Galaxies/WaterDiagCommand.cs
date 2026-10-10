using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    /// <summary>
    /// `galaxies_waterdiag` — one-shot dump of EVERY water surface around the player,
    /// so the double-layer / pinned-vs-moving bug can be diagnosed from NUMBERS instead
    /// of screenshots. Prints the buoyancy surface level, the tide weld check, the
    /// sampled WaterVolume — AND enumerates every MeshRenderer wearing a water shader
    /// within range (include-inactive), sorted by height, reporting for each whether it
    /// is ANIMATED (_depth != 0, _UseGlobalWind = 1) or PINNED/FLAT (_depth = 0,
    /// _UseGlobalWind = 0). Two entries at different Y = the double layer, and the flags
    /// say which one is the static one.
    ///
    /// Writes the full dump to a FILE (BepInEx/FiresWaterDiag.txt) as well as the console
    /// + log, so the exact numbers survive the session and can be handed over verbatim.
    /// </summary>
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    internal static class WaterDiagCommand
    {
        private static bool s_reg;

        private static void Postfix()
        {
            if (s_reg) return;
            s_reg = true;

            new Terminal.ConsoleCommand("galaxies_waterdiag",
                "Dump EVERY water surface at the player to console + BepInEx/FiresWaterDiag.txt (double-layer diagnosis).",
                args =>
                {
                    var p = Player.m_localPlayer;
                    if (p == null) { args.Context.AddString("[waterdiag] no local player"); return; }
                    Vector3 pos = p.transform.position;

                    var sb = new StringBuilder(4096);
                    void L(string s) { args.Context.AddString(s); FiresLogger.LogInfo(s); sb.AppendLine(s); }

                    L($"=== WATER DIAG @ ({pos.x:F1},{pos.y:F1},{pos.z:F1}) ===");

                    // Biome + config gates
                    var biome = WorldGenerator.instance != null
                        ? WorldGenerator.instance.GetBiome(pos.x, pos.z).ToString() : "?";
                    bool shape = GalaxiesConfig.TideShapingEnabled != null && GalaxiesConfig.TideShapingEnabled.Value;
                    bool riv   = GalaxiesConfig.ExtendedRiversEnabled != null && GalaxiesConfig.ExtendedRiversEnabled.Value;
                    L($"biome={biome}  TideShaping={shape}  ExtendedRivers={riv}  " +
                      $"RiverTideFraction={(GalaxiesConfig.RiverTideFraction != null ? GalaxiesConfig.RiverTideFraction.Value : -1f):F2}");

                    // Tide (C#) + shader globals
                    float surfaceOffset = TideSystem.SurfaceOffset;
                    float tideGlobal = Shader.GetGlobalFloat("_FiresTideOffset");
                    float fieldOn    = Shader.GetGlobalFloat("_FiresTideRiverFieldOn");
                    L($"TideSystem.SurfaceOffset={surfaceOffset:F3}  shader _FiresTideOffset={tideGlobal:F3}  _FiresTideRiverFieldOn={fieldOn:F0}");

                    // The tide/river FIELD, CPU side (what buoyancy uses)
                    float influence = TideRiverField.TideInfluence(pos.x, pos.z);
                    float raise      = TideRiverField.RiverRaise(pos.x, pos.z);
                    L($"CPU field: TideInfluence={influence:F3}  RiverRaise={raise:F3}");
                    float buoyAdd = surfaceOffset * (shape ? influence : 1f) + (riv ? raise : 0f);
                    float visAdd  = tideGlobal * (fieldOn > 0.5f ? influence : 1f) + (fieldOn > 0.5f ? raise : 0f);
                    L($"WELD CHECK: buoyancy adds {buoyAdd:F3}  vs  visual adds ~{visAdd:F3}  (diff {(buoyAdd - visAdd):F3} — want 0)");

                    // Buoyancy surface level (patched) + the WaterVolume material
                    WaterVolume wv = null;
                    float buoyLevel = Floating.GetWaterLevel(pos, ref wv);
                    L($"buoyancy water level (GetWaterLevel) = {buoyLevel:F3}   (sea level c_WaterLevel={ZoneSystem.c_WaterLevel:F1})");
                    if (wv != null && wv.m_waterSurface != null && wv.m_waterSurface.sharedMaterial != null)
                    {
                        var mat = wv.m_waterSurface.sharedMaterial;
                        string sh = mat.shader != null ? mat.shader.name : "null";
                        float uwg = mat.HasProperty("_UseGlobalWind") ? mat.GetFloat("_UseGlobalWind") : -1f;
                        L($"player WaterVolume surface: shader='{sh}'  _UseGlobalWind={uwg:F0}  rendererEnabled={wv.m_waterSurface.enabled}");
                    }
                    else L("player WaterVolume: none / no surface material at this position");

                    // Vanilla LOD plane (the classic pinned flat surface)
                    var lod = GameObject.Find("_GameMain/_Environment/WaterPlane");
                    if (lod != null) L($"vanilla WaterPlane (LOD): FOUND, activeSelf={lod.activeSelf}, y={lod.transform.position.y:F1}");
                    else L("vanilla WaterPlane (LOD): not found (hidden/deactivated — good) OR name changed");

                    // ── FULL ENUMERATION: every water renderer near the player ──────────
                    // This is what a single-point probe cannot see. Find EVERY MeshRenderer
                    // (include inactive) whose shader/name is water-ish, within RangeXZ, and
                    // report height + animated/flat flags. Two rows at different Y = the
                    // double layer; the flags say which one is pinned.
                    const float RangeXZ = 400f;
                    L($"--- ALL WATER RENDERERS within {RangeXZ:F0}m (sorted by height) ---");

                    var rows = new List<(float y, string line)>();
                    var all = Object.FindObjectsOfType<MeshRenderer>(true);
                    int scanned = 0;
                    foreach (var mr in all)
                    {
                        if (mr == null) continue;
                        var mat = mr.sharedMaterial;
                        string shName = (mat != null && mat.shader != null) ? mat.shader.name : "";
                        string goName = mr.gameObject.name;
                        bool waterish =
                            shName.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            goName.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!waterish) continue;

                        Vector3 rp = mr.transform.position;
                        float dxz = Vector2.Distance(new Vector2(rp.x, rp.z), new Vector2(pos.x, pos.z));
                        if (dxz > RangeXZ) continue;
                        scanned++;

                        // Animated vs flat: FiresWater/vanilla water displaces from _depth[]
                        // and _UseGlobalWind. All-zero _depth + wind 0 = a pinned flat sheet.
                        string depthStr = "n/a";
                        if (mat != null)
                        {
                            try
                            {
                                var d = mat.GetFloatArray("_depth");
                                if (d != null && d.Length > 0) depthStr = $"[{d[0]:F2}..]";
                            }
                            catch { /* prop absent on this shader */ }
                        }
                        float wind = (mat != null && mat.HasProperty("_UseGlobalWind")) ? mat.GetFloat("_UseGlobalWind") : -1f;
                        float isLod = (mat != null && mat.HasProperty("_IsLod")) ? mat.GetFloat("_IsLod") : -1f;
                        bool flat = depthStr == "[0.00..]" && Mathf.Approximately(wind, 0f);

                        rows.Add((rp.y,
                            $"  y={rp.y,7:F2}  d={dxz,6:F1}m  {(mr.enabled ? "ON " : "off")} " +
                            $"actv={(mr.gameObject.activeInHierarchy ? "Y" : "n")} " +
                            $"{(flat ? "FLAT/PINNED" : "animated  ")} " +
                            $"depth={depthStr} wind={wind:F0} isLod={isLod:F0} " +
                            $"sh='{shName}' go='{goName}'"));
                    }
                    rows.Sort((a, b) => b.y.CompareTo(a.y));   // highest first
                    foreach (var r in rows) L(r.line);
                    L($"  ({scanned} water renderer(s) found; total MeshRenderers scanned={all.Length})");

                    // ── FiresWater subsystem live state (FTS statics via reflection so
                    // Galaxies doesn't need a compile ref to FTS) ───────────────────────
                    DumpFtsStatic(L, "VerdantsAscent.Water.Core.FiresWaterExtendedRing", "Enabled", "RadiusMeters", "HideLodWater");
                    DumpFtsStatic(L, "VerdantsAscent.Water.Core.FiresWaterZoneTiles", "Enabled", "ExtendedZones");
                    DumpFtsStatic(L, "VerdantsAscent.Water.Core.FiresWaterFlatWorldPlane", "Enabled");

                    // Write the file
                    try
                    {
                        string path = Path.Combine(BepInEx.Paths.ConfigPath, "..", "FiresWaterDiag.txt");
                        path = Path.GetFullPath(path);
                        File.WriteAllText(path, sb.ToString());
                        args.Context.AddString($"[waterdiag] written to {path}");
                        FiresLogger.LogInfo($"[waterdiag] written to {path}");
                    }
                    catch (System.Exception e)
                    {
                        args.Context.AddString($"[waterdiag] FILE WRITE FAILED: {e.Message}");
                        FiresLogger.LogWarning($"[waterdiag] file write failed: {e}");
                    }
                },
                isCheat: true);   // 0.1.34: admin / devcommands only, as the help says

            FiresLogger.LogInfo("[waterdiag] console command 'galaxies_waterdiag' registered.");
        }

        // Read one or more public static fields off an FTS type by reflection and print
        // them. FTS is a sibling plugin loaded in the same domain; no compile-time ref.
        private static void DumpFtsStatic(System.Action<string> L, string typeName, params string[] fields)
        {
            var t = AccessTools.TypeByName(typeName);
            if (t == null) { L($"{typeName}: <type not loaded>"); return; }
            var sb = new StringBuilder();
            sb.Append(typeName.Substring(typeName.LastIndexOf('.') + 1)).Append(": ");
            for (int i = 0; i < fields.Length; i++)
            {
                var f = AccessTools.Field(t, fields[i]);
                object v = f != null ? f.GetValue(null) : "<no field>";
                sb.Append(fields[i]).Append('=').Append(v);
                if (i < fields.Length - 1) sb.Append("  ");
            }
            L(sb.ToString());
        }
    }
}
