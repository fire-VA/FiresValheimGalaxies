using BepInEx.Configuration;
using FiresCore.Sync;

namespace VerdantsAscent.Galaxies
{
    // All config for the moon-driven tide + current system. Self-contained so
    // it doesn't bloat the shared ConfigManager. Bind() is called once from the
    // plugin Awake with the BepInEx ConfigFile and the live ConfigSync; every
    // entry is registered for sync, and the plugin's single master lock
    // (configServerAuthority) makes them server-authoritative when enabled.
    internal static class GalaxiesConfig
    {
        private const string General   = "Galaxies - General";
        private const string Ships     = "Galaxies - Ships";
        private const string Wind      = "Galaxies - Wind";
        private const string Rivers    = "Galaxies - Rivers";
        private const string RiverScan = "Galaxies - Rivers (Detection)";
        private const string Ocean     = "Galaxies - Open Ocean";
        private const string Tides     = "Galaxies - Tides";
        private const string TideRange = "Galaxies - Tides (Range)";
        private const string TideCycle = "Galaxies - Tides (Cycle)";
        private const string Tidal     = "Galaxies - Tidal Currents";
        private const string Mouths    = "Galaxies - River Mouth Detection";
        private const string Diving    = "Galaxies - Diving";
        private const string Moon      = "Galaxies - Moon";
        private const string Whirl     = "Galaxies - Whirlpools";
        private const string TideShape = "Galaxies - Rivers & Tide Shaping";

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

        public static ConfigEntry<bool>  CreatureDivingEnabled;
        public static ConfigEntry<float> CreatureDiveMaxDepth;
        public static ConfigEntry<float> CreatureDiveSpeed;

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
                new ConfigDescription("Who drives the ocean. Galaxies = Galaxies' tides and currents are the baseline and Vedr's events (flood/drought rise, tsunamis, waterspouts) layer on top, clamped by MaxCombinedSeaOffset. Vedr = Galaxies' tides and currents stand down and only Vedr's flood/drought offset moves the sea.",
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
                "Shape the tide spatially instead of dropping the whole world sheet uniformly: the open ocean "
                + "gets the full tide, but rivers and swamps only get a fraction (RiverTideFraction) so they "
                + "don't drain to nothing and stay navigable at low tide. Off = every water body drops the full "
                + "tide (the old behaviour). Client-local, applies live.");
            RiverTideFraction = config.Bind(TideShape, "RiverTideFraction", 0.1f,
                new ConfigDescription("How much of the ocean tide reaches river/swamp water. 0.1 = they keep 90% of "
                    + "their level at low tide (a slow trickle at the mouth); 0 = totally tide-immune; 1 = full ocean tide.",
                    new AcceptableValueRange<float>(0f, 1f)));
            TideMouthBlend = config.Bind(TideShape, "TideMouthBlend", 48f,
                new ConfigDescription("Width (m) of the ocean↔inland crossfade at a river mouth, so the tide eases off "
                    + "as you head upstream instead of stopping at a hard line.",
                    new AcceptableValueRange<float>(8f, 200f)));

