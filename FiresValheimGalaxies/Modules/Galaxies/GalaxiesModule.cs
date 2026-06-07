using UnityEngine;
// Bind canonical FUC infrastructure for Galaxies. Aliases force the bare
// names FiresLogger / PlayerSpawnGate to resolve to FUC rather than the
// dormant FAT-clone copies in the VerdantsAscent root namespace.
using FiresLogger = FiresCore.Logging.FiresLogger;
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

        // Called from the plugin once Harmony patches are applied.
        public static void StartWorld()
        {
            PlayerSpawnGate.RunWhenLocalReady(30f, _ =>
            {
                // Worldgen river/stream index — built once per world load, used
                // by RiverCurrentCache for true along-channel flow direction.
                WorldgenRivers.Build();
                RiverMouthRegistry.RequestBuild();
                FiresLogger.LogInfo("Galaxies: tide system live, worldgen rivers indexed, river-mouth scan queued.");
            });
        }

        public static void Shutdown()
        {
            RiverCurrentCache.Clear();
            ShipCurrentPatch.ClearSmoothing();
            RiverMouthRegistry.Clear();
            WorldgenRivers.Clear();
            TideSystem.Reset();
        }

        private void Update()
        {
            if (GalaxiesConfig.EnableMod == null || !GalaxiesConfig.EnableMod.Value)
                return;

            TideSystem.Tick();

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
