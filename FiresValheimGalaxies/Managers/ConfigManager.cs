using BepInEx.Configuration;
using System;

namespace VerdantsAscent.Managers
{
    // Slim ConfigManager for FiresValheimGalaxies — exposes only the configs
    // that CustomTerrain and CustomWater actually consume. The original
    // FiresRPGmaker ConfigManager had hundreds of entries for companions,
    // quests, territory, progression, etc. — none of which apply here.
    //
    // ConfigSync integration: configServerAuthority is the locking toggle
    // (server forces clients to match); the rest are unlocked client-side
    // tunables. Hook configSync.lockedConfigChanged → OnConfigLockChanged
    // in the plugin Awake so admin changes propagate.
    public sealed class ConfigManager : IDisposable
    {
        private static ConfigManager _instance;
        public static ConfigManager Instance => _instance ??= new ConfigManager();

        public ConfigEntry<bool> configVerboseLogging;
        public ConfigEntry<bool> configServerAuthority;
        public ConfigEntry<int> configHeightCacheResolution;
        public ConfigEntry<int> configHeightCacheSamplesPerFrame;

        // Phase 8d toggle: route terrain ops through the new TileAnchor system
        // (Y=-5000 tile-aligned ZDOs) instead of the legacy TerrainCombinerAnchor
        // path. Default OFF until the new path is validated in multiplayer.
        // When ON, op entry points (disk place / paint / height ops) write through
        // TileAnchor.Spawn + ApplyOpAndSave per overlapping 64m tile; the old
        // TerrainCompStorage / TerrainPersistence / DirtFloorSaveManager calls
        // become no-ops. See TERRAIN_PERSISTENCE_REDESIGN_PLAN.md §6.2.
        public ConfigEntry<bool> configUseNewTilePersistence;

        // Custom/FiresWater shader controls. Plugin Awake binds settingChanged
        // handlers to push these into the matching static fields on
        // FiresWaterShaderLoader / FiresUnderwaterEnv. Default ON during
        // bring-up so flipping the bundle in immediately produces visible
        // changes; flip off in the BepInEx config UI if you need to A/B
        // against vanilla.
        public ConfigEntry<bool> configFiresWaterEnabled;
        public ConfigEntry<bool> configFiresWaterSwapVanilla;
        public ConfigEntry<bool> configFiresWaterUnderwaterEnv;

        // Active-zone-area expansion while sailing over deep ocean. Extends
        // the inner ring of animated WaterVolume meshes so storm-wave crests
        // don't reach the vanilla LOD-plane boundary and visually cut off.
        // Reverts to vanilla the moment the player returns near shore.
        public ConfigEntry<bool> configOceanRingExtendEnabled;
        public ConfigEntry<int> configOceanRingExtendedActiveArea;
        public ConfigEntry<float> configOceanRingDepthThreshold;

        public event Action ConfigLockChanged;

        public void Initialize(ConfigFile config)
        {
            if (config == null) return;

            // 0.1.56 (Fire 10-20 player-facing config rule): short plain descriptions; UseNewTilePersistence (internal switch) hidden.
            configVerboseLogging = config.Bind(
                "Galaxies - General", "VerboseLogging", false,
                "Write extra terrain and water diagnostics to the log.");

            configServerAuthority = config.Bind(
                "Galaxies - General", "ServerAuthority", true,
                "The server makes every client use its settings.");

            configHeightCacheResolution = config.Bind(
                "Galaxies - Water", "HeightCacheResolution", 4096,
                new ConfigDescription("Detail of the height map used to find rivers.", new AcceptableValueRange<int>(1024, 20480)));

            configHeightCacheSamplesPerFrame = config.Bind(
                "Galaxies - Water", "HeightCacheSamplesPerFrame", 5000,
                new ConfigDescription("Height samples per frame while the river map builds.", new AcceptableValueRange<int>(500, 20000)));

            configUseNewTilePersistence = config.Bind(
                "Galaxies - General", "UseNewTilePersistence", false,
                new ConfigDescription("Save terrain edits with the new tile system (internal, unfinished).", null,
                    new System.ComponentModel.BrowsableAttribute(false)));

            // 0.1.57 (Discord how-to audit): nothing reads this any more (the water look moved to TossinShade); kept bound (saved values,
            // server sync) but hidden from the settings window.
            configFiresWaterEnabled = config.Bind(
                "Galaxies - Water", "Enabled", true,
                new ConfigDescription("No longer used (the water look is TossinShade's).", null, new System.ComponentModel.BrowsableAttribute(false)));

            configFiresWaterSwapVanilla = config.Bind(
                "Galaxies - Water", "SwapVanillaOcean", true,
                "Use the Galaxies water shader for the whole vanilla ocean too.");

            configFiresWaterUnderwaterEnv = config.Bind(
                "Galaxies - Water", "UnderwaterEnv", true,
                "Underwater fog, light and colour while your view is below water.");

            configOceanRingExtendEnabled = config.Bind(
                "Galaxies - Water", "OceanRingExtendEnabled", true,
                "Load a wider ring of ocean at sea so storm waves do not cut off.");

            configOceanRingExtendedActiveArea = config.Bind(
                "Galaxies - Water", "OceanRingExtendedActiveArea",
                2,
                new ConfigDescription(
                    "Ring size at sea: 1 = vanilla, 2 = 5x5 zones, 3 = 7x7; bigger costs more.",
                    new AcceptableValueRange<int>(1, 4)));

            configOceanRingDepthThreshold = config.Bind(
                "Galaxies - Water", "OceanRingDepthThreshold",
                10f,
                new ConfigDescription(
                    "Water depth (m) below you before the wider ring turns on.",
                    new AcceptableValueRange<float>(1f, 30f)));

            // NOTE: this ConfigManager once carried "FiresWater/Skybox*" A/B-test entries cloned
            // from FiresTossinShade. The skybox driver never shipped in Galaxies, so they were
            // DEAD config that only looked functional (and misled a sky-flash investigation,
            // 2026-07-09). Removed — the live skybox overrides are FTS's ("10 - FiresSky" etc.).
        }

        // Wired via configSync.lockedConfigChanged += Instance.OnConfigLockChanged.
        // ConfigSync's lockedConfigChanged is an Action (no payload) — fires whenever
        // the server's lock state changes; the locked vs unlocked decision lives on
        // ConfigSync.IsLocked / IsSourceOfTruth at the moment of inspection.
        public void OnConfigLockChanged()
        {
            ConfigLockChanged?.Invoke();
        }

        public void Dispose()
        {
            // Reserved for future FileSystemWatcher / async resources.
        }
    }
}
