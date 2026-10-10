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

            configVerboseLogging = config.Bind(
                "General", "VerboseLogging", false,
                "Enable verbose log output for terrain/water diagnostics.");

            configServerAuthority = config.Bind(
                "General", "ServerAuthority", true,
                "When true, the server forces clients to match its config (ConfigSync lock).");

            configHeightCacheResolution = config.Bind(
                "Water", "HeightCacheResolution", 4096,
                "Grid resolution (X by X) for WorldHeightCache used by auto-water river detection. Range [1024, 20480].");

            configHeightCacheSamplesPerFrame = config.Bind(
                "Water", "HeightCacheSamplesPerFrame", 5000,
                "Samples per frame while building WorldHeightCache. Range [500, 20000].");

            configUseNewTilePersistence = config.Bind(
                "Terrain", "UseNewTilePersistence", false,
                "When true, terrain ops save through the new tile-anchor system (64m anchors at Y=-5000) "
                + "instead of the legacy TerrainCombinerAnchor at Y=0. Default OFF until validated. "
                + "See Docs/TERRAIN_PERSISTENCE_REDESIGN_PLAN.md.");

            configFiresWaterEnabled = config.Bind(
                "FiresWater", "Enabled", true,
                "Master switch for the Custom/FiresWater shader. When true, FAT custom water meshes "
                + "render with the two-pass shader (visible from below with proper wave-synced surface). "
                + "When false, FAT water uses the captured vanilla material as-is (one-sided).");

            configFiresWaterSwapVanilla = config.Bind(
                "FiresWater", "SwapVanillaOcean", true,
                "When true (and Enabled is also true), every vanilla WaterVolume's surface shader is swapped "
                + "to Custom/FiresWater. Affects ALL ocean rendering in the world. The swap is value-preserving "
                + "(all 33 vanilla water properties transfer to the new shader). Flip off for A/B comparison.");

            configFiresWaterUnderwaterEnv = config.Bind(
                "FiresWater", "UnderwaterEnv", true,
                "When true, a custom 'FiresUnderwater' environment is registered with EnvMan and force-"
                + "activated whenever the local player's eye drops below a water surface. Drives fog, sun "
                + "color, ambient light, and wet-status together via the game's own env-blend system "
                + "(2-second crossfade). Independent of the shader Enabled toggle.");

            configOceanRingExtendEnabled = config.Bind(
                "FiresWater", "OceanRingExtendEnabled", true,
                "When true, bump ZoneSystem.m_activeArea while the local player is over deep ocean so the "
                + "animated FiresWater zone meshes cover a wider ring and storm waves don't visibly cut "
                + "off at the vanilla LOD-plane boundary. Reverts to vanilla the moment the player returns "
                + "near shore. Client-only — does not affect other players.");

            configOceanRingExtendedActiveArea = config.Bind(
                "FiresWater", "OceanRingExtendedActiveArea",
                2,
                new ConfigDescription(
                    "ZoneSystem.m_activeArea value used while over deep ocean. 1=vanilla (3x3 zones, ~96m "
                    + "radius), 2=5x5 (~160m), 3=7x7 (~224m). Larger values load more zones around the "
                    + "player so cost more memory/CPU/network while at sea.",
                    new AcceptableValueRange<int>(1, 4)));

            configOceanRingDepthThreshold = config.Bind(
                "FiresWater", "OceanRingDepthThreshold",
                10f,
                new ConfigDescription(
                    "Water column depth (meters below sea level Y=30) at the player's position above which "
                    + "the ring expansion activates. Lower = activates closer to shore (more aggressive). "
                    + "Higher = only in true deep ocean.",
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
