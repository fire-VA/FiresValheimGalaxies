using UnityEngine;
// PlayerSpawnGate binds to canonical FUC infrastructure; FiresLogger binds
// to the mod's own core-backed wrapper so lines carry the
// FiresValheimGalaxies tag and gate on this mod's verbose config.
using FiresLogger = VerdantsAscent.FiresLogger;
using PlayerSpawnGate = FiresCore.Lifecycle.PlayerSpawnGate;

namespace VerdantsAscent.Galaxies
{
    // Runtime host for the tide/current system. Attached to the plugin's
    // DontDestroyOnLoad root. Ticks the deterministic tide math every frame and
    // runs periodic cache maintenance. The river-mouth scan is queued once the
    // local player is ready (worldgen available, zone stream safe to read).
    internal sealed class GalaxiesModule : MonoBehaviour
    {
        private const float CleanupIntervalSeconds = 60f;
        private float _lastCleanup;
        private bool _ringStoodDown;   // 0.1.62

        // Publish the tide/current state to the FiresCore water contract so any mod
        // (swimming, ships, FAT ponds) can query water height/flow without referencing
        // Galaxies. Cleared on destroy so consumers fall back to vanilla water.
        private void Awake()
        {
            FiresCore.Services.WaterState.Register(new GalaxiesWaterState());
            // Surface-shaping field the FiresWater shore-field baker samples so the rendered
            // mesh bends to match the buoyancy weld. This MUST apply the EXACT same feature
            // gates as WaterVolumeTidePatch — otherwise the visual bends by the field while
            // buoyancy uses the uniform tide (or vice versa), and the two surfaces split into
            // the "pinned level + moving level" double-layer. With both shaping features off,
            // this returns (1,0) everywhere → the shader's tide == the uniform buoyancy tide.
            FiresCore.Services.WaterState.SurfaceFieldSampler = (x, z) =>
            {
                bool shape = GalaxiesConfig.TideShapingEnabled != null && GalaxiesConfig.TideShapingEnabled.Value;
                bool riv   = GalaxiesConfig.ExtendedRiversEnabled != null && GalaxiesConfig.ExtendedRiversEnabled.Value;
                float influence = shape ? TideRiverField.TideInfluence(x, z) : 1f;
                float raise     = riv   ? TideRiverField.RiverRaise(x, z)     : 0f;
                return new UnityEngine.Vector2(influence, raise);
            };
        }

        private void OnDestroy()
        {
            FiresCore.Services.WaterState.Clear();
            FiresCore.Services.WaterState.SurfaceFieldSampler = null;
        }

        // Called from the plugin once Harmony patches are applied.
        public static void StartWorld()
        {
            PlayerSpawnGate.RunWhenLocalReady(30f, _ =>
            {
                // Worldgen river/stream index — built once per world load, used
                // by RiverCurrentCache for true along-channel flow direction.
                WorldgenRivers.Build();
                RiverMouthRegistry.RequestBuild();
                FiresLogger.LogVerbose("Galaxies: tide system live, worldgen rivers indexed, river-mouth scan queued.");
            });
        }

        // 0.1.55 ([seasons], Fire 10-15 via [lead]: the FAD direct hop): the world caches (rivers, river mouths, whirlpools, tide state) were
        // built once per PROCESS, so after a hop world B ran on world A's data. They now go with the world (ZNet.OnDestroy) and are built
        // again for the next one (ZNet.Start; the first world is still started by the plugin's FinishInit). Vedr's external tide offset
        // channel is NOT cleared (TideSystem.Reset leaves it).
        [HarmonyLib.HarmonyPatch(typeof(ZNet), "OnDestroy")]
        static class WorldLeftPatch
        {
            static void Postfix() { s_worldsLeft++; Shutdown(); UnityEngine.Debug.Log($"[FiresValheimGalaxies] [Galaxies] world caches cleared (world #{s_worldsLeft} left)"); }
        }

        [HarmonyLib.HarmonyPatch(typeof(ZNet), "Start")]
        static class WorldStartPatch
        {
            static void Postfix()
            {
                if (s_worldsLeft == 0) return;   // the first world: FinishInit starts it
                StartWorld();
                UnityEngine.Debug.Log($"[FiresValheimGalaxies] [Galaxies] world caches rebuilt for the next world (after {s_worldsLeft} left)");
            }
        }
        static int s_worldsLeft;

        public static void Shutdown()
        {
            WhirlpoolSystem.Clear();
            RiverCurrentCache.Clear();
            ShipCurrentPatch.ClearSmoothing();
            RiverMouthRegistry.Clear();
            WorldgenRivers.Clear();
            TideRiverField.Clear();
            TideSystem.Reset();
        }

        private void Update()
        {
            if (GalaxiesConfig.EnableMod == null || !GalaxiesConfig.EnableMod.Value)
                return;

            // 0.1.62: a halo ring world has no sea; tides, currents and whirlpools stand down (their caches cleared once)
            if (RingWorld.Active)
            {
                if (!_ringStoodDown) { _ringStoodDown = true; Shutdown(); }
                return;
            }
            if (_ringStoodDown) { _ringStoodDown = false; StartWorld(); }

            GalaxiesConfig.PollVedrSeasons();        // 0.1.33: the lunar cycle follows Vedr's season length (2 s throttle inside)
            VerdantsAscent.Water.Core.SeasonalWaterFreeze.Tick();   // 0.1.39: Vedr winter freezes/thaws Galaxies custom water (2 s throttle inside)
            TideSystem.Tick();
            WhirlpoolSpawner.Tick();                // storm-gated deterministic spawns (1s throttle inside)
            WhirlpoolSystem.PushShaderGlobals();     // _FiresWhirl* globals for FiresWater's funnel visual

            if (Time.time - _lastCleanup >= CleanupIntervalSeconds)
            {
                _lastCleanup = Time.time;
                RiverCurrentCache.Cleanup(GalaxiesConfig.RiverCacheLifetimeSeconds.Value);
                ShipCurrentPatch.PruneStaleSmoothing();
                RiverMouthRegistry.MaintenanceTick();
            }
        }
    }
}
