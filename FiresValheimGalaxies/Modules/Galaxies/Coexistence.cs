using BepInEx.Bootstrap;

namespace VerdantsAscent.Galaxies
{
    // Detects whether the standalone FiresAdminTerrain plugin is loaded. When it
    // is, Galaxies defers all water / terrain / sky systems to FAT and runs only
    // the tide + current add-on. When FAT is absent, Galaxies drives those
    // systems itself (it carries the same modules) and runs as a standalone mod.
    internal static class Coexistence
    {
        public const string FatGuid = "com.Fire.FiresAdminTerrain";

        private static bool _checked;
        private static bool _fatPresent;

        public static bool FatPresent
        {
            get
            {
                if (!_checked)
                {
                    _checked = true;
                    _fatPresent = Chainloader.PluginInfos != null
                                  && Chainloader.PluginInfos.ContainsKey(FatGuid);
                }
                return _fatPresent;
            }
        }
    }
}
