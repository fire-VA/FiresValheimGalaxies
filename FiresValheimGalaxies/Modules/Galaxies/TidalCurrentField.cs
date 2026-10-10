using UnityEngine;

namespace VerdantsAscent.Galaxies
{
    // Maps (world position, tide state) to a current vector.
    //   - Outgoing tide: vectors point offshore; near a river mouth, push out
    //     along the river's outflow direction.
    //   - Incoming tide: vectors point shoreward and swirl around river mouths.
    //   - Slack tide: magnitude tapers to zero and CurrentSystem falls back to
    //     the open-ocean noise field.
    // Strength scales with |TideVelocity|, so peak current is mid-tide and zero
    // at the turn.
    internal static class TidalCurrentField
    {
        public static bool TryGetCurrent(Vector3 pos, out Vector3 direction, out float strength)
        {
            direction = Vector3.zero;
            strength  = 0f;

            if (!GalaxiesConfig.EnableTidalCurrents.Value)
                return false;

            TideSystem.State tide = TideSystem.Current;

            float peakVelocity = EstimatePeakVelocity(tide);
            float t = peakVelocity > 1e-4f
                ? Mathf.Clamp(tide.TideVelocity / peakVelocity, -1f, 1f)
                : 0f;

            if (Mathf.Abs(t) < 0.05f)
                return false; // slack tide

            bool incoming = t > 0f;
            float baseStrength = incoming
                ? GalaxiesConfig.IncomingCurrentStrength.Value
                : GalaxiesConfig.OutgoingCurrentStrength.Value;

            Vector2 here = new Vector2(pos.x, pos.z);
            Vector2 flow = ComputeFlow(here, incoming, out float swirlBoost);

            if (flow.sqrMagnitude < 1e-6f)
                return false;

            flow.Normalize();
            direction = new Vector3(flow.x, 0f, flow.y);
            strength  = baseStrength * Mathf.Abs(t) * swirlBoost;
            return true;
        }

        private static Vector2 ComputeFlow(Vector2 here, bool incoming, out float swirlBoost)
        {
            swirlBoost = 1f;

            Vector2 acc = Vector2.zero;
            float radius = Mathf.Max(8f, GalaxiesConfig.RiverMouthSwirlRadius.Value);
            float radiusSq = radius * radius;
            float swirlStrength = GalaxiesConfig.RiverMouthSwirlStrength.Value;
            bool anyMouthClose = false;

            foreach (RiverMouthRegistry.Mouth m in RiverMouthRegistry.All)
            {
                Vector2 delta = here - m.Position;
                float dSq = delta.sqrMagnitude;
                if (dSq > radiusSq) continue;

                anyMouthClose = true;
                float d = Mathf.Sqrt(dSq);
                float falloff = 1f - (d / radius);

                if (incoming)
                {
                    Vector2 radial = d > 0.001f ? delta / d : Vector2.zero;
                    Vector2 tangent = new Vector2(-radial.y, radial.x);
                    Vector2 inward = -radial;
                    acc += (tangent * swirlStrength + inward) * falloff;
                }
                else
                {
                    acc += m.OutflowDir * falloff * swirlStrength;
                }
            }

            if (anyMouthClose)
            {
                swirlBoost = 1.25f;
                return acc;
            }

            // Open-ocean fallback: radial from world origin. Coarse near (0,0) but
            // adequate until a shoreline-normal field is available.
            Vector2 radialFromOrigin = here.sqrMagnitude > 1f ? here.normalized : Vector2.zero;
            return incoming ? -radialFromOrigin : radialFromOrigin;
        }

        // Peak tide velocity for normalization.
        // y(t) = mid + amp * cos(omega t)  =>  peak |dy/dt| = amp * omega
        private static float EstimatePeakVelocity(TideSystem.State tide)
        {
            float amplitude = (tide.HighY - tide.LowY) * 0.5f;
            float dayLength = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f;
            float cyclesPerOrbit = Mathf.Max(0.25f, GalaxiesConfig.TideCyclesPerOrbit.Value);
            float secondsPerTideCycle = dayLength / cyclesPerOrbit;
            float omega = (2f * Mathf.PI) / Mathf.Max(0.001f, secondsPerTideCycle);
            return amplitude * omega;
        }
    }
}
