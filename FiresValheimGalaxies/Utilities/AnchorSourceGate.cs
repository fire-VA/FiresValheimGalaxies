using UnityEngine;

namespace VerdantsAscent
{
    /// <summary>
    /// 0.1.72 ([lead] 11-08, Fire's BlueHills perf stack without FiresAdminTerrain: four ERROR lines at load, "[TileAnchorPrefab] LoadSourcePrefab:
    /// cannot find source prefab 'piece_dirtfloor_vad'" and "[Water/LandIce/LandSnowAnchorPrefab] Source prefab 'piece_dirtfloor_vad' not available").
    /// Galaxies' terrain tile, water, land ice and land snow anchors are clones of FiresAdminTerrain's 'piece_dirtfloor_vad' piece. Galaxies' own
    /// bundle does not carry it, so without FAT these anchors (FAT's terrain / water features) have no source. That is a missing dependency, not
    /// an error. This gate asks once, up front, before any anchor tries to load or clone its source: the source is in Galaxies' bundle (no load,
    /// AssetBundle.Contains) or registered in ZNetScene (FAT loaded). When it is absent, every anchor stands down and one Info line says why, once
    /// per world load. When it is present, nothing changes.
    /// </summary>
    internal static class AnchorSourceGate
    {
        internal const string SourcePrefab = "piece_dirtfloor_vad";
        private static ZNetScene s_saidFor;

        /// <summary>True when the anchors' source piece can be loaded. Logs one Info line per ZNetScene when it can't.</summary>
        internal static bool Available(string anchor)
        {
            var scene = ZNetScene.instance;
            if (scene == null) return false;
            bool inBundle = false;
            try { var b = FiresValheimGalaxies.assetBundle; inBundle = b != null && b.Contains(SourcePrefab); } catch { inBundle = false; }
            if (inBundle || scene.GetPrefab(SourcePrefab) != null) return true;
            if (s_saidFor != scene)
            {
                s_saidFor = scene;
                FiresLogger.LogInfo($"[Anchors] '{SourcePrefab}' (FiresAdminTerrain's piece) is not loaded, so FiresAdminTerrain is not installed: Galaxies' terrain tile, water, land ice and land snow anchors stand down (first asked by '{anchor}'). These are FiresAdminTerrain features; nothing else in Galaxies needs them.");
            }
            return false;
        }
    }
}
