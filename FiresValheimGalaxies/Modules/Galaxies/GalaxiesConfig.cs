using BepInEx.Configuration;
using FiresCore.Sync;

namespace VerdantsAscent.Galaxies
{
    // All config for the moon-driven tide + current system. Self-contained so
    // it doesn't bloat the shared ConfigManager. Bind() is called once from the
    // plugin Awake with the BepInEx ConfigFile and the live ConfigSync; every
    // entry is registered for sync, and the plugin's single master lock
    // (configServerAuthority) makes them server-authoritative when enabled.
    // Configuration Manager reads these fields by name from the description's tags (0.1.41: IsAdvanced on the debug overlay).
    internal sealed class ConfigurationManagerAttributes
    {
        public bool? IsAdvanced;
    }

    internal static class GalaxiesConfig
    {
        private const string General   = "Galaxies - General";
        private const string Ships     = "Galaxies - Ships";
        private const string Wind      = "Galaxies - Wind";
        private const string Rivers    = "Galaxies - Rivers";
        private const string RiverScan = "Galaxies - Rivers";   // 0.1.41: merged, migrated in GalaxiesConfig.MigrateMovedKeys
        private const string Ocean     = "Galaxies - Open Ocean";
        private const string Tides     = "Galaxies - Tides";
        private const string TideRange = "Galaxies - Tides";   // 0.1.41: merged, migrated in GalaxiesConfig.MigrateMovedKeys
        private const string TideCycle = "Galaxies - Tides";   // 0.1.41: merged, migrated in GalaxiesConfig.MigrateMovedKeys
        private const string Tidal     = "Galaxies - Tides";   // 0.1.41: merged, migrated in GalaxiesConfig.MigrateMovedKeys
        private const string Mouths    = "Galaxies - Rivers";   // 0.1.41: merged, migrated in GalaxiesConfig.MigrateMovedKeys
        private const string Diving    = "Galaxies - Diving";
        private const string Moon      = "Galaxies - Moon";
        private const string Whirl     = "Galaxies - Whirlpools";
        private const string TideShape = "Galaxies - Rivers";   // 0.1.41: merged, migrated in GalaxiesConfig.MigrateMovedKeys

        public static ConfigEntry<bool> EnableMod;

        // Tidal mouths (feature A): keep rivers/swamps from draining at low tide.
        public static ConfigEntry<bool>  TideShapingEnabled;
        public static ConfigEntry<float> RiverTideFraction;   // 0 = rivers ignore tide entirely; 1 = full ocean tide
        public static ConfigEntry<float> TideMouthBlend;      // metres of ocean↔inland crossfade at the mouth

        // Extended rivers (feature B): bend the water plane UP along river spines.
        public static ConfigEntry<bool>  ExtendedRiversEnabled;   // MASTER toggle — default OFF (experimental)
        public static ConfigEntry<float> RiverMaxClimb;           // hard cap (m) on how far above Y30 water may rise
        public static ConfigEntry<float> RiverBankMargin;         // keep the surface at least this far below the lower bank
        public static ConfigEntry<float> RiverBankSampleWiden;    // sample banks this * halfWidth out (>1 = beyond the channel)


        public static ConfigEntry<bool>  MoonEnabled;
        public static ConfigEntry<float> MoonDistance;
        public static ConfigEntry<float> MoonSizeScale;
        public static ConfigEntry<float> MoonSynodicDays;
        public static ConfigEntry<float> MoonOrbitInclination;
        public static ConfigEntry<float> MoonPhaseOffset;
        public static ConfigEntry<float> MoonSunBrightness;
        public static ConfigEntry<float> MoonDarkSide;
        public static ConfigEntry<float> MoonEmission;
        public static ConfigEntry<float> MoonTerminatorSoft;
        public static ConfigEntry<float> MoonSaturation;
        public static ConfigEntry<bool>  MoonFollowVedr;
        public static ConfigEntry<float> MoonCyclesPerSeason;

        public const int OrbitalSlots = 4;
        public static ConfigEntry<bool>[]   OrbitalEnabled    = new ConfigEntry<bool>[OrbitalSlots];
        public static ConfigEntry<string>[] OrbitalPrefab     = new ConfigEntry<string>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalWidth      = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalApex       = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalNadir      = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalPeriod     = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalEggRatio   = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalWobbleAmp  = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalWobbleSec  = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalPrecession = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalPhase      = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalSpin       = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalScale      = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalTilt       = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalYaw        = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<VerdantsAscent.Water.Core.OrbitMotion>[] OrbitalMotion = new ConfigEntry<VerdantsAscent.Water.Core.OrbitMotion>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalLockAz     = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<float>[]  OrbitalLockEl     = new ConfigEntry<float>[OrbitalSlots];
        public static ConfigEntry<bool>[]   OrbitalTidalLock  = new ConfigEntry<bool>[OrbitalSlots];
        private static readonly string[] OrbitalSections =
            { "Galaxies - Orbital A", "Galaxies - Orbital B", "Galaxies - Orbital C", "Galaxies - Orbital D" };

        public static ConfigEntry<bool>  EnableShipCurrents;
        public static ConfigEntry<float> ShipWaterHeightTolerance;
        public static ConfigEntry<float> MaxCurrentSpeedContribution;
        public static ConfigEntry<float> CurrentDirectionSmoothing;
        public static ConfigEntry<bool>  EnablePlayerCurrents;
        public static ConfigEntry<bool>  EnableCreatureCurrents;
        public static ConfigEntry<float> CharacterCurrentScale;
        public static ConfigEntry<bool>  AdminBypassCurrents;

        public static ConfigEntry<float> WindBehindThreshold;
        public static ConfigEntry<float> WindAgainstThreshold;
        public static ConfigEntry<float> BadWindCurrentMultiplier;
        public static ConfigEntry<float> AgainstCurrentDrag;
        public static ConfigEntry<float> CrossCurrentDrag;

        public static ConfigEntry<bool>  EnableRiverCurrents;
        public static ConfigEntry<float> RiverCurrentMinStrength;
        public static ConfigEntry<float> RiverCurrentMaxStrength;
        public static ConfigEntry<float> RiverCacheCellSize;
        public static ConfigEntry<float> RiverCacheLifetimeSeconds;
        public static ConfigEntry<bool>  RiverBilinearBlend;
        public static ConfigEntry<float> RiverScanRadius;
        public static ConfigEntry<float> RiverMinDepth;
        public static ConfigEntry<float> RiverMaxDepth;
        public static ConfigEntry<float> RiverMinSlopeMagnitude;
        public static ConfigEntry<float> RiverBankConfirmDepth;

        public static ConfigEntry<bool>  EnableOceanCurrents;
        public static ConfigEntry<float> OceanCurrentStrength;
        public static ConfigEntry<float> OceanNoiseScale;
        public static ConfigEntry<float> OceanNoiseSeed;

