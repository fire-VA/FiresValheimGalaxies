using UnityEngine;

namespace VerdantsAscent.Utilities
{
    // FiresValheimGalaxies' sections for the shared FiresCore help panel (registration only).
    internal static class GalaxiesHelpContent
    {
        private const string ModId = "FiresValheimGalaxies";
        private static bool _registered;

        public static void Register()
        {
            if (_registered || Application.isBatchMode) return;
            _registered = true;

            FiresCore.Help.HelpRegistry.RegisterSection(ModId, "", "The Moon, Tides & Currents", 0, BuildOverview);
            FiresCore.Help.HelpRegistry.RegisterSection(ModId, "", "Sailing the Currents", 1, BuildCurrents);
            FiresCore.Help.HelpRegistry.RegisterSection(ModId, "", "Diving", 2, BuildDiving);
            FiresCore.Help.HelpRegistry.RegisterSection(ModId, "", "Tuning & Diagnostics", 3, BuildAdmin, adminOnly: true);
        }

        private static void BuildOverview(FiresCore.Help.HelpContentWriter w)
        {
            w.Header("The Moon, Tides & Currents");
            w.Paragraph("Valheim Galaxies replaces the sky's painted-on moon with a REAL one — a 3D " +
                "moon with physical phases that drives real tides and ocean currents.");
            w.Divider();

            w.SubHeader("The Moon");
            w.Bullet("A true 3D moon orbits overhead — high at midnight, gone at noon — cycling " +
                "New → Full over a configurable lunar month.");
            w.Bullet("Up to four additional ORBITALS — moons and satellites on tilted orbits you can " +
                "actually fly out to and stand on ([Galaxies - Orbital A-D]).");
            w.Bullet("The galaxy skybox, dome, and aurora visuals ship in Fires Tossin Shade — this " +
                "mod is the moon and the physics.");
            w.Divider();

            w.SubHeader("Tides");
            w.Bullet("The whole ocean rises and falls with the moon — shorelines walk in and out " +
                "through the day.");
            w.Bullet("Spring tides at full and new moon swing deeper both ways.");
            w.Bullet("Rivers and swamps only feel a fraction of the tide, so they never drain dry.");
            w.Bullet("Tide math is deterministic from world time — every player sees the same water.");
            w.Divider();

            w.SubHeader("Whirlpools");
            w.Paragraph("During storms, rotating funnels can open in deep ocean — a visible swirl, an " +
                "inward pull, and a sink core that drags ships down and grinds their hulls. Sail wide " +
                "or ride the rim for a thrill.");
        }

        private static void BuildCurrents(FiresCore.Help.HelpContentWriter w)
        {
            w.Header("Sailing the Currents");
            w.Paragraph("Water MOVES, and it moves your ship:");
            w.Bullet("Rivers push downhill — drift lazily downstream, fight your way up.");
            w.Bullet("Open-ocean gyres — slow, large-scale currents worth riding on long hauls.");
            w.Bullet("Tidal flow — river mouths breathe with the tide: outbound on the ebb, inbound " +
                "on the flood, with a rotating swirl right at the mouth.");
            w.Bullet("Swimmers and creatures feel the drift too, not just ships.");
            w.Divider();

            w.SubHeader("Wind vs Current");
            w.Paragraph("Wind alignment matters: sailing WITH the current and wind is fast; fighting " +
                "the current with bad wind drags hard. Crosscurrents push you off heading — trim " +
                "accordingly.");
            w.Divider();

            w.Paragraph("Admins and hosts can exempt themselves from drift (AdminBypassCurrents); " +
                "god-mode and flight always bypass.");
        }

        private static void BuildDiving(FiresCore.Help.HelpContentWriter w)
        {
            w.Header("Diving");
            w.Paragraph("Swim DOWN. Player swimming is Fires Unified Core's [Swimming] setting " +
                "(Auto turns it on with Galaxies; Off is vanilla swimming):");
            w.Bullet("Hold Crouch — descend");
            w.Bullet("Hold Jump — rise");
            w.Bullet("Let go — hold your depth");
            w.Bullet("Creatures dive too — sea serpents will follow you down " +
                "(CreatureDivingEnabled).");
        }

        private static void BuildAdmin(FiresCore.Help.HelpContentWriter w)
        {
            w.AdminHeader("Tuning & Diagnostics");
            w.SubHeader("Key Config ([Galaxies - *] in F8, live sliders)");
            w.Bullet("General.EnableMod — master switch for the moon/tide/current system");
            w.Bullet("Moon: Distance, SizeScale, SynodicDays (game days per lunar cycle — drives " +
                "phase AND tides), brightness/terminator look");
            w.Bullet("Tides: EnableTides, the High/Low Y levels per moon state, TideCyclesPerOrbit " +
                "(1 = one high+low per day; 2 = Earth-like)");
            w.Bullet("Ships/Rivers/Ocean: current strengths, river detection, wind drag factors");
            w.Bullet("Whirlpools: spawn chance, lifetime, radii, pull/damage, storm environment list");
            w.Bullet("Debug Visualization: VisualizeCurrents draws live current arrows around you; " +
                "VisualizeOrbits draws orbital paths");
            w.AdminDivider();

            w.SubHeader("Diagnostics");
            w.Bullet("fires_moondiag — full moon+orbitals pipeline dump (also to BepInEx/FiresMoonDiag.txt)");
            w.Bullet("galaxies_waterdiag — every water surface near you + tide weld check");
            w.Bullet("galaxies_river [here|scan] — tide/river shaping state; scan finds the nearest " +
                "carved channel");
            w.Bullet("galaxies_whirl here|clear|status — spawn/clear/inspect whirlpools");
            w.AdminDivider();

            w.Paragraph("With FiresAdminTerrain installed, this mod runs ONLY its celestial and " +
                "diving systems — the built-in terrain/water toolset stands down so the two never " +
                "double-patch.");
        }
    }
}