            ExtendedRiversEnabled = config.Bind(TideShape, "ExtendedRiversEnabled", false,
                "EXPERIMENTAL MASTER TOGGLE (default OFF). Bend the water plane UP along river spines to make true "
                + "flowing rivers that climb above sea level (Y30). Strictly bank-safe: at every point the surface is "
                + "clamped below the sampled riverbanks (minus RiverBankMargin), so a river whose banks can't hold the "
                + "raise simply isn't raised there — no flooding. Capped by RiverMaxClimb. Both the visual and the "
                + "buoyancy/swim surface follow it together. Client-local, applies live (re-bakes the field on toggle).");
            RiverMaxClimb = config.Bind(TideShape, "RiverMaxClimb", 6f,
                new ConfigDescription("Hard cap (m) on how far above sea level a river surface may climb, before the "
                    + "bank-safety clamp. Keep modest — the banks clamp it lower anyway wherever they're low.",
                    new AcceptableValueRange<float>(0f, 40f)));
            RiverBankMargin = config.Bind(TideShape, "RiverBankMargin", 1.0f,
                new ConfigDescription("Keep the raised surface at least this far (m) below the LOWER of the two banks. "
                    + "Higher = more conservative (less flood risk, lower rivers); the safety headroom.",
                    new AcceptableValueRange<float>(0.25f, 5f)));
            RiverBankSampleWiden = config.Bind(TideShape, "RiverBankSampleWiden", 1.15f,
                new ConfigDescription("Sample the banks this multiple of the channel half-width out from the centerline. "
                    + ">1 checks terrain just beyond the water edge (stricter, safer); 1 = right at the water's edge.",
                    new AcceptableValueRange<float>(1f, 2.5f)));

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
                "Whirlpools that spawn rarely during storms: a rotating current ring, an inward pull, a "
                + "funnel depression in the water and a sink core that can pull ships under. Deterministic "
                + "spawns (all players see the same whirlpools) — no networking involved.");
            Sync(sync, WhirlpoolsEnabled);
            WhirlpoolStormEnvironments = config.Bind(Whirl, "StormEnvironments", "ThunderStorm,Ashlands_SeaStorm,Ashlands_storm",
                "Comma-separated environment names during which whirlpools may spawn.");
            WhirlpoolSpawnChance = config.Bind(Whirl, "SpawnChancePerZone", 0.02f,
                "Chance per 64m ocean zone per time block that a whirlpool forms there while a storm runs. "
                + "0.02 = rare; raise for testing.");
            WhirlpoolBlockSeconds = config.Bind(Whirl, "LifetimeSeconds", 180f,
                "How long one whirlpool roll lives (it swells in and dies down inside this window).");
            WhirlpoolScanRadius = config.Bind(Whirl, "ScanRadius", 320f,
                "How far around the player the spawner evaluates ocean zones.");
            WhirlpoolMinOceanDepth = config.Bind(Whirl, "MinOceanDepth", 8f,
                "Minimum water depth for a spawn — keeps funnels off beaches and shallows.");
            WhirlpoolMaxActive = config.Bind(Whirl, "MaxActive", 3,
                "Cap on simultaneous storm-spawned whirlpools near the player.");
            WhirlpoolOuterRadius = config.Bind(Whirl, "OuterRadius", 60f,
                "Radius where the whirlpool's current begins.");
            WhirlpoolCoreRadius = config.Bind(Whirl, "CoreRadius", 8f,
                "Radius of the sink core — inside this, ships get dragged down.");
            WhirlpoolTangentialSpeed = config.Bind(Whirl, "TangentialSpeed", 5f,
                "Peak rotational current speed (m/s) — the swirl ships get caught in.");
            WhirlpoolRadialSpeed = config.Bind(Whirl, "RadialSpeed", 1.5f,
                "Inward pull current speed (m/s) — never zero inside the ring, strongest near the core.");
            WhirlpoolShipSinkForce = config.Bind(Whirl, "ShipSinkForce", 25f,
                "Downward force on ships inside the core (quadratic ramp — the trap).");
            WhirlpoolShipDamage = config.Bind(Whirl, "ShipDamagePerSecond", 6f,
                "Structural damage per second dealt to a ship held inside the sink core, at full "
                + "strength at dead centre (scales down toward the core edge and with the "
                + "grow/fade ramp). 0 disables hull damage.");
            WhirlpoolFunnelDepth = config.Bind(Whirl, "FunnelDepth", 3f,
                "How deep the water surface bowls at dead centre (affects buoyancy AND the visual).");
            WhirlpoolSpiralArms = config.Bind(Whirl, "SpiralArms", 3f,
                "Number of spiral ridges on the funnel wall.");
            WhirlpoolSpiralSpeed = config.Bind(Whirl, "SpiralSpeed", 1.5f,
                "Rotation speed of the spiral pattern (radians/sec).");
            WhirlpoolRidgeHeight = config.Bind(Whirl, "RidgeHeight", 0.3f,
                "Amplitude of the spiral ridges (fades to zero at the core and the rim).");