        public static ConfigEntry<bool>  EnableTides;
        // 0.1.36 (Fire: Galaxies and Vedr must never BOTH drive the ocean): exactly one owner of the sea level, tides and currents.
        public static ConfigEntry<string> OceanOwner;
        public static bool GalaxiesOwnsOcean => OceanOwner == null || OceanOwner.Value != "Vedr";
        // 0.1.37 (Fire: Vedr's events stay on and layer on top): the combined sea-level offset (Galaxies tide + Vedr flood/drought) is
        // clamped to this many metres either side of the normal sea.
        public static ConfigEntry<float> MaxCombinedSeaOffset;
        public static ConfigEntry<bool>  EnableTideHeightApply;
        public static ConfigEntry<float> NormalHighTideY;
        public static ConfigEntry<float> NormalLowTideY;
        public static ConfigEntry<float> SpringHighTideY;
        public static ConfigEntry<float> SpringLowTideY;
        public static ConfigEntry<float> BloodMoonHighTideY;
        public static ConfigEntry<float> BloodMoonLowTideY;
        // 0.1.59 (Fire 10-20: "no blood-moon event, so the blood-moon tide heights are unused" -> Galaxies adds its own)
        public static ConfigEntry<bool> BloodMoonEnabled;
        public static ConfigEntry<int> BloodMoonOneIn;
        public static ConfigEntry<float> TideCyclesPerOrbit;

        public static ConfigEntry<bool>  EnableTidalCurrents;
        public static ConfigEntry<float> OutgoingCurrentStrength;
        public static ConfigEntry<float> IncomingCurrentStrength;
        public static ConfigEntry<float> RiverMouthSwirlRadius;
        public static ConfigEntry<float> RiverMouthSwirlStrength;

        public static ConfigEntry<float> RiverMouthScanCellSize;
        public static ConfigEntry<float> RiverMouthMaxBuildSeconds;

        public static ConfigEntry<bool>   WhirlpoolsEnabled;
        public static ConfigEntry<string> WhirlpoolStormEnvironments;
        public static ConfigEntry<float>  WhirlpoolSpawnChance;
        public static ConfigEntry<float>  WhirlpoolBlockSeconds;
        public static ConfigEntry<float>  WhirlpoolScanRadius;
        public static ConfigEntry<float>  WhirlpoolMinOceanDepth;
        public static ConfigEntry<int>    WhirlpoolMaxActive;
        public static ConfigEntry<float>  WhirlpoolOuterRadius;
        public static ConfigEntry<float>  WhirlpoolCoreRadius;
        public static ConfigEntry<float>  WhirlpoolTangentialSpeed;
        public static ConfigEntry<float>  WhirlpoolRadialSpeed;
        public static ConfigEntry<float>  WhirlpoolShipSinkForce;
        public static ConfigEntry<float>  WhirlpoolShipDamage;
        public static ConfigEntry<float>  WhirlpoolFunnelDepth;
        public static ConfigEntry<float>  WhirlpoolSpiralArms;
        public static ConfigEntry<float>  WhirlpoolSpiralSpeed;
        public static ConfigEntry<float>  WhirlpoolRidgeHeight;

        public static ConfigEntry<bool>  DebugVisualizeCurrents;
        public static ConfigEntry<float> DebugCurrentsGridSpacing;
        public static ConfigEntry<float> DebugCurrentsGridRadius;
        public static ConfigEntry<float> DebugCurrentsArrowScale;
        public static ConfigEntry<int>   DebugCurrentsStreamlineSteps;
        public static ConfigEntry<bool>  DebugVisualizeOrbits;
        public static ConfigEntry<int>   DebugOrbitsResolution;

