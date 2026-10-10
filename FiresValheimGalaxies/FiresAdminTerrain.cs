using BepInEx;
using FiresCore.Sync;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VerdantsAscent.Managers;
using VerdantsAscent.Terrain.Persistence;
using VerdantsAscent.Terrain.Utilities;

namespace VerdantsAscent
{
    // Standalone Fires-* mod hosting CustomTerrain + CustomWater. Clutter is
    // delegated to FAP (source-linked) — see csproj for the linked files.
    //
    // Companion / NPC / Quest / Guild / Discord / Wayshrine / Territory init
    // intentionally absent: this mod is admin terrain + water tools only.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    // FiresUnifiedCore is a HARD dependency — Galaxies' own modules use FUC's
    // canonical PlayerSpawnGate / FrameBudgetScheduler / FiresLogger via
    // file-top `using` aliases. Declaring it hard guarantees FUC's Awake runs
    // first so its statics are live by the time our Setup runs.
    [BepInDependency(FiresCore.FiresUnifiedCore.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    // Soft dependencies — these mods are not required for FAT to load, but
    // we integrate with them when present. Declaring them gives r2modman /
    // Thunderstore proper "depends on" metadata AND lets BepInEx order our
    // Awake AFTER theirs, so reflective lookups (AdminCheck, IH ruler
    // patches, WEC undo bindings) hit fully-initialized targets.
    [BepInDependency("com.Fire.FiresAdminTerrain", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.InfinityHammer", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.serversync", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.worldeditcommands", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("org.bepinex.plugins.serverdevcommands", BepInDependency.DependencyFlags.SoftDependency)]
    public class FiresValheimGalaxies : BaseUnityPlugin
    {
        public const string PluginGUID = "com.Fire.FiresValheimGalaxies";
        public const string PluginName = "FiresValheimGalaxies";
        public const string PluginVersion = "0.1.39";

        public static readonly Harmony harmony = new Harmony(PluginGUID);
        public static FiresValheimGalaxies Instance { get; private set; }

        public static AssetBundle assetBundle;
        public static GameObject RootObject;
        public static GameObject PrefabContainer { get; private set; }

        public ConfigSync configSync;
        private static ILogHandler _originalLogHandler;

        // BepInEx log source — banner emitters route through this (not Debug.Log)
        // so banner lines don't stdout-echo a raw white console duplicate.
        public static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            // BIG ringed-planet banner — fires FIRST so it sits at
            // the top of the Galaxies console output as the load
            // announcement. The compact planet banner fires once the
            // world is live to bookend the load with a "ready"
            // confirmation. See FiresGalaxiesBanner for the content.
            try { VerdantsAscent.Utilities.FiresGalaxiesBanner.PrintBig(); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{PluginName}] FiresGalaxiesBanner.PrintBig failed: {ex.Message}");
            }

            Debug.Log($"[{PluginName}] Awake() entered — version {PluginVersion}");
            Instance = this;

            // Coexistence gate: when the standalone FiresAdminTerrain plugin is
            // also installed it owns the water / terrain / sky systems, so we run
            // ONLY the moon-driven tide + current add-on and let FAT drive the
            // rest. When FAT is absent, Galaxies drives those systems itself and
            // runs as a standalone mod.
            bool fatPresent = VerdantsAscent.Galaxies.Coexistence.FatPresent;
            FiresLogger.LogVerbose($"FiresAdminTerrain {(fatPresent ? "DETECTED — deferring water/terrain/sky to FAT, running tides only" : "absent — running full standalone water/terrain/sky + tides")}.");

            InstallLogFilter();

            RootObject = new GameObject("_FiresAdminTerrainRoot");
            UnityEngine.Object.DontDestroyOnLoad(RootObject);

            // Shader drivers (water shader, sky, underwater, ocean ring) removed —
            // shaders now live in FiresTossinShade. Galaxies keeps WaterMaterialManager
            // (the gameplay material provider), which pulls the FiresWater wrap through
            // FiresCore's WaterShaderBridge when the shader mod is installed.

            PrefabContainer = new GameObject("FiresAdminTerrain_Prefabs");
            PrefabContainer.transform.parent = RootObject.transform;
            PrefabContainer.SetActive(false);

            ConfigManager.Instance.Initialize(Config);

            configSync = new ConfigSync(PluginGUID)
            {
                DisplayName = PluginName,
                CurrentVersion = PluginVersion,
                MinimumRequiredVersion = PluginVersion
            };
            configSync.lockedConfigChanged += ConfigManager.Instance.OnConfigLockChanged;
            configSync.AddLockingConfigEntry(ConfigManager.Instance.configServerAuthority);
            configSync.AddConfigEntry(ConfigManager.Instance.configVerboseLogging);
            configSync.AddConfigEntry(ConfigManager.Instance.configHeightCacheResolution);
            configSync.AddConfigEntry(ConfigManager.Instance.configHeightCacheSamplesPerFrame);
            configSync.AddConfigEntry(ConfigManager.Instance.configUseNewTilePersistence);
            configSync.AddConfigEntry(ConfigManager.Instance.configFiresWaterEnabled);
            configSync.AddConfigEntry(ConfigManager.Instance.configFiresWaterSwapVanilla);
            configSync.AddConfigEntry(ConfigManager.Instance.configFiresWaterUnderwaterEnv);

            // Moon-driven tide + current system. Binds its own config (server-
            // locked on the physics/visual entries) and hosts the per-frame tide
            // tick. Reads FiresMoon (source-linked) for phase/orbit.
            VerdantsAscent.Galaxies.GalaxiesConfig.Bind(Config, configSync);
            RootObject.AddComponent<VerdantsAscent.Galaxies.GalaxiesModule>();
            // 3D moon — Galaxies-owned, loads from the firesgalaxies bundle, drives
            // the tides + renders physical phases. Self-gates on MoonEnabled.
            RootObject.AddComponent<VerdantsAscent.Water.Core.FiresMoonDriver>();
            // Configurable orbiting bodies (satellites / ships) — Galaxies-owned.
            RootObject.AddComponent<VerdantsAscent.Water.Core.FiresOrbitalsDriver>();
            // GL debug overlay for currents + orbits. Always attached; both
            // toggles default off so it costs nothing until you flip one on.
            RootObject.AddComponent<VerdantsAscent.Galaxies.GalaxiesDebugVisualizer>();

            LoadAssets();
            // HD_Additions' copy of this bundle would be refused (two ERROR lines a load): skip its moon load while we hold ours.
            VerdantsAscent.Galaxies.HdMoonYield.Install(harmony, () => assetBundle != null);

            FiresCore.Materials.TerrainMaterialCache.MaterialCaptured += OnTerrainMaterialCaptured;

            // Tile-restore batch tracker — starts an infinite polling
            // coroutine that emits the 🪨 TILES banner whenever a wave
            // of tile restores (TileAnchor.Load calls) has settled.
            // Per-tile success logs are verbose-only; the batch banner
            // is the default-visible summary. See VATileRestoreBatch
            // for the debounce model.
            StartCoroutine(VerdantsAscent.Utilities.VATileRestoreBatch.PollAndFlushCoroutine());

            if (ZNet.instance && ZNet.instance.IsDedicated())
                StartCoroutine(DelayedDedicatedServerInit());
            else
                FinishInit();
        }


        private void InstallLogFilter()
        {
            // FiresUnifiedCore installs the one shared rate-limit/suppression log handler now
            // (FiresCore.Logging.RateLimitedLogHandler). This per-mod copy was collapsed into Core so the handler
            // chain stays a single wrapper instead of one-per-mod — intentionally a no-op.
        }

        private IEnumerator DelayedDedicatedServerInit()
        {
            while (ZNetScene.instance == null || ObjectDB.instance == null)
                yield return null;
            yield return new WaitForEndOfFrame();
            FinishInit();
        }

        // Patch classes whose method bodies reference client-only Unity types
        // (Hud, Minimap, InventoryGui, FejdStartup, etc.). On a dedicated
        // server the Mono runtime crashes natively when Harmony's IL rewriter
        // tries to resolve those references. Same pattern as Ascend.cs in the
        // original FiresRPGmaker mod.
        private static readonly HashSet<string> _clientOnlyPatchTypes = new HashSet<string>
        {
            // Phase F: legacy DirtFloorClientPatches entry removed (the
            // DirtFloorPatches type is deleted). Add new client-only patch
            // type names here as we identify them on the dedi path.
        };

        private static bool IsClientOnlyPatch(Type type)
        {
            for (var t = type; t != null; t = t.DeclaringType)
            {
                if (_clientOnlyPatchTypes.Contains(t.FullName))
                    return true;
            }
            return false;
        }

        // Galaxies' own Harmony patch types (the tide/current add-on + the diving
        // module, which relocated here from FAT). When the real FAT plugin is
        // present we apply ONLY these and let FAT own the rest.
        private static bool IsGalaxiesPatchType(Type type)
        {
            return type.Namespace != null
                && (type.Namespace.StartsWith("VerdantsAscent.Galaxies", StringComparison.Ordinal)
                    || type.Namespace.StartsWith("VerdantsAscent.Diving", StringComparison.Ordinal));
        }

        private void FinishInit()
        {
            bool isDedicatedServer = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
            bool fatPresent = VerdantsAscent.Galaxies.Coexistence.FatPresent;

            try
            {
                if (!isDedicatedServer && !fatPresent)
                {
                    harmony.PatchAll();
                }
                else
                {
                    int patched = 0;
                    int skipped = 0;
                    var assembly = Assembly.GetExecutingAssembly();

                    foreach (var type in assembly.GetTypes())
                    {
                        // FAT present: apply ONLY our Galaxies patches; FAT owns
                        // the rest, so patching its targets here would double-patch.
                        if (fatPresent && !IsGalaxiesPatchType(type))
                        {
                            skipped++;
                            continue;
                        }
                        if (IsClientOnlyPatch(type))
                        {
                            skipped++;
                            continue;
                        }

                        try
                        {
                            harmony.CreateClassProcessor(type).Patch();
                            patched++;
                        }
                        catch (Exception ex)
                        {
                            if (type.GetCustomAttribute<HarmonyPatch>() != null ||
                                type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                            {
                                Debug.LogWarning($"[{PluginName}] Failed to apply Harmony patch {type.FullName}: {ex.Message}");
                            }
                        }
                    }

                    Debug.Log($"[{PluginName}] Selective patch (FAT present={fatPresent}, dedicated={isDedicatedServer}): patched {patched} types, skipped {skipped}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{PluginName}] harmony.PatchAll threw an exception: {ex}");
            }

            // Initialize the legacy biome / paint / terrain RPC systems that
            // were ported verbatim from RPG-maker. The [HarmonyPatch] classes
            // inside these files auto-register via harmony.PatchAll above, but
            // their RPC handlers + console commands + file-cache loaders need
            // explicit Initialize calls.
            //
            // Without these:
            //   * TerrainPaintRPC's biome/clutter/paint RPCs aren't registered,
            //     so MP biome sync silently no-ops on the receiver side.
            //   * BiomeFilePersistence's on-disk biome-paint cache is never
            //     loaded, so biome paints on vanilla terrain don't survive
            //     world reload.
            //   * BiomeOverlayPainter's paint_lava / paint_snow / paint_biome
            //     console commands aren't registered.
            //
            // Phase F: legacy TerrainPaintRPC removed. RPC sync now flows
            // through TileBrushRPC (tile ops) + RegionScrubberRPC (clear).

            // Track which sub-system inits succeeded so the 🔧 COMMANDS
            // mini-banner at the end can summarize. Each int is 1 if
            // the corresponding Initialize/RegisterCommands call
            // returned without throwing, 0 otherwise. Per-success
            // detail logs are verbose-only — the banner conveys the
            // aggregate count for normal loads.
            // FAT-coexistence: skip ALL of FAT's terrain/biome/ice/snow command
            // and persistence init when the real FAT plugin is loaded — it
            // registers these itself, and double-registration collides on console
            // commands and terrain ZDO persistence. Standalone Galaxies runs them.
            int biomePersistOK = 0, biomePainterOK = 0, iceToolOK = 0, landSnowOK = 0;
            if (!fatPresent)
            {

            try
            {
                VerdantsAscent.Terrain.Biome.BiomeFilePersistence.Initialize();
                biomePersistOK = 1;
                if (FiresLogger.VerboseEnabled)
                    Debug.Log($"[{PluginName}] BiomeFilePersistence initialized.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{PluginName}] BiomeFilePersistence.Initialize threw: {ex.Message}");
            }

            if (!isDedicatedServer)
            {
                try
                {
                    VerdantsAscent.Terrain.Biome.BiomeOverlayPainter.RegisterCommands();
                    biomePainterOK = 1;
                    if (FiresLogger.VerboseEnabled)
                        Debug.Log($"[{PluginName}] BiomeOverlayPainter console commands registered.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{PluginName}] BiomeOverlayPainter.RegisterCommands threw: {ex.Message}");
                }

                // 2026-05-25 round-19: WIRE UP the LandIce / FloatingIce
                // pipeline that's been sitting dead in the assembly since
                // the original port. IcePlacementTool.Initialize() registers
                // place_ice / place_ice_land / place_ice_water / delete_ice /
                // break_standalone_ice, and chains to LandIceCommands +
                // FloatingIceCommands which register the height-mod
                // commands (ice_land_raise / lower / level / smooth /
                // delete_ice_land + floating equivalents). Also registers
                // the new LandSnowCommands for the terrain-anchor snow.
                try
                {
                    VerdantsAscent.Water.Tools.IcePlacementTool.Initialize();
                    iceToolOK = 1;
                    if (FiresLogger.VerboseEnabled)
                        Debug.Log($"[{PluginName}] IcePlacementTool + LandIce/FloatingIce commands registered.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{PluginName}] IcePlacementTool.Initialize threw: {ex.Message}");
                }

                try
                {
                    VerdantsAscent.Water.Tools.LandSnowCommands.Initialize();
                    landSnowOK = 1;
                    if (FiresLogger.VerboseEnabled)
                        Debug.Log($"[{PluginName}] LandSnowCommands registered (terrain-anchor snow, no water).");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{PluginName}] LandSnowCommands.Initialize threw: {ex.Message}");
                }
            }

            }

            // Moon-driven tide system: queue the river-mouth scan once the local
            // player is ready. Harmony patches (ship currents + water-surface tide
            // offset) registered above via PatchAll.
            try { VerdantsAscent.Galaxies.GalaxiesModule.StartWorld(); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{PluginName}] GalaxiesModule.StartWorld failed: {ex.Message}");
            }

            // 🌌 GALAXIES mini-box — single visual summary of the tide add-on
            // plus the command sub-systems that came up. Per-line details
            // remain available via the verbose gate.
            int commandSubsystems = biomePersistOK + biomePainterOK + iceToolOK + landSnowOK;
            FiresCore.Logging.LoadSummary.For(PluginName).EmitMiniBox("🌌 GALAXIES", new[]
            {
                "tide system live",
                "worldgen rivers indexed",
                "river-mouth scan queued",
                fatPresent ? "cmds: owned by FAT" : $"cmd subsystems ready: {commandSubsystems}",
                fatPresent ? "deferring to FAT" : "full mode",
            });

            try { VerdantsAscent.Utilities.GalaxiesHelpContent.Register(); }
            catch (Exception ex) { Debug.LogWarning($"[{PluginName}] Help registration failed: {ex.Message}"); }

            // Compact "ready" banner — small ringed planet with
            // brand panel. Deferred to WORLD LOAD time (when
            // ZNetScene is up) so it fires in lockstep with FAP's
            // and FGN's compact banners. The BIG "loading" banner
            // already fired at the top of Awake; the compact one
            // bookends the load once the world is actually live.
            StartCoroutine(EmitCompactBannerWhenZNetReady());
        }

        // Waits for ZNetScene + prefab table to be live, then emits
        // the compact ringed-planet brand banner. Failure is
        // non-fatal — falls back to the plain "mod loaded" log so
        // the load event is still recorded.
        private System.Collections.IEnumerator EmitCompactBannerWhenZNetReady()
        {
            while (ZNetScene.instance == null
                   || ZNetScene.instance.m_prefabs == null
                   || ZNetScene.instance.m_prefabs.Count == 0)
            {
                yield return null;
            }
            yield return new WaitForEndOfFrame();

            try { VerdantsAscent.Utilities.FiresGalaxiesBanner.Print(); }
            catch (Exception ex)
            {
                Debug.Log($"[{PluginName}] mod loaded. (banner failed: {ex.Message})");
            }
        }

        private void OnDestroy()
        {
            FiresCore.Materials.TerrainMaterialCache.MaterialCaptured -= OnTerrainMaterialCaptured;

            if (assetBundle != null)
            {
                try { assetBundle.Unload(true); }
                catch (Exception ex) { Debug.LogError($"[{PluginName}] AssetBundle unload error: {ex}"); }
                finally { assetBundle = null; }
            }

            if (RootObject != null)
            {
                UnityEngine.Object.Destroy(RootObject);
                RootObject = null;
            }

            try { harmony?.UnpatchSelf(); } catch { }

            try { VerdantsAscent.Galaxies.GalaxiesModule.Shutdown(); } catch { }

            try { ConfigManager.Instance?.Dispose(); } catch { }

            configSync = null;
            Instance = null;
        }

        public AssetBundle GetAssetBundle() => assetBundle;

        // Tiles that spawned on the fallback move to the captured material in one pass.
        private static void OnTerrainMaterialCaptured(Material material)
        {
            TileMeshRenderer.UpgradeAllRenderersToCachedMaterial();
        }

        private const string BundleName = "firesgalaxies";

        private void LoadAssets()
        {
            if (assetBundle != null) return;

            try
            {
                ReleaseForeignCopies();
                assetBundle = GetAssetBundleFromResources(BundleName);
                if (assetBundle == null)
                {
                    Debug.LogWarning($"[{PluginName}] Embedded asset bundle '{BundleName}' not found — the moon will not render.");
                    return;
                }
                Debug.Log($"[{PluginName}] Loaded asset bundle: {assetBundle.name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{PluginName}] Asset load failed: {ex}");
            }
        }

        // HD_Additions embeds a copy of this bundle and loads it at its own start, and Unity refuses a second bundle with
        // the same files, so whichever loaded second lost its moon. Galaxies owns the moon and tides (Fire, 2026-09-27):
        // another mod's copy is released with Unload(false), which keeps whatever that mod already took out of it alive.
        private static void ReleaseForeignCopies()
        {
            var copies = new List<AssetBundle>();
            foreach (var loaded in AssetBundle.GetAllLoadedAssetBundles())
                if (loaded != null && loaded.name == BundleName) copies.Add(loaded);
            foreach (var copy in copies)
            {
                Debug.LogWarning($"[{PluginName}] another mod already loaded a copy of '{BundleName}' (HD_Additions ships one); released it so Galaxies loads its own.");
                copy.Unload(false);
            }
        }

        private AssetBundle GetAssetBundleFromResources(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                Debug.LogError($"[{PluginName}] No embedded resource ending with '{name}'.");
                return null;
            }

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                using var ms = new System.IO.MemoryStream();
                stream.CopyTo(ms);
                return AssetBundle.LoadFromMemory(ms.ToArray());   // full copy — disposed-stream lazy re-read crashes the dedi
            }
        }
    }
}