            EnableShipCurrents = config.Bind(Ships, "EnableShipCurrents", true,
                "Apply current forces to ships.");
            ShipWaterHeightTolerance = config.Bind(Ships, "ShipWaterHeightTolerance", 2.0f,
                "Ship must be within this height of the water surface to receive current force.");
            MaxCurrentSpeedContribution = config.Bind(Ships, "MaxCurrentSpeedContribution", 2f,
                "Soft limit. Current force tapers as ship speed along the current approaches this.");
            CurrentDirectionSmoothing = config.Bind(Ships, "CurrentDirectionSmoothing", 2.5f,
                "How quickly a ship's current direction is smoothed toward new readings.");
            EnablePlayerCurrents = config.Bind(Ships, "EnablePlayerCurrents", true,
                "Apply current drift to the LOCAL PLAYER while in water (swimming or wading). "
                + "Independent of ship drift and creature drift.");
            EnableCreatureCurrents = config.Bind(Ships, "EnableCreatureCurrents", true,
                "Apply current drift to non-player characters (creatures, mobs) while in water. "
                + "Only the network-owner client applies the force to avoid multiplayer physics "
                + "fights — non-owners see the result via ZDO sync.");
            CharacterCurrentScale = config.Bind(Ships, "CharacterCurrentScale",
                1.0f,
                new ConfigDescription(
                    "Multiplier on current strength when applied to ANY character (player or "
                    + "creature). Treated as a drift velocity bias (m/s) added to the character's "
                    + "swim/walk velocity each fixed step. Higher = stronger pull. Default 2.0 "
                    + "feels clearly noticeable in default-strength currents.",
                    new AcceptableValueRange<float>(0f, 8f)));
            AdminBypassCurrents = config.Bind(Ships, "AdminBypassCurrents", false,
                "When true, currents do not push the local player OR any ship they own if the "
                + "local player is an admin (or the host). Useful for admins building or "
                + "surveying without fighting drift. God-mode and any flying state ALWAYS bypass "
                + "currents regardless of this toggle. Server-authoritative.");
            Sync(sync, EnableShipCurrents);
            Sync(sync, ShipWaterHeightTolerance);
            Sync(sync, MaxCurrentSpeedContribution);
            Sync(sync, CurrentDirectionSmoothing);
            Sync(sync, EnablePlayerCurrents);
            Sync(sync, EnableCreatureCurrents);
            Sync(sync, CharacterCurrentScale);
            Sync(sync, AdminBypassCurrents);

            WindBehindThreshold = config.Bind(Wind, "WindBehindThreshold", 0.20f,
                "Wind alignment above this means wind is helping the ship.");
            WindAgainstThreshold = config.Bind(Wind, "WindAgainstThreshold", -0.25f,
                "Wind alignment below this means wind is fighting the ship.");
            BadWindCurrentMultiplier = config.Bind(Wind, "BadWindCurrentMultiplier", 1.0f,
                "Current strength multiplier when wind is against the ship.");
            AgainstCurrentDrag = config.Bind(Wind, "AgainstCurrentDrag", 0.15f,
                "Extra drag when the ship is fighting both wind and current.");
            CrossCurrentDrag = config.Bind(Wind, "CrossCurrentDrag", 0.05f,
                "Mild drag when wind is bad and current is mostly sideways.");
            Sync(sync, WindBehindThreshold);
            Sync(sync, WindAgainstThreshold);
            Sync(sync, BadWindCurrentMultiplier);
            Sync(sync, AgainstCurrentDrag);
            Sync(sync, CrossCurrentDrag);