        public static void Bind(ConfigFile config, ConfigSync sync)
        {
            if (config == null) return;

            OceanOwner = config.Bind(General, "OceanOwner", "Galaxies",
                // 0.1.56 (Fire 10-20 player-facing config rule): long descriptions in this file cut to one short line.
                new ConfigDescription("Who drives the sea: Galaxies (tides, currents, Vedr events on top) or Vedr only.",
                    new AcceptableValueList<string>("Galaxies", "Vedr")));
            Sync(sync, OceanOwner);
            MaxCombinedSeaOffset = config.Bind(General, "MaxCombinedSeaOffset", 3.5f,
                new ConfigDescription("Most the sea may sit above or below its normal level when Galaxies' tide and a Vedr flood/drought add up (metres).",
                    new AcceptableValueRange<float>(0.5f, 10f)));
            Sync(sync, MaxCombinedSeaOffset);
            EnableMod = config.Bind(General, "EnableMod", true,
                "Master switch for the moon-driven tide and current system.");
            Sync(sync, EnableMod);

            // ── Tidal mouths (A) + extended rivers (B) — CLIENT-LOCAL while experimental ──
            // Deliberately NOT ServerSync'd: the shaping is DETERMINISTIC from worldgen (every
            // client computes the identical field), and leaving these client-local lets you toggle
            // and A/B them live against a server without the ServerAuthority lock reverting them.
            // When the feature graduates, add Sync back so a server can pin the look for everyone.
            TideShapingEnabled = config.Bind(TideShape, "TideShapingEnabled", true,
                "Rivers and swamps get only part of the tide so they stay sailable at low tide.");
            RiverTideFraction = config.Bind(TideShape, "RiverTideFraction", 0.1f,
                new ConfigDescription("Share of the ocean tide that reaches rivers and swamps (0 = none, 1 = full).",
                    new AcceptableValueRange<float>(0f, 1f)));
            TideMouthBlend = config.Bind(TideShape, "TideMouthBlend", 48f,
                new ConfigDescription("Width (m) over which the tide fades out at a river mouth.",
                    new AcceptableValueRange<float>(8f, 200f)));

            ExtendedRiversEnabled = config.Bind(TideShape, "ExtendedRiversEnabled", false,
                "Experimental: rivers rise above sea level inland, kept below their banks.");
            RiverMaxClimb = config.Bind(TideShape, "RiverMaxClimb", 6f,
                new ConfigDescription("Most (m) a river surface may rise above sea level.",
                    new AcceptableValueRange<float>(0f, 40f)));
            RiverBankMargin = config.Bind(TideShape, "RiverBankMargin", 1.0f,
                new ConfigDescription("Keep a raised river at least this far (m) below its lower bank.",
                    new AcceptableValueRange<float>(0.25f, 5f)));
            RiverBankSampleWiden = config.Bind(TideShape, "RiverBankSampleWiden", 1.15f,
                new ConfigDescription("How far out the banks are checked (1 = water edge, higher = safer).",
                    new AcceptableValueRange<float>(1f, 2.5f)));
            // 0.1.59 ([lead] / Fire 10-20 HOW TO limits: "river tide-shaping keys are per player, the server doesn't sync them"): graduated -
            // the server pins the river look for everyone, like every other river key above (same Sync helper, same ServerAuthority lock).
            Sync(sync, TideShapingEnabled); Sync(sync, RiverTideFraction); Sync(sync, TideMouthBlend);
            Sync(sync, ExtendedRiversEnabled); Sync(sync, RiverMaxClimb); Sync(sync, RiverBankMargin); Sync(sync, RiverBankSampleWiden);

            // Any shaping change invalidates the cached field + forces the shore-field baker to
            // re-bake, so toggles/sliders apply live (no relog). The version bump is what
            // FiresShoreField watches across the mod boundary.
            System.EventHandler reshape = (_, __) =>
            {
                TideRiverField.Clear();
                FiresCore.Services.WaterState.SurfaceFieldVersion++;
            };
            TideShapingEnabled.SettingChanged += reshape;
            RiverTideFraction.SettingChanged += reshape;
            TideMouthBlend.SettingChanged += reshape;
            ExtendedRiversEnabled.SettingChanged += reshape;
            RiverMaxClimb.SettingChanged += reshape;
            RiverBankMargin.SettingChanged += reshape;
            RiverBankSampleWiden.SettingChanged += reshape;

            WhirlpoolsEnabled = config.Bind(Whirl, "WhirlpoolsEnabled", true,
                "Rare storm whirlpools that swirl, pull in and can sink ships.");
            Sync(sync, WhirlpoolsEnabled);
            WhirlpoolStormEnvironments = config.Bind(Whirl, "StormEnvironments", "ThunderStorm,Ashlands_SeaStorm,Ashlands_storm",
                "Comma-separated environment names during which whirlpools may spawn.");
            WhirlpoolSpawnChance = config.Bind(Whirl, "SpawnChancePerZone", 0.02f,
                "Chance per ocean zone per storm window that a whirlpool forms (0.02 = rare).");
            WhirlpoolBlockSeconds = config.Bind(Whirl, "LifetimeSeconds", 180f,
                new ConfigDescription("How long one whirlpool roll lives (it swells in and dies down inside this window).", new AcceptableValueRange<float>(10f, 1800f)));
            WhirlpoolScanRadius = config.Bind(Whirl, "ScanRadius", 320f,
                new ConfigDescription("How far around the player the spawner evaluates ocean zones.", new AcceptableValueRange<float>(50f, 1000f)));
            WhirlpoolMinOceanDepth = config.Bind(Whirl, "MinOceanDepth", 8f,
                new ConfigDescription("Minimum water depth for a spawn — keeps funnels off beaches and shallows.", new AcceptableValueRange<float>(0f, 50f)));
            WhirlpoolMaxActive = config.Bind(Whirl, "MaxActive", 3,
                new ConfigDescription("Cap on simultaneous storm-spawned whirlpools near the player.", new AcceptableValueRange<int>(0, 10)));
            WhirlpoolOuterRadius = config.Bind(Whirl, "OuterRadius", 60f,
                new ConfigDescription("Radius where the whirlpool's current begins.", new AcceptableValueRange<float>(10f, 300f)));
            WhirlpoolCoreRadius = config.Bind(Whirl, "CoreRadius", 8f,
                new ConfigDescription("Radius of the sink core — inside this, ships get dragged down.", new AcceptableValueRange<float>(1f, 60f)));
            WhirlpoolTangentialSpeed = config.Bind(Whirl, "TangentialSpeed", 5f,
                new ConfigDescription("Peak rotational current speed (m/s) — the swirl ships get caught in.", new AcceptableValueRange<float>(0f, 30f)));
            WhirlpoolRadialSpeed = config.Bind(Whirl, "RadialSpeed", 1.5f,
                new ConfigDescription("Inward pull current speed (m/s) — never zero inside the ring, strongest near the core.", new AcceptableValueRange<float>(0f, 20f)));
            WhirlpoolShipSinkForce = config.Bind(Whirl, "ShipSinkForce", 25f,
                new ConfigDescription("Downward force on ships inside the core (quadratic ramp — the trap).", new AcceptableValueRange<float>(0f, 200f)));
            WhirlpoolShipDamage = config.Bind(Whirl, "ShipDamagePerSecond", 6f,
                new ConfigDescription("Hull damage per second to a ship in the core (0 = none).", new AcceptableValueRange<float>(0f, 100f)));
            WhirlpoolFunnelDepth = config.Bind(Whirl, "FunnelDepth", 3f,
                new ConfigDescription("How deep the water surface bowls at dead centre (affects buoyancy AND the visual).", new AcceptableValueRange<float>(0f, 20f)));
            WhirlpoolSpiralArms = config.Bind(Whirl, "SpiralArms", 3f,
                new ConfigDescription("Number of spiral ridges on the funnel wall.", new AcceptableValueRange<float>(0f, 12f)));
            WhirlpoolSpiralSpeed = config.Bind(Whirl, "SpiralSpeed", 1.5f,
                new ConfigDescription("Rotation speed of the spiral pattern (radians/sec).", new AcceptableValueRange<float>(0f, 10f)));
            WhirlpoolRidgeHeight = config.Bind(Whirl, "RidgeHeight", 0.3f,
                new ConfigDescription("Amplitude of the spiral ridges (fades to zero at the core and the rim).", new AcceptableValueRange<float>(0f, 3f)));

            EnableShipCurrents = config.Bind(Ships, "EnableShipCurrents", true,
                "Apply current forces to ships.");
            ShipWaterHeightTolerance = config.Bind(Ships, "ShipWaterHeightTolerance", 2.0f,
                new ConfigDescription("Ship must be within this height of the water surface to receive current force.", new AcceptableValueRange<float>(0f, 10f)));
            MaxCurrentSpeedContribution = config.Bind(Ships, "MaxCurrentSpeedContribution", 2f,
                new ConfigDescription("Soft limit. Current force tapers as ship speed along the current approaches this.", new AcceptableValueRange<float>(0f, 20f)));
            CurrentDirectionSmoothing = config.Bind(Ships, "CurrentDirectionSmoothing", 2.5f,
                new ConfigDescription("How quickly a ship's current direction is smoothed toward new readings.", new AcceptableValueRange<float>(0.1f, 20f)));
            EnablePlayerCurrents = config.Bind(Ships, "EnablePlayerCurrents", true,
                "Currents push you while you swim or wade.");
            EnableCreatureCurrents = config.Bind(Ships, "EnableCreatureCurrents", true,
                "Currents push creatures in the water.");
            CharacterCurrentScale = config.Bind(Ships, "CharacterCurrentScale",
                1.0f,
                new ConfigDescription(
                    "How strongly currents push players and creatures.",
                    new AcceptableValueRange<float>(0f, 8f)));
            AdminBypassCurrents = config.Bind(Ships, "AdminBypassCurrents", false,
                "Currents do not push admins or their ships.");
            Sync(sync, EnableShipCurrents);
            Sync(sync, ShipWaterHeightTolerance);
            Sync(sync, MaxCurrentSpeedContribution);
            Sync(sync, CurrentDirectionSmoothing);
            Sync(sync, EnablePlayerCurrents);
            Sync(sync, EnableCreatureCurrents);
            Sync(sync, CharacterCurrentScale);
            Sync(sync, AdminBypassCurrents);

            WindBehindThreshold = config.Bind(Wind, "WindBehindThreshold", 0.20f,
                new ConfigDescription("Wind alignment above this means wind is helping the ship.", new AcceptableValueRange<float>(-1f, 1f)));
            WindAgainstThreshold = config.Bind(Wind, "WindAgainstThreshold", -0.25f,
                new ConfigDescription("Wind alignment below this means wind is fighting the ship.", new AcceptableValueRange<float>(-1f, 1f)));
            BadWindCurrentMultiplier = config.Bind(Wind, "BadWindCurrentMultiplier", 1.0f,
                new ConfigDescription("Current strength multiplier when wind is against the ship.", new AcceptableValueRange<float>(0f, 5f)));
            AgainstCurrentDrag = config.Bind(Wind, "AgainstCurrentDrag", 0.15f,
                new ConfigDescription("Extra drag when the ship is fighting both wind and current.", new AcceptableValueRange<float>(0f, 2f)));
            CrossCurrentDrag = config.Bind(Wind, "CrossCurrentDrag", 0.05f,
                new ConfigDescription("Mild drag when wind is bad and current is mostly sideways.", new AcceptableValueRange<float>(0f, 2f)));
            Sync(sync, WindBehindThreshold);
            Sync(sync, WindAgainstThreshold);
            Sync(sync, BadWindCurrentMultiplier);
            Sync(sync, AgainstCurrentDrag);
            Sync(sync, CrossCurrentDrag);

            EnableRiverCurrents = config.Bind(Rivers, "EnableRiverCurrents", true,
                "Detect rivers from terrain slope and apply downhill current.");
            RiverCurrentMinStrength = config.Bind(Rivers, "RiverCurrentMinStrength", 0.25f,
                new ConfigDescription("Minimum river current force.", new AcceptableValueRange<float>(0f, 5f)));
            RiverCurrentMaxStrength = config.Bind(Rivers, "RiverCurrentMaxStrength", 0.6f,
                new ConfigDescription("Maximum river current force.", new AcceptableValueRange<float>(0f, 5f)));
            RiverCacheCellSize = config.Bind(Rivers, "RiverCacheCellSize", 32f,
                new ConfigDescription("Cache grid cell size for river decisions.", new AcceptableValueRange<float>(4f, 256f)));
            RiverCacheLifetimeSeconds = config.Bind(Rivers, "RiverCacheLifetimeSeconds", 600f,
                new ConfigDescription("Seconds before unused river cache cells are pruned.", new AcceptableValueRange<float>(10f, 7200f)));
            RiverBilinearBlend = config.Bind(Rivers, "RiverBilinearBlend", true,
                "Blend the 4 nearest cells so flow does not step at cell boundaries.");
            RiverScanRadius = config.Bind(RiverScan, "RiverScanRadius", 48f,
                new ConfigDescription("Radius used to sample terrain slope. Larger reads channel tilt instead of bank-to-bank slope.", new AcceptableValueRange<float>(4f, 256f)));
            RiverMinDepth = config.Bind(RiverScan, "RiverMinDepth", 0.25f,
                new ConfigDescription("Minimum water depth at the cell center for river detection.", new AcceptableValueRange<float>(0f, 10f)));
            RiverMaxDepth = config.Bind(RiverScan, "RiverMaxDepth", 5f,
                new ConfigDescription("Maximum water depth at the cell center for river detection. Deeper falls back to ocean.", new AcceptableValueRange<float>(0.5f, 50f)));
            RiverMinSlopeMagnitude = config.Bind(RiverScan, "RiverMinSlopeMagnitude", 0.02f,
                new ConfigDescription("Minimum slope magnitude required to count as a river.", new AcceptableValueRange<float>(0f, 1f)));
            RiverBankConfirmDepth = config.Bind(RiverScan, "RiverBankConfirmDepth", 0.1f,
                new ConfigDescription("Bank-confirmation probe depth. Rejects sloping beaches that look like rivers.", new AcceptableValueRange<float>(0f, 5f)));
            Sync(sync, EnableRiverCurrents);
            Sync(sync, RiverCurrentMinStrength);
            Sync(sync, RiverCurrentMaxStrength);
            Sync(sync, RiverCacheCellSize);
            Sync(sync, RiverCacheLifetimeSeconds);
            Sync(sync, RiverBilinearBlend);
            Sync(sync, RiverScanRadius);
            Sync(sync, RiverMinDepth);
            Sync(sync, RiverMaxDepth);
            Sync(sync, RiverMinSlopeMagnitude);
            Sync(sync, RiverBankConfirmDepth);

            EnableOceanCurrents = config.Bind(Ocean, "EnableOceanCurrents", true,
                "Apply the baseline open-ocean noise current when no river or tide current is active.");
            OceanCurrentStrength = config.Bind(Ocean, "OceanCurrentStrength", 0.2f,
                new ConfigDescription("Open-ocean current force.", new AcceptableValueRange<float>(0f, 5f)));
            OceanNoiseScale = config.Bind(Ocean, "OceanNoiseScale", 1500f,
                new ConfigDescription("World-space wavelength of the ocean current noise field. Larger = broader gyres.", new AcceptableValueRange<float>(50f, 20000f)));
            OceanNoiseSeed = config.Bind(Ocean, "OceanNoiseSeed", 1337f,
                new ConfigDescription("Offset into the noise field. Change to reshape ocean currents.", new AcceptableValueRange<float>(0f, 100000f)));
            Sync(sync, EnableOceanCurrents);
            Sync(sync, OceanCurrentStrength);
            Sync(sync, OceanNoiseScale);
            Sync(sync, OceanNoiseSeed);

            EnableTides = config.Bind(Tides, "EnableTides", true,
                "Master toggle for the tide system.");
            EnableTideHeightApply = config.Bind(Tides, "EnableTideHeightApply", true,
                // 0.1.56 (Fire 10-20 player-facing config rule): test-only switch, hidden.
                new ConfigDescription("Move the water surface with the tide (test switch).", null,
                    new System.ComponentModel.BrowsableAttribute(false)));
            NormalHighTideY = config.Bind(TideRange, "NormalHighTideY", 30f,
                new ConfigDescription("Standard high tide water y. Matches vanilla so shoreline meshes line up.", new AcceptableValueRange<float>(20f, 40f)));
            NormalLowTideY = config.Bind(TideRange, "NormalLowTideY", 29f,
                new ConfigDescription("Standard low tide water y on a typical day.", new AcceptableValueRange<float>(20f, 40f)));
            SpringHighTideY = config.Bind(TideRange, "SpringHighTideY", 30f,
                new ConfigDescription("High tide y at full or new moon (spring tide).", new AcceptableValueRange<float>(20f, 40f)));
            SpringLowTideY = config.Bind(TideRange, "SpringLowTideY", 28f,
                new ConfigDescription("Low tide y at full or new moon. Exposes more shoreline.", new AcceptableValueRange<float>(20f, 40f)));
            BloodMoonHighTideY = config.Bind(TideRange, "BloodMoonHighTideY", 31f,
                new ConfigDescription("High tide y during a blood moon. Mildly exceeds the vanilla water plane.", new AcceptableValueRange<float>(20f, 40f)));
            BloodMoonLowTideY = config.Bind(TideRange, "BloodMoonLowTideY", 27f,
                new ConfigDescription("Low tide y during a blood moon. Maximum shoreline exposure.", new AcceptableValueRange<float>(20f, 40f)));
            BloodMoonEnabled = config.Bind(TideRange, "BloodMoonEnabled", true,
                "Now and then a full moon turns red and brings stronger tides.");
            BloodMoonOneIn = config.Bind(TideRange, "BloodMoonOneIn", 10,
                new ConfigDescription("About one full moon in this many is a blood moon.", new AcceptableValueRange<int>(2, 100)));
            TideCyclesPerOrbit = config.Bind(TideCycle, "TideCyclesPerOrbit",
                1f,
                new ConfigDescription(
                    "High/low tide cycles per game day (1 = one of each, 2 = Earth-like).",
                    new AcceptableValueRange<float>(0.5f, 4f)));
            Sync(sync, EnableTides);
            Sync(sync, EnableTideHeightApply);
            Sync(sync, NormalHighTideY);
            Sync(sync, NormalLowTideY);
            Sync(sync, SpringHighTideY);
            Sync(sync, SpringLowTideY);
            Sync(sync, BloodMoonHighTideY);
            Sync(sync, BloodMoonLowTideY);
            Sync(sync, BloodMoonEnabled); Sync(sync, BloodMoonOneIn);   // 0.1.59: the server decides which full moons are blood moons
            Sync(sync, TideCyclesPerOrbit);

            EnableTidalCurrents = config.Bind(Tidal, "EnableTidalCurrents", true,
                "Enable tide-driven ocean currents.");
            OutgoingCurrentStrength = config.Bind(Tidal, "OutgoingCurrentStrength", 0.3f,
                new ConfigDescription("Force when the tide is going out (water dropping).", new AcceptableValueRange<float>(0f, 5f)));
            IncomingCurrentStrength = config.Bind(Tidal, "IncomingCurrentStrength", 0.3f,
                new ConfigDescription("Force when the tide is coming in (water rising).", new AcceptableValueRange<float>(0f, 5f)));
            RiverMouthSwirlRadius = config.Bind(Tidal, "RiverMouthSwirlRadius", 80f,
                new ConfigDescription("Radius around each river mouth where tidal swirl applies.", new AcceptableValueRange<float>(0f, 500f)));
            RiverMouthSwirlStrength = config.Bind(Tidal, "RiverMouthSwirlStrength", 0.4f,
                new ConfigDescription("Strength multiplier on the rotational swirl at river mouths.", new AcceptableValueRange<float>(0f, 5f)));
            Sync(sync, EnableTidalCurrents);
            Sync(sync, OutgoingCurrentStrength);
            Sync(sync, IncomingCurrentStrength);
            Sync(sync, RiverMouthSwirlRadius);
            Sync(sync, RiverMouthSwirlStrength);

            RiverMouthScanCellSize = config.Bind(Mouths, "RiverMouthScanCellSize", 64f,
                new ConfigDescription("Grid cell size for river-mouth detection.", new AcceptableValueRange<float>(8f, 256f)));
            RiverMouthMaxBuildSeconds = config.Bind(Mouths, "RiverMouthMaxBuildSeconds", 8f,
                new ConfigDescription("Maximum seconds the river-mouth scan may take on world load.", new AcceptableValueRange<float>(1f, 60f)));
            Sync(sync, RiverMouthScanCellSize);
            Sync(sync, RiverMouthMaxBuildSeconds);

            const string Debug = "Galaxies - Debug Visualization";
            DebugVisualizeCurrents = config.Bind(Debug, "VisualizeCurrents", false, new ConfigDescription("Draw coloured lines showing the currents around you (white weak, red strong).", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
            DebugCurrentsGridSpacing = config.Bind(Debug, "CurrentsGridSpacing",
                16f,
                new ConfigDescription(
                    "Spacing between streamline seed points in meters.",
                    new AcceptableValueRange<float>(4f, 64f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            DebugCurrentsGridRadius = config.Bind(Debug, "CurrentsGridRadius",
                80f,
                new ConfigDescription(
                    "Radius around the player to sample the current field.",
                    new AcceptableValueRange<float>(16f, 256f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            DebugCurrentsArrowScale = config.Bind(Debug, "CurrentsArrowScale",
                4f,
                new ConfigDescription(
                    "Length of each current line step.",
                    new AcceptableValueRange<float>(0.5f, 20f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            DebugCurrentsStreamlineSteps = config.Bind(Debug, "CurrentsStreamlineSteps",
                12,
                new ConfigDescription(
                    "Segments per current line (1 = a single arrow).",
                    new AcceptableValueRange<int>(1, 32), new ConfigurationManagerAttributes { IsAdvanced = true }));
            DebugVisualizeOrbits = config.Bind(Debug, "VisualizeOrbits", false, new ConfigDescription("Draw the path of every orbiting body.", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
            DebugOrbitsResolution = config.Bind(Debug, "OrbitsResolution",
                64,
                new ConfigDescription(
                    "How many segments make up each traced orbit ring.",
                    new AcceptableValueRange<int>(16, 256), new ConfigurationManagerAttributes { IsAdvanced = true }));

            // 0.1.67 ([seasons] + [ghost], Fire 10-30: creature diving moved to FiresMonsterOverhaul): the 3 [Galaxies - Diving] keys are
            // no longer bound; FMO 0.0.47 owns them (it copied the values from this cfg, confirmed in the dedi log 10-30). Their saved lines
            // stay in the cfg file untouched. 0.1.68 ([lead], Fire: "off unless movement is installed"): Galaxies has no creature diving at all; FMO is its only home.

            MoonEnabled = config.Bind(Moon, "Enabled", true,
                "Show the 3D moon with real phases; it drives the tides.");
            MoonDistance = config.Bind(Moon, "Distance", 4500f,
                new ConfigDescription("Sky distance of the moon (m).", new AcceptableValueRange<float>(500f, 12000f)));
            MoonSizeScale = config.Bind(Moon, "SizeScale", 1.0f,
                new ConfigDescription("Visual size multiplier.", new AcceptableValueRange<float>(0.2f, 5f)));
            MoonSynodicDays = config.Bind(Moon, "SynodicDays", 30f,
                new ConfigDescription("Game days per moon cycle (phases and tides); Vedr can override it.",
                    new AcceptableValueRange<float>(1f, 120f)));
            // 0.1.33 (Fire 10-06: "realistic is subjective here and when vedr is installed its cycles should roughly match the
            // seasons based of vedr configs, so a ten day summer would see things differently from a 30 day seasonal setting").
            MoonFollowVedr = config.Bind(Moon, "FollowVedrSeasons", true,
                "With Vedr installed, the moon cycle follows Vedr's season length.");
            MoonCyclesPerSeason = config.Bind(Moon, "CyclesPerSeason", 1f,
                new ConfigDescription("Moon cycles per Vedr season when following Vedr.",
                    new AcceptableValueRange<float>(0.25f, 6f)));
            MoonOrbitInclination = config.Bind(Moon, "OrbitInclinationDeg", 8f,
                new ConfigDescription("Tilt of the moon's orbital plane vs the ecliptic.", new AcceptableValueRange<float>(0f, 45f)));
            MoonPhaseOffset = config.Bind(Moon, "PhaseOffsetDays", 0f,
                new ConfigDescription("Shift the starting phase (days). Drives both moon + tides.", new AcceptableValueRange<float>(0f, 120f)));
            MoonSunBrightness = config.Bind(Moon, "SunBrightness", 1.4f,
                new ConfigDescription("Lit-side brightness.", new AcceptableValueRange<float>(0f, 4f)));
            MoonDarkSide = config.Bind(Moon, "DarkSide", 0.04f,
                new ConfigDescription("Earthshine on the dark side.", new AcceptableValueRange<float>(0f, 0.5f)));
            MoonEmission = config.Bind(Moon, "Emission", 0.2f,
                new ConfigDescription("Self-glow on the lit side.", new AcceptableValueRange<float>(0f, 2f)));
            MoonTerminatorSoft = config.Bind(Moon, "TerminatorSoftness", 0.18f,
                new ConfigDescription("Softness of the lit/dark boundary.", new AcceptableValueRange<float>(0f, 1f)));
            MoonSaturation = config.Bind(Moon, "Saturation", 0.7f,
                new ConfigDescription("0 = greyscale, 1 = full colour.", new AcceptableValueRange<float>(0f, 1f)));
            Sync(sync, MoonEnabled); Sync(sync, MoonDistance); Sync(sync, MoonSizeScale); Sync(sync, MoonSynodicDays);
            Sync(sync, MoonOrbitInclination); Sync(sync, MoonPhaseOffset); Sync(sync, MoonSunBrightness); Sync(sync, MoonDarkSide);
            Sync(sync, MoonEmission); Sync(sync, MoonTerminatorSoft); Sync(sync, MoonSaturation);
            Sync(sync, MoonFollowVedr); Sync(sync, MoonCyclesPerSeason);
            MoonFollowVedr.SettingChanged      += (_, __) => ApplyMoon();
            MoonCyclesPerSeason.SettingChanged += (_, __) => ApplyMoon();

            ApplyMoon();
            MoonEnabled.SettingChanged          += (_, __) => ApplyMoon();
            MoonDistance.SettingChanged         += (_, __) => ApplyMoon();
            MoonSizeScale.SettingChanged        += (_, __) => ApplyMoon();
            MoonSynodicDays.SettingChanged      += (_, __) => ApplyMoon();
            MoonOrbitInclination.SettingChanged += (_, __) => ApplyMoon();
            MoonPhaseOffset.SettingChanged      += (_, __) => ApplyMoon();
            MoonSunBrightness.SettingChanged    += (_, __) => ApplyMoon();
            MoonDarkSide.SettingChanged         += (_, __) => ApplyMoon();
            MoonEmission.SettingChanged         += (_, __) => ApplyMoon();
            MoonTerminatorSoft.SettingChanged   += (_, __) => ApplyMoon();
            MoonSaturation.SettingChanged       += (_, __) => ApplyMoon();

            BindOrbitals(config, sync);
            // 0.1.35 (Fire: "we feel them, but navigation is still possible"): tides and currents were so strong a full-sail
            // Karve could not fight them. New defaults; a config still on an OLD default moves to the new one (logged), a
            // hand-set value stays.
            MigrateDefault(MaxCurrentSpeedContribution, 8f, 2f);
            MigrateDefault(CharacterCurrentScale, 2f, 1f);
            MigrateDefault(BadWindCurrentMultiplier, 1.35f, 1f);
            MigrateDefault(AgainstCurrentDrag, 0.85f, 0.15f);
            MigrateDefault(CrossCurrentDrag, 0.25f, 0.05f);
            MigrateDefault(RiverCurrentMinStrength, 0.75f, 0.25f);
            MigrateDefault(RiverCurrentMaxStrength, 2.25f, 0.6f);
            MigrateDefault(OceanCurrentStrength, 0.65f, 0.2f);
            MigrateDefault(NormalLowTideY, 28f, 29f);
            MigrateDefault(SpringLowTideY, 25f, 28f);
            MigrateDefault(BloodMoonHighTideY, 32f, 31f);
            MigrateDefault(BloodMoonLowTideY, 24f, 27f);
            MigrateDefault(OutgoingCurrentStrength, 0.8f, 0.3f);
            MigrateDefault(IncomingCurrentStrength, 0.8f, 0.3f);
            MigrateDefault(RiverMouthSwirlStrength, 1.2f, 0.4f);
            // 0.1.57 (Discord how-to audit): the 0.1.38 one-off "old default true -> false" migration of AdminBypassCurrents is gone. Its
            // guard was a static flag, so it ran again on EVERY game start and switched off any admin who had turned the bypass on.
        }

        // 0.1.41 (config audit 10-13): sections merged (Tides*, Rivers*) and the unprefixed General / Terrain / Water / FiresWater
        // moved under "Galaxies - ". Each entry still found under its old section (a BepInEx orphan) is copied into its new
        // place once and dropped, so nobody's value resets; the three dead Diving keys are dropped too. Called after every Bind.
        internal static void MigrateMovedKeys(ConfigFile config)
        {
            var log = BepInEx.Logging.Logger.CreateLogSource("Galaxies.Config");
            var orphans = HarmonyLib.AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config, null) as System.Collections.Generic.Dictionary<ConfigDefinition, string>;
            if (orphans == null || orphans.Count == 0) return;
            var moves = new System.Collections.Generic.Dictionary<string, string[]>
            {
                { "Galaxies - Rivers", new[] { "Galaxies - Rivers (Detection)", "Galaxies - River Mouth Detection", "Galaxies - Rivers & Tide Shaping" } },
                { "Galaxies - Tides", new[] { "Galaxies - Tides (Range)", "Galaxies - Tides (Cycle)", "Galaxies - Tidal Currents" } },
                { "Galaxies - General", new[] { "General", "Terrain" } },
                { "Galaxies - Water", new[] { "Water", "FiresWater" } },
            };
            int moved = 0, dropped = 0;
            foreach (var def in System.Linq.Enumerable.ToList(config.Keys))
            {
                if (!moves.TryGetValue(def.Section, out var oldSections)) continue;
                foreach (string oldSection in oldSections)
                {
                    var old = new ConfigDefinition(oldSection, def.Key);
                    if (!orphans.TryGetValue(old, out string raw)) continue;
                    orphans.Remove(old);
                    config[def].SetSerializedValue(raw);
                    moved++;
                    log.LogDebug($"[Galaxies] config: [{oldSection}] {def.Key} -> [{def.Section}] = '{config[def].GetSerializedValue()}'");
                }
            }
            foreach (string dead in new[] { "DivingEnabled", "MaxSwimSpeed", "UnderwaterStaminaDrain" })
                if (orphans.Remove(new ConfigDefinition(Diving, dead))) { dropped++; log.LogDebug($"[Galaxies] config: dropped dead [{Diving}] {dead} (creature diving reads CreatureDiving*)"); }
            if (moved + dropped > 0) { config.Save(); log.LogDebug($"[Galaxies] config: {moved} moved key(s) migrated, {dropped} dead key(s) dropped"); }
        }

        private static void MigrateDefault(ConfigEntry<float> entry, float oldDefault, float newDefault)
        {
            if (entry == null || UnityEngine.Mathf.Abs(entry.Value - oldDefault) > 0.0001f) return;
            entry.Value = newDefault;
            UnityEngine.Debug.Log($"[Galaxies] {entry.Definition.Section}/{entry.Definition.Key} moved from the old default {oldDefault} to {newDefault}");
        }


        // Push moon config into the moon driver, and keep the tide phase math
        // (FUC MoonPhase) in lockstep so the visible moon and the tides it drives
        // stay synced on one cycle.
        private static void ApplyMoon()
        {
            VerdantsAscent.Water.Core.FiresMoon.Enabled             = MoonEnabled.Value;
            VerdantsAscent.Water.Core.FiresMoon.Distance            = MoonDistance.Value;
            VerdantsAscent.Water.Core.FiresMoon.SizeScale           = MoonSizeScale.Value;
            float synodic = EffectiveSynodicDays(out string source);
            VerdantsAscent.Water.Core.FiresMoon.SynodicDays         = synodic;
            VerdantsAscent.Water.Core.FiresMoon.OrbitInclinationDeg = MoonOrbitInclination.Value;
            VerdantsAscent.Water.Core.FiresMoon.PhaseOffsetDays     = MoonPhaseOffset.Value;
            VerdantsAscent.Water.Core.FiresMoon.SunBrightness       = MoonSunBrightness.Value;
            VerdantsAscent.Water.Core.FiresMoon.DarkSide            = MoonDarkSide.Value;
            VerdantsAscent.Water.Core.FiresMoon.Emission            = MoonEmission.Value;
            VerdantsAscent.Water.Core.FiresMoon.TerminatorSoft      = MoonTerminatorSoft.Value;
            VerdantsAscent.Water.Core.FiresMoon.Saturation          = MoonSaturation.Value;
            FiresCore.Services.MoonPhase.SynodicDays     = synodic;
            FiresCore.Services.MoonPhase.PhaseOffsetDays = MoonPhaseOffset.Value;
            if (!UnityEngine.Mathf.Approximately(synodic, s_loggedSynodic))
            {
                s_loggedSynodic = synodic;
                FiresLogger.LogDebug($"[Galaxies] lunar cycle {synodic:0.##} game days ({source})");
            }
        }

        private static float s_loggedSynodic = -1f;
        private static System.Func<int> s_vedrDaysPerSeason;
        private static int s_vedrDaysSeen = -1;
        private static float s_nextVedrPoll;

        // The cycle length: Vedr's season over CyclesPerSeason when Vedr runs and FollowVedrSeasons is on, else SynodicDays.
        private static float EffectiveSynodicDays(out string source)
        {
            int days = MoonFollowVedr != null && MoonFollowVedr.Value ? VedrDaysPerSeason() : 0;
            if (days <= 0)
            {
                source = MoonFollowVedr != null && MoonFollowVedr.Value ? "SynodicDays; Vedr not running" : "SynodicDays";
                return MoonSynodicDays.Value;
            }
            float cycles = UnityEngine.Mathf.Max(0.25f, MoonCyclesPerSeason.Value);
            source = $"Vedr: {days} days per season / {cycles:0.##} cycles per season";
            return UnityEngine.Mathf.Max(1f, days / cycles);
        }

        // Vedr's public API by reflection (no compile reference): its synced 'Days per season', 0 until its season clock is
        // ready or when Vedr isn't loaded. Looked up again until found, since Vedr can load after Galaxies binds.
        private static int VedrDaysPerSeason()
        {
            if (s_vedrDaysPerSeason == null)
            {
                try
                {
                    var getter = System.Type.GetType("Vedr.VedrApi, Norger.Vedr")?.GetProperty("DaysPerSeason")?.GetGetMethod();
                    if (getter != null) s_vedrDaysPerSeason = (System.Func<int>)System.Delegate.CreateDelegate(typeof(System.Func<int>), getter);
                }
                catch { s_vedrDaysPerSeason = null; }
                if (s_vedrDaysPerSeason == null) return 0;
            }
            try { return s_vedrDaysPerSeason(); } catch { return 0; }
        }

        // Every 2 s from GalaxiesModule.Update: Vedr's season length can arrive later (its clock, the server's synced cfg) or
        // change live; a change re-applies the moon so its phase, orbit and the tides follow.
        internal static void PollVedrSeasons()
        {
            if (MoonFollowVedr == null || !MoonFollowVedr.Value) return;
            if (UnityEngine.Time.unscaledTime < s_nextVedrPoll) return;
            s_nextVedrPoll = UnityEngine.Time.unscaledTime + 2f;
            int days = VedrDaysPerSeason();
            if (days == s_vedrDaysSeen) return;
            s_vedrDaysSeen = days;
            ApplyMoon();
        }

        // One config section per orbital slot (4 slots x 15 live sliders), bound
        // in a loop so the body that orbits, its path shape, and its timing are all
        // tunable. Applied to FiresOrbitals.Configure on bind + any change.
        private static void BindOrbitals(ConfigFile config, ConfigSync sync)
        {
            for (int i = 0; i < OrbitalSlots; i++)
            {
                string sec = OrbitalSections[i];
                OrbitalEnabled[i]    = config.Bind(sec, "01 Enabled", i == 0,
                    "Show this orbiting body (slot A is the moon).");
                OrbitalPrefab[i]     = config.Bind(sec, "02 PrefabName", i == 0 ? "FiresMoon" : "",
                    "Prefab that orbits (FiresMoon = the moon; empty = a plain sphere).");
                OrbitalWidth[i]      = config.Bind(sec, "03 OrbitWidthMeters", 11000f, new ConfigDescription("Full horizontal width of the orbit (m).", new AcceptableValueRange<float>(2000f, 24000f)));
                OrbitalApex[i]       = config.Bind(sec, "04 ApexHeight", 3000f,  new ConfigDescription("Highest point of the orbit (m).", new AcceptableValueRange<float>(200f, 8000f)));
                OrbitalNadir[i]      = config.Bind(sec, "05 NadirHeight", -400f,  new ConfigDescription("Lowest point (m). Negative passes under the world on the far side.", new AcceptableValueRange<float>(-2000f, 2000f)));
                OrbitalPeriod[i]     = config.Bind(sec, "06 PeriodSeconds", 300f, new ConfigDescription("Seconds for one full lap.", new AcceptableValueRange<float>(20f, 3600f)));
                OrbitalEggRatio[i]   = config.Bind(sec, "07 EggRatio", 0.8f,      new ConfigDescription("Orbit shape: 1 = round, lower = more egg.", new AcceptableValueRange<float>(0.3f, 1f)));
                OrbitalWobbleAmp[i]  = config.Bind(sec, "08 WobbleAmplitude", 0f, new ConfigDescription("Nutation: vertical nod height (m). 0 = none.", new AcceptableValueRange<float>(0f, 1500f)));
                OrbitalWobbleSec[i]  = config.Bind(sec, "09 WobbleSeconds", 20f,  new ConfigDescription("Nutation period (s).", new AcceptableValueRange<float>(2f, 600f)));
                OrbitalPrecession[i] = config.Bind(sec, "10 PrecessionMinutes", 0f, new ConfigDescription("Minutes for the orbit's high side to sweep a full circle around Y. 0 = fixed.", new AcceptableValueRange<float>(0f, 240f)));
                OrbitalPhase[i]      = config.Bind(sec, "11 PhaseOffsetDeg", i * 90f, new ConfigDescription("Starting angle (deg) so bodies don't bunch up.", new AcceptableValueRange<float>(0f, 360f)));
                OrbitalSpin[i]       = config.Bind(sec, "12 SpinDegPerSec", 10f,  new ConfigDescription("Body's own-axis spin (deg/s). Not used while '19 TidalLock' is on; 0 = no spin.", new AcceptableValueRange<float>(-180f, 180f)));
                OrbitalScale[i]      = config.Bind(sec, "13 Scale", 50f,          new ConfigDescription("Model scale multiplier (placeholder sphere is 1m).", new AcceptableValueRange<float>(1f, 2000f)));
                OrbitalTilt[i]       = config.Bind(sec, "14 TiltDeg", 0f,         new ConfigDescription("Orbital-plane inclination (deg).", new AcceptableValueRange<float>(-90f, 90f)));
                OrbitalYaw[i]        = config.Bind(sec, "15 YawDeg", 0f,          new ConfigDescription("Compass heading of the orbit's long axis (deg).", new AcceptableValueRange<float>(0f, 360f)));
                // 0.1.32 (Fire 10-06): the moon slot's motion is a choice again; before, the moon prefab ignored every orbit key.
                OrbitalMotion[i]     = config.Bind(sec, "16 Motion", VerdantsAscent.Water.Core.OrbitMotion.Auto,
                    "How it moves: Realistic moon, Custom orbit, Locked in place, AntiSun, or Auto.");
                OrbitalLockAz[i]     = config.Bind(sec, "17 LockAzimuthDeg", 180f, new ConfigDescription("Locked: compass heading of the body (deg; 0 = north, 90 = east, 180 = south).", new AcceptableValueRange<float>(0f, 360f)));
                OrbitalLockEl[i]     = config.Bind(sec, "18 LockElevationDeg", 35f, new ConfigDescription("Locked: height above the horizon (deg; 90 = straight up, negative = below).", new AcceptableValueRange<float>(-90f, 90f)));
                OrbitalTidalLock[i]  = config.Bind(sec, "19 TidalLock", i == 0,
                    "Keep the same face toward the world, like a real moon.");
                Sync(sync, OrbitalMotion[i]); Sync(sync, OrbitalLockAz[i]); Sync(sync, OrbitalLockEl[i]); Sync(sync, OrbitalTidalLock[i]);

                Sync(sync, OrbitalEnabled[i]); Sync(sync, OrbitalPrefab[i]); Sync(sync, OrbitalWidth[i]); Sync(sync, OrbitalApex[i]); Sync(sync, OrbitalNadir[i]);
                Sync(sync, OrbitalPeriod[i]); Sync(sync, OrbitalEggRatio[i]); Sync(sync, OrbitalWobbleAmp[i]); Sync(sync, OrbitalWobbleSec[i]); Sync(sync, OrbitalPrecession[i]);
                Sync(sync, OrbitalPhase[i]); Sync(sync, OrbitalSpin[i]); Sync(sync, OrbitalScale[i]); Sync(sync, OrbitalTilt[i]); Sync(sync, OrbitalYaw[i]);

                System.EventHandler apply = (_, __) => ApplyOrbitals();
                OrbitalEnabled[i].SettingChanged += apply; OrbitalPrefab[i].SettingChanged += apply; OrbitalWidth[i].SettingChanged += apply; OrbitalApex[i].SettingChanged += apply; OrbitalNadir[i].SettingChanged += apply;
                OrbitalPeriod[i].SettingChanged += apply; OrbitalEggRatio[i].SettingChanged += apply; OrbitalWobbleAmp[i].SettingChanged += apply; OrbitalWobbleSec[i].SettingChanged += apply; OrbitalPrecession[i].SettingChanged += apply;
                OrbitalPhase[i].SettingChanged += apply; OrbitalSpin[i].SettingChanged += apply; OrbitalScale[i].SettingChanged += apply; OrbitalTilt[i].SettingChanged += apply; OrbitalYaw[i].SettingChanged += apply;
                OrbitalMotion[i].SettingChanged += apply; OrbitalLockAz[i].SettingChanged += apply; OrbitalLockEl[i].SettingChanged += apply; OrbitalTidalLock[i].SettingChanged += apply;
            }
            ApplyOrbitals();
        }

        private static void ApplyOrbitals()
        {
            var orbitals = new VerdantsAscent.Water.Core.OrbitParams[OrbitalSlots];
            for (int i = 0; i < OrbitalSlots; i++)
            {
                orbitals[i] = new VerdantsAscent.Water.Core.OrbitParams
                {
                    Enabled           = OrbitalEnabled[i].Value,
                    PrefabName        = OrbitalPrefab[i].Value,
                    // A slot wired to the moon prefab rides the vanilla moon's sky direction
                    // (see FiresOrbitals.ComputePosition) so it lines up with the moon light/shafts.
                    FollowVanillaMoon = OrbitalPrefab[i].Value == VerdantsAscent.Water.Core.FiresMoon.PrefabName,
                    OrbitWidth        = OrbitalWidth[i].Value,
                    ApexHeight        = OrbitalApex[i].Value,
                    NadirHeight       = OrbitalNadir[i].Value,
                    PeriodSeconds     = OrbitalPeriod[i].Value,
                    EggRatio          = OrbitalEggRatio[i].Value,
                    WobbleAmplitude   = OrbitalWobbleAmp[i].Value,
                    WobbleSeconds     = OrbitalWobbleSec[i].Value,
                    PrecessionMinutes = OrbitalPrecession[i].Value,
                    PhaseOffsetDeg    = OrbitalPhase[i].Value,
                    SpinDegPerSec     = OrbitalSpin[i].Value,
                    Scale             = OrbitalScale[i].Value,
                    TiltDeg           = OrbitalTilt[i].Value,
                    YawDeg            = OrbitalYaw[i].Value,
                    Motion            = OrbitalMotion[i].Value,
                    LockAzimuthDeg    = OrbitalLockAz[i].Value,
                    LockElevationDeg  = OrbitalLockEl[i].Value,
                    TidalLock         = OrbitalTidalLock[i].Value,
                };
            }
            VerdantsAscent.Water.Core.FiresOrbitals.Configure(orbitals);

            // If an enabled slot is the reachable moon, hide the skybox-disc moon so there's
            // exactly one moon (the vanilla flat moon stays suppressed by FiresMoonDriver).
            bool moonOrbital = false;
            for (int i = 0; i < OrbitalSlots; i++)
                if (OrbitalEnabled[i].Value
                    && OrbitalPrefab[i].Value == VerdantsAscent.Water.Core.FiresMoon.PrefabName)
                { moonOrbital = true; break; }
            VerdantsAscent.Water.Core.FiresMoon.SuppressDisc = moonOrbital;
        }

        // 0.1.56 (Fire 10-20 player-facing config rule): without Show Advanced only keybinds and each feature's master switch show.
        private static readonly System.Collections.Generic.HashSet<string> s_basicKeys = new System.Collections.Generic.HashSet<string>
        {
            "Galaxies - General/EnableMod", "Galaxies - General/OceanOwner", "Galaxies - General/ServerAuthority",
            "Galaxies - Tides/EnableTides", "Galaxies - Tides/BloodMoonEnabled", "Galaxies - Moon/Enabled",   // 0.1.57: Water/Enabled is unread (the water look moved to TossinShade): hidden, not basic
            "Galaxies - Whirlpools/WhirlpoolsEnabled",
            "Galaxies - Ships/EnableShipCurrents", "Galaxies - Rivers/EnableRiverCurrents",
            "Galaxies - Open Ocean/EnableOceanCurrents", "Galaxies - Rivers/ExtendedRiversEnabled",
        };

        internal static bool IsBasicKey(ConfigFile config, ConfigDefinition d)
        {
            if (d == null) return false;
            if (s_basicKeys.Contains(d.Section + "/" + d.Key)) return true;
            System.Type t;
            try { t = config[d].SettingType; } catch { return false; }
            return t == typeof(BepInEx.Configuration.KeyboardShortcut) || t == typeof(UnityEngine.KeyCode);
        }

        // Register a synced entry. Server-authority for ALL synced entries is
        // governed by the single master locking entry (configServerAuthority),
        // which the plugin registers once via AddLockingConfigEntry — ServerSync
        // permits only one locking entry per ConfigSync, so we never add our own.
        private static void Sync<T>(ConfigSync sync, ConfigEntry<T> entry)
        {
            if (sync == null) return;
            sync.AddConfigEntry(entry);
        }
    }
}
