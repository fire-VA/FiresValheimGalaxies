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

        public static void Tick()
        {
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
            SurfaceOffset = y - WaterUtil.OceanBaselineY();
            Shader.SetGlobalFloat(Prop_TideOffset, SurfaceOffset);
        }

        private static void ClearSurface()
        {
            SurfaceOffset = 0f;
            Shader.SetGlobalFloat(Prop_TideOffset, 0f);
        }
    }
}
