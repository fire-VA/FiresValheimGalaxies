using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    // HD_Additions (Norger / Balrond; not ours) embeds its own copy of Galaxies' firesgalaxies bundle for its moon and loads it in
    // CelestialIntegration.Initialize. With Galaxies installed that bundle is already loaded, so Unity refuses the copy (an ERROR: "another
    // AssetBundle with the same files is already loaded") and HD then logs its own ERROR ("Embedded moon AssetBundle resource ... was not
    // found"); its moon never gets assets either way. Its sun and tides do not use that bundle.
    // 0.1.31 (the lead, Fire 10-05: a clean log): while Galaxies holds the bundle, HD's moon-bundle load is skipped (it returns null, as the
    // refused load did) and HD's log field is held back for that one branch, whose only use of it is a null-checked LogError. It is put back
    // at the very next bundle load (HD's sun) and by a finalizer on Initialize. HD is found by reflection when its assembly loads (BepInEx may
    // load it after this plugin's Awake): absent or changed = nothing patched, one Debug line says why.
    internal static class HdMoonYield
    {
        private const string HdAssembly = "HD_Additions";
        private const string HdType = "BalrondNorgerHDPieces.CelestialIntegration";
        private const string LoadMethod = "LoadEmbeddedAssetBundle", InitMethod = "Initialize", LogField = "_log";
        private const string MoonLabel = "moon";

        private static Harmony s_harmony;
        private static Func<bool> s_galaxiesHoldsBundle;
        private static FieldInfo s_log;
        private static object s_heldLog;
        private static bool s_holding, s_patched, s_said;

        internal static void Install(Harmony harmony, Func<bool> galaxiesHoldsBundle)
        {
            s_harmony = harmony;
            s_galaxiesHoldsBundle = galaxiesHoldsBundle;
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == HdAssembly);
            if (loaded != null) { Patch(loaded); return; }
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (args?.LoadedAssembly == null || args.LoadedAssembly.GetName().Name != HdAssembly) return;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            Patch(args.LoadedAssembly);
        }

        private static void Patch(Assembly hd)
        {
            if (s_patched) return;
            try
            {
                Type type = hd.GetType(HdType, false);
                MethodInfo load = type != null ? AccessTools.Method(type, LoadMethod, new[] { typeof(string), typeof(string) }) : null;
                MethodInfo init = type != null ? AccessTools.Method(type, InitMethod) : null;
                s_log = type != null ? AccessTools.Field(type, LogField) : null;
                if (load == null || load.ReturnType != typeof(AssetBundle) || s_log == null || !s_log.IsStatic)
                {
                    FiresLogger.LogDebug($"[Galaxies] HD_Additions is loaded but its {HdType}.{LoadMethod}(string, string) / {LogField} is not there as expected: its moon bundle load is left alone.");
                    return;
                }
                s_harmony.Patch(load, prefix: new HarmonyMethod(typeof(HdMoonYield), nameof(LoadPrefix)));
                if (init != null) s_harmony.Patch(init, finalizer: new HarmonyMethod(typeof(HdMoonYield), nameof(InitFinalizer)));
                s_patched = true;
            }
            catch (Exception ex)
            {
                FiresLogger.LogDebug($"[Galaxies] HD_Additions moon-bundle patch not applied ({ex.GetType().Name}: {ex.Message}): its moon bundle load is left alone.");
            }
        }

        // HD's parameters are (resourceName, label); its moon is label "moon".
        private static bool LoadPrefix(object[] __args, ref AssetBundle __result)
        {
            Restore();
            string label = __args != null && __args.Length > 1 ? __args[1] as string : null;
            if (label != MoonLabel || s_galaxiesHoldsBundle == null || !s_galaxiesHoldsBundle()) return true;
            s_heldLog = s_log.GetValue(null);
            s_log.SetValue(null, null);
            s_holding = true;
            if (!s_said)
            {
                s_said = true;
                FiresLogger.LogDebug("[Galaxies] HD_Additions' moon bundle load skipped: Galaxies already holds 'firesgalaxies' (Unity would refuse the copy). HD's sun and tides are untouched.");
            }
            __result = null;
            return false;
        }

        private static void InitFinalizer() => Restore();   // a finalizer runs even if Initialize throws; void = the exception goes on unchanged

        private static void Restore()
        {
            if (!s_holding) return;
            s_holding = false;
            if (s_log.GetValue(null) == null) s_log.SetValue(null, s_heldLog);
            s_heldLog = null;
        }
    }
}
