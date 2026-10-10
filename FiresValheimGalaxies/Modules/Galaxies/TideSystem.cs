using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    // Computes the current tide state from moon input and exposes the resulting
    // water-surface offset. All math is a pure function of moon state, so every
    // client given the same world time and config arrives at the same answer
    // with no sync needed.
    //
    // The offset is applied two ways:
    //   - Physics/logic: WaterVolumeTidePatch adds SurfaceOffset to vanilla
    //     water-surface queries (buoyancy, swimming, depth).
    //   - Visual: pushed as the _FiresTideOffset shader global for FiresWater.
    internal static class TideSystem
    {
        public struct State
        {
            public float WaterY;         // current water surface y
            public float TideVelocity;   // d(WaterY)/dt; sign tells incoming/outgoing
            public float TidePhase01;    // 0 = low, 0.5 = mid, 1 = high
            public float SpringWeight;   // 0..1, 1 at full/new moon
            public bool  IsBloodMoon;
            public float HighY;
            public float LowY;
        }

        // Current water surface minus the vanilla ocean baseline. Zero when tide
        // height application is disabled. Read by WaterVolumeTidePatch and WaterUtil.
        public static float SurfaceOffset { get; private set; }

        private static readonly int Prop_TideOffset = Shader.PropertyToID("_FiresTideOffset");

        private static State _state;
        private static float _lastWaterY;
        private static float _lastSampleTime;

        public static State Current => _state;

        public static void Reset()
        {
            _state = default;
            _lastWaterY = 0f;
            _lastSampleTime = 0f;
            ClearSurface();
        }

        // Fire (10-01 23:2x: "just remove hd additions and let vedrs floods read from galaxies when its installed"): another
        // mod's whole-sea offset (Vedr's floods + / droughts -) rides this same channel, so buoyancy/swimming (WaterVolumeTidePatch)
        // and the rendered sheet (_FiresTideOffset) move together instead of the other mod moving the WaterVolumes itself.
        // Vedr calls it by reflection (no compile reference): SetExternalOffset("Vedr", metres); 0 removes the source.
        private static readonly System.Collections.Generic.Dictionary<string, float> s_external = new System.Collections.Generic.Dictionary<string, float>();
        private static float s_externalSum;
        private static readonly System.Collections.Generic.HashSet<string> s_sources = new System.Collections.Generic.HashSet<string>();

        public static float ExternalOffset => s_externalSum;

        public static void SetExternalOffset(string source, float metres)
        {
            if (string.IsNullOrEmpty(source)) return;
            if (s_sources.Add(source))
                FiresLogger.LogInfo($"[Galaxies] tide channel: '{source}' hooked SetExternalOffset; its water offset moves swimming and the rendered sea with the tides.");
            if (Mathf.Abs(metres) < 0.0005f) s_external.Remove(source);
            else s_external[source] = metres;
            float sum = 0f;
            foreach (float v in s_external.Values) sum += v;
            s_externalSum = sum;
        }

        // 0.1.36: the single ocean owner. Galaxies owns: an outside (Vedr) offset is ignored. Vedr owns: Galaxies' tides stand
        // down and only the outside offset is on the channel.
        // 0.1.37: with Galaxies as owner a Vedr flood/drought still ADDS on top (an event layer); only the double tide is gone.
        private static float Ext => s_externalSum;
        private static float s_nextClampLog;

        private static float Clamp(float offset)
        {
            float max = GalaxiesConfig.MaxCombinedSeaOffset != null ? GalaxiesConfig.MaxCombinedSeaOffset.Value : 3.5f;
            float c = Mathf.Clamp(offset, -max, max);
            if (!Mathf.Approximately(c, offset) && Time.time >= s_nextClampLog)
            {
                s_nextClampLog = Time.time + 60f;
                FiresLogger.LogInfo($"[Galaxies] sea offset {offset:0.00} m (tide + events {s_externalSum:0.00} m) clamped to {c:0.00} m (MaxCombinedSeaOffset {max:0.#})");
            }
            return c;
        }
        private static string s_loggedOwner;

        public static void Tick()
        {
            string owner = GalaxiesConfig.GalaxiesOwnsOcean ? "Galaxies" : "Vedr";
            if (owner != s_loggedOwner)
            {
                s_loggedOwner = owner;
                FiresLogger.LogInfo($"[Galaxies] ocean owner: {owner} ({(owner == "Galaxies" ? "Galaxies tides and currents are the baseline; Vedr events layer on top, clamped to MaxCombinedSeaOffset" : "Vedr floods/droughts; Galaxies tides and currents stand down")})");
            }
            if (!GalaxiesConfig.GalaxiesOwnsOcean)
            {
                ClearSurface();
                return;
            }
            if (!GalaxiesConfig.EnableTides.Value)
            {
                ClearSurface();
                return;
            }

            ComputeState(ref _state);

            float now = Time.time;
            float dt  = now - _lastSampleTime;
            if (dt > 0.0001f)
            {
                _state.TideVelocity = (_state.WaterY - _lastWaterY) / dt;
                _lastWaterY     = _state.WaterY;
                _lastSampleTime = now;
            }

            if (GalaxiesConfig.EnableTideHeightApply.Value)
                ApplySurface(_state.WaterY);
            else
                ClearSurface();
        }

        private static void ComputeState(ref State state)
        {
            bool blood = MoonInput.IsBloodMoon();
            float springWeight = MoonInput.GetSpringTideWeight();
            float orbit = MoonInput.GetOrbitAngleRadians();

            float highY, lowY;
            if (blood)
            {
                highY = GalaxiesConfig.BloodMoonHighTideY.Value;
                lowY  = GalaxiesConfig.BloodMoonLowTideY.Value;
            }
            else
            {
                highY = Mathf.Lerp(GalaxiesConfig.NormalHighTideY.Value, GalaxiesConfig.SpringHighTideY.Value, springWeight);
                lowY  = Mathf.Lerp(GalaxiesConfig.NormalLowTideY.Value,  GalaxiesConfig.SpringLowTideY.Value,  springWeight);
            }

            float cyclesPerOrbit = Mathf.Max(0.25f, GalaxiesConfig.TideCyclesPerOrbit.Value);
            float tideAngle  = orbit * cyclesPerOrbit;
            float tideLevel  = 0.5f + 0.5f * Mathf.Cos(tideAngle); // 1 = high, 0 = low

            state.HighY        = highY;
            state.LowY         = lowY;
            state.WaterY       = Mathf.Lerp(lowY, highY, tideLevel);
            state.TidePhase01  = tideLevel;
            state.SpringWeight = springWeight;
            state.IsBloodMoon  = blood;
        }

        private static void ApplySurface(float y)
        {
            SurfaceOffset = Clamp(y - WaterUtil.OceanBaselineY() + Ext);
            Shader.SetGlobalFloat(Prop_TideOffset, SurfaceOffset);
        }

        // Tides off (or their height not applied): only the outside offset (a Vedr flood/drought) is left on the channel.
        private static void ClearSurface()
        {
            SurfaceOffset = Clamp(Ext);
            Shader.SetGlobalFloat(Prop_TideOffset, SurfaceOffset);
        }
    }
}