            EnableRiverCurrents = config.Bind(Rivers, "EnableRiverCurrents", true,
                "Detect rivers from terrain slope and apply downhill current.");
            RiverCurrentMinStrength = config.Bind(Rivers, "RiverCurrentMinStrength", 0.25f,
                "Minimum river current force.");
            RiverCurrentMaxStrength = config.Bind(Rivers, "RiverCurrentMaxStrength", 0.6f,
                "Maximum river current force.");
            RiverCacheCellSize = config.Bind(Rivers, "RiverCacheCellSize", 32f,
                "Cache grid cell size for river decisions.");
            RiverCacheLifetimeSeconds = config.Bind(Rivers, "RiverCacheLifetimeSeconds", 600f,
                "Seconds before unused river cache cells are pruned.");
            RiverBilinearBlend = config.Bind(Rivers, "RiverBilinearBlend", true,
                "Blend the 4 nearest cells so flow does not step at cell boundaries.");
            RiverScanRadius = config.Bind(RiverScan, "RiverScanRadius", 48f,
                "Radius used to sample terrain slope. Larger reads channel tilt instead of bank-to-bank slope.");
            RiverMinDepth = config.Bind(RiverScan, "RiverMinDepth", 0.25f,
                "Minimum water depth at the cell center for river detection.");
            RiverMaxDepth = config.Bind(RiverScan, "RiverMaxDepth", 5f,
                "Maximum water depth at the cell center for river detection. Deeper falls back to ocean.");
            RiverMinSlopeMagnitude = config.Bind(RiverScan, "RiverMinSlopeMagnitude", 0.02f,
                "Minimum slope magnitude required to count as a river.");
            RiverBankConfirmDepth = config.Bind(RiverScan, "RiverBankConfirmDepth", 0.1f,
                "Bank-confirmation probe depth. Rejects sloping beaches that look like rivers.");
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
                "Open-ocean current force.");
            OceanNoiseScale = config.Bind(Ocean, "OceanNoiseScale", 1500f,
                "World-space wavelength of the ocean current noise field. Larger = broader gyres.");
            OceanNoiseSeed = config.Bind(Ocean, "OceanNoiseSeed", 1337f,
                "Offset into the noise field. Change to reshape ocean currents.");
            Sync(sync, EnableOceanCurrents);
            Sync(sync, OceanCurrentStrength);
            Sync(sync, OceanNoiseScale);
            Sync(sync, OceanNoiseSeed);

            EnableTides = config.Bind(Tides, "EnableTides", true,
                "Master toggle for the tide system.");
            EnableTideHeightApply = config.Bind(Tides, "EnableTideHeightApply", true,
                "If false, tide height is calculated but not pushed to the water surface. For testing.");
            NormalHighTideY = config.Bind(TideRange, "NormalHighTideY", 30f,
                "Standard high tide water y. Matches vanilla so shoreline meshes line up.");
            NormalLowTideY = config.Bind(TideRange, "NormalLowTideY", 29f,
                "Standard low tide water y on a typical day.");
            SpringHighTideY = config.Bind(TideRange, "SpringHighTideY", 30f,
                "High tide y at full or new moon (spring tide).");
            SpringLowTideY = config.Bind(TideRange, "SpringLowTideY", 28f,
                "Low tide y at full or new moon. Exposes more shoreline.");
            BloodMoonHighTideY = config.Bind(TideRange, "BloodMoonHighTideY", 31f,
                "High tide y during a blood moon. Mildly exceeds the vanilla water plane.");
            BloodMoonLowTideY = config.Bind(TideRange, "BloodMoonLowTideY", 27f,
                "Low tide y during a blood moon. Maximum shoreline exposure.");
            TideCyclesPerOrbit = config.Bind(TideCycle, "TideCyclesPerOrbit",
                1f,
                new ConfigDescription(
                    "Number of high/low cycles per moon orbit (one orbit = one Valheim day, "
                    + "auto-tracking any day-length mods via EnvMan.GetDayFraction). "
                    + "1 = one high + one low per day; high tide lands at midnight when the "
                    + "moon is at apex, low tide at noon. 2 = Earth-like two highs + two lows "
                    + "per day (faster cycle, more dramatic on Valheim's ~30-minute day). "
                    + "Values between are valid but unusual.",
                    new AcceptableValueRange<float>(0.5f, 4f)));
            Sync(sync, EnableTides);
            Sync(sync, EnableTideHeightApply);
            Sync(sync, NormalHighTideY);
            Sync(sync, NormalLowTideY);
            Sync(sync, SpringHighTideY);
            Sync(sync, SpringLowTideY);
            Sync(sync, BloodMoonHighTideY);
            Sync(sync, BloodMoonLowTideY);
            Sync(sync, TideCyclesPerOrbit);

            EnableTidalCurrents = config.Bind(Tidal, "EnableTidalCurrents", true,
                "Enable tide-driven ocean currents.");
            OutgoingCurrentStrength = config.Bind(Tidal, "OutgoingCurrentStrength", 0.3f,
                "Force when the tide is going out (water dropping).");
            IncomingCurrentStrength = config.Bind(Tidal, "IncomingCurrentStrength", 0.3f,
                "Force when the tide is coming in (water rising).");
            RiverMouthSwirlRadius = config.Bind(Tidal, "RiverMouthSwirlRadius", 80f,
                "Radius around each river mouth where tidal swirl applies.");
            RiverMouthSwirlStrength = config.Bind(Tidal, "RiverMouthSwirlStrength", 0.4f,
                "Strength multiplier on the rotational swirl at river mouths.");
            Sync(sync, EnableTidalCurrents);
            Sync(sync, OutgoingCurrentStrength);
            Sync(sync, IncomingCurrentStrength);
            Sync(sync, RiverMouthSwirlRadius);
            Sync(sync, RiverMouthSwirlStrength);

            RiverMouthScanCellSize = config.Bind(Mouths, "RiverMouthScanCellSize", 64f,
                "Grid cell size for river-mouth detection.");
            RiverMouthMaxBuildSeconds = config.Bind(Mouths, "RiverMouthMaxBuildSeconds", 8f,
                "Maximum seconds the river-mouth scan may take on world load.");
            Sync(sync, RiverMouthScanCellSize);
            Sync(sync, RiverMouthMaxBuildSeconds);

            const string Debug = "Galaxies - Debug Visualization";
            DebugVisualizeCurrents = config.Bind(Debug, "VisualizeCurrents", false,
                "Draw streamlines that follow the current flow at every grid point around the "
                + "player. Each line is traced by walking N steps along the local current "
                + "direction (so it curves through swirls), and segments are color-coded by "
                + "strength: white = weak, yellow = medium, orange = moderate, red = strong. "
                + "Client-only.");
            DebugCurrentsGridSpacing = config.Bind(Debug, "CurrentsGridSpacing",
                16f,
                new ConfigDescription(
                    "Spacing between streamline seed points in meters.",
                    new AcceptableValueRange<float>(4f, 64f)));
            DebugCurrentsGridRadius = config.Bind(Debug, "CurrentsGridRadius",
                80f,
                new ConfigDescription(
                    "Radius around the player to sample the current field.",
                    new AcceptableValueRange<float>(16f, 256f)));
            DebugCurrentsArrowScale = config.Bind(Debug, "CurrentsArrowScale",
                4f,
                new ConfigDescription(
                    "Length multiplier on each streamline step. Larger = longer lines that trace "
                    + "further into the flow. Step length is also scaled by local current strength "
                    + "so strong currents stretch the line and weak ones curl it tight.",
                    new AcceptableValueRange<float>(0.5f, 20f)));
            DebugCurrentsStreamlineSteps = config.Bind(Debug, "CurrentsStreamlineSteps",
                12,
                new ConfigDescription(
                    "Number of segments per streamline. Higher = longer, smoother curves; lower = "
                    + "shorter arrow-like marks. 1 reduces to a single arrow per seed.",
                    new AcceptableValueRange<int>(1, 32)));
            DebugVisualizeOrbits = config.Bind(Debug, "VisualizeOrbits", false,
                "Trace the full orbital path of every active FiresOrbitals body in world space. "
                + "Useful for adjusting orbit parameters and seeing the shape change live. "
                + "Client-only. Reads this mod's FiresOrbitals.");
            DebugOrbitsResolution = config.Bind(Debug, "OrbitsResolution",
                64,
                new ConfigDescription(
                    "How many segments make up each traced orbit ring.",
                    new AcceptableValueRange<int>(16, 256)));

            // Player swimming is Core's [Swimming] setting (0.1.30); these keys are creature diving only.
            CreatureDivingEnabled = config.Bind(Diving, "CreatureDivingEnabled", true,
                "Let AI creatures dive to follow a diving target instead of being stuck at the surface.");
            CreatureDiveMaxDepth = config.Bind(Diving, "CreatureMaxDiveDepth", 30f,
                new ConfigDescription("How far below the surface (m) a creature will sink chasing a goal.",
                    new AcceptableValueRange<float>(2f, 100f)));
            CreatureDiveSpeed = config.Bind(Diving, "CreatureDiveSpeed", 3f,
                new ConfigDescription("How fast a creature descends/ascends (m/s) following its goal.",
                    new AcceptableValueRange<float>(0.5f, 10f)));
            Sync(sync, CreatureDivingEnabled);
            Sync(sync, CreatureDiveMaxDepth);
            Sync(sync, CreatureDiveSpeed);

            ApplyDiving();
            CreatureDivingEnabled.SettingChanged += (_, __) => ApplyDiving();
            CreatureDiveMaxDepth.SettingChanged  += (_, __) => ApplyDiving();
            CreatureDiveSpeed.SettingChanged     += (_, __) => ApplyDiving();

            MoonEnabled = config.Bind(Moon, "Enabled", true,
                "Render the 3D moon with physical phases (loaded from the firesgalaxies bundle). "
                + "The moon drives the tides.");
            MoonDistance = config.Bind(Moon, "Distance", 4500f,
                new ConfigDescription("Sky distance of the moon (m).", new AcceptableValueRange<float>(500f, 12000f)));
            MoonSizeScale = config.Bind(Moon, "SizeScale", 1.0f,
                new ConfigDescription("Visual size multiplier.", new AcceptableValueRange<float>(0.2f, 5f)));
            MoonSynodicDays = config.Bind(Moon, "SynodicDays", 30f,
                new ConfigDescription("Game days per full lunar cycle. Drives BOTH the moon phase and the tides. "
                    + "Not used while FollowVedrSeasons is on and Vedr is installed.",
                    new AcceptableValueRange<float>(1f, 120f)));
            // 0.1.33 (Fire 10-06: "realistic is subjective here and when vedr is installed its cycles should roughly match the
            // seasons based of vedr configs, so a ten day summer would see things differently from a 30 day seasonal setting").
            MoonFollowVedr = config.Bind(Moon, "FollowVedrSeasons", true,
                "When Vedr is installed, the lunar cycle (moon phases, its orbit and the tides) takes its length from Vedr's "
                + "'Days per season' divided by CyclesPerSeason, instead of SynodicDays. Without Vedr, SynodicDays is used.");
            MoonCyclesPerSeason = config.Bind(Moon, "CyclesPerSeason", 1f,
                new ConfigDescription("FollowVedrSeasons: full moon cycles in one Vedr season. 1 = one cycle a season "
                    + "(a 10-day season gives a 10-day cycle, a 30-day season a 30-day one); 3 is about Earth's.",
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
            // 0.1.38 (Fire): admins and hosts feel currents too; a config still on the old default (true) moves to false.
            if (AdminBypassCurrents != null && AdminBypassCurrents.Value && System.Convert.ToBoolean(AdminBypassCurrents.DefaultValue) == false
                && !s_bypassMigrated)
            {
                s_bypassMigrated = true;
                AdminBypassCurrents.Value = false;
                UnityEngine.Debug.Log("[Galaxies] Galaxies - Ships/AdminBypassCurrents moved from the old default True to False");
            }
        }

        private static bool s_bypassMigrated;

        private static void MigrateDefault(ConfigEntry<float> entry, float oldDefault, float newDefault)
        {
            if (entry == null || UnityEngine.Mathf.Abs(entry.Value - oldDefault) > 0.0001f) return;
            entry.Value = newDefault;
            UnityEngine.Debug.Log($"[Galaxies] {entry.Definition.Section}/{entry.Definition.Key} moved from the old default {oldDefault} to {newDefault}");
        }

        // Push creature diving config into CreatureDiving's static fields. Re-pushed
        // on any change so the BepInEx config UI is live.
        private static void ApplyDiving()
        {
            VerdantsAscent.Diving.CreatureDiving.Enabled             = CreatureDivingEnabled.Value;
            VerdantsAscent.Diving.CreatureDiving.MaxDiveDepth        = CreatureDiveMaxDepth.Value;
            VerdantsAscent.Diving.CreatureDiving.DescendSpeed        = CreatureDiveSpeed.Value;
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
                    "Spawn this orbiting body. Slot A defaults on with the FiresMoon model so the system is visible out of the box.");
                OrbitalPrefab[i]     = config.Bind(sec, "02 PrefabName", i == 0 ? "FiresMoon" : "",
                    "Prefab to orbit. 'FiresMoon' = the moon model from the firesgalaxies bundle (a reachable orbiting moon). "
                    + "Otherwise any ZNetScene prefab name (ship / piece / FAP blueprint). Empty or not found => placeholder sphere.");
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
                    "How this body moves. Auto: Realistic for the FiresMoon prefab, Custom for anything else. "
                    + "Realistic: a real moon on the game clock: it crosses the sky once a day, rises a little later each day and goes through its phases "
                    + "over the lunar cycle ([Galaxies - Moon] SynodicDays, or Vedr's season with FollowVedrSeasons), on a plane tilted by OrbitInclinationDeg; '11 PhaseOffsetDeg' shifts it along that cycle. "
                    + "Custom: the egg orbit above (03-10, 14, 15), one lap per '06 PeriodSeconds'. "
                    + "Locked: held still at '17 LockAzimuthDeg' / '18 LockElevationDeg'. "
                    + "AntiSun: always opposite the sun, where the moonlight comes from (the pre-0.1.32 moon, always full).");
                OrbitalLockAz[i]     = config.Bind(sec, "17 LockAzimuthDeg", 180f, new ConfigDescription("Locked: compass heading of the body (deg; 0 = north, 90 = east, 180 = south).", new AcceptableValueRange<float>(0f, 360f)));
                OrbitalLockEl[i]     = config.Bind(sec, "18 LockElevationDeg", 35f, new ConfigDescription("Locked: height above the horizon (deg; 90 = straight up, negative = below).", new AcceptableValueRange<float>(-90f, 90f)));
                OrbitalTidalLock[i]  = config.Bind(sec, "19 TidalLock", i == 0,
                    "Keep the same face toward the world, like a real moon (it never seems to turn). Off: it spins at '12 SpinDegPerSec'.");
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
