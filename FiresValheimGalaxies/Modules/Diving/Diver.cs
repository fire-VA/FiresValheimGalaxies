using System.Collections.Generic;
using UnityEngine;

namespace VerdantsAscent.Diving
{
    /// <summary>
    /// Per-player diving state, attached on Player.Awake. Drives vanilla
    /// <c>Player.m_swimDepth</c> (1.4 = treading at the surface; larger = body
    /// pushed deeper) plus <c>m_moveDir</c> so the character actually descends/
    /// ascends. Faithful port of the Under-The-Sea Diver, with the version-risky
    /// equipment-stamina / pushback / status-speed calls trimmed out (a v1
    /// simplification — re-add later if needed). All tuning reads
    /// <see cref="FiresDiving"/>.
    /// </summary>
    public class Diver : MonoBehaviour
    {
        public const float DefaultSwimDepth = 1.4f;   // vanilla treading depth
        public const float DivingSwimDepth  = 2.5f;   // past this = "diving", not just dipped

        private const float MinDiveVertical    = 0.35f; // crouch always at least gently sinks / jump rises
        private const float ViewSubmergeMargin = 0.15f; // swim-depth past treading before the view goes under

        private static readonly Dictionary<Player, Diver> s_divers = new Dictionary<Player, Diver>();

        public Player player;
        public float BaseSwimSpeed { get; private set; }

        private bool  m_holdingDepth;   // Coast() is holding a steady world-Y
        private float m_holdWorldY;     // the world-Y we hold while coasting straight

        public static bool TryGet(Character c, out Diver diver)
        {
            diver = null;
            return c is Player p && s_divers.TryGetValue(p, out diver) && diver != null;
        }

        private void Awake()
        {
            player = GetComponent<Player>();
            if (player == null) { Object.Destroy(this); return; }
            player.m_swimDepth = DefaultSwimDepth;
            BaseSwimSpeed = player.m_swimSpeed;
            s_divers[player] = this;
        }

        private void OnDestroy()
        {
            if (player != null) s_divers.Remove(player);
        }

        // ---- State queries -------------------------------------------------
        public void ResetSwimDepthIfNotInWater()
        {
            if (!player.InWater()) ResetSwimDepthToDefault();
        }

        public void ResetSwimDepthToDefault()
        {
            player.m_swimDepth = DefaultSwimDepth;
            m_holdingDepth = false;
        }

        // Can start diving DOWN: in water, off the ground, currently swimming.
        // (Under-The-Sea also gates on seafloor clearance via GetGroundHeight;
        // dropped here to avoid a version-specific overload — vanilla swim
        // collision already stops the body at the floor.)
        public bool CanDive()
            => player.InWater() && !player.IsOnGround() && player.IsSwimming();

        public bool IsInsideLiquid()
        {
            float surface = player.GetLiquidLevel();
            float sub = Mathf.Max(0f, surface - player.transform.position.y);
            if (sub > DefaultSwimDepth) return true;
            // Shallow columns (generated rivers run ~1.1m deep): the fixed
            // threshold could NEVER arm there, so every hold gate bailed and
            // releasing dive snapped the body back to the surface. Arm once
            // submerged near the local bottom instead — the threshold scales
            // to the column, floored so ankle water cannot arm it.
            if (ZoneSystem.instance != null
                && ZoneSystem.instance.GetSolidHeight(player.transform.position, out float ground))
            {
                float column = surface - ground;
                if (column > 0.9f && sub > Mathf.Max(0.55f, column - 0.45f)) return true;
            }
            return false;
        }

        public bool IsUnderSurface() => player.m_swimDepth > DefaultSwimDepth;
        public bool IsDiving()       => player.m_swimDepth > DivingSwimDepth;
        public bool IsSurfacing()    => !IsDiving() && IsUnderSurface();
        public bool IsRestingInWater()
            => !IsDiving() && player.IsSwimming() && player.GetVelocity().magnitude < 1f;

        // View gate: deep enough that the camera + underwater env should switch
        // to the submerged look. The margin past treading means the head bobbing
        // across the waterline at the surface doesn't flicker the view.
        public bool IsSubmergedForView() => player.m_swimDepth > DefaultSwimDepth + ViewSubmergeMargin;

        // Is the LOCAL player submerged-for-view? Used by the camera clamp + the
        // underwater env driver so the surface view stays locked while treading
        // and only switches once an actual dive starts.
        public static bool LocalIsSubmerged()
        {
            var p = Player.m_localPlayer;
            return p != null && s_divers.TryGetValue(p, out var d) && d != null && d.IsSubmergedForView();
        }

        // ---- Stamina -------------------------------------------------------
        public void RegenRestingStamina(float dt)
        {
            float delay = -FiresDiving.RestingStaminaRegenDelay * player.m_staminaRegenDelay;
            player.m_staminaRegenTimer -= dt;
            if (player.GetStamina() >= player.GetMaxStamina() || player.m_staminaRegenTimer > delay) return;
            float rate = FiresDiving.RestingStaminaRegenRate * player.m_staminaRegen;
            float gain = (1f + player.m_skills.GetSkillFactor(Skills.SkillType.Swim)) * rate;
            player.m_stamina = Mathf.Min(player.GetMaxStamina(),
                player.m_stamina + gain * dt * Game.m_staminaRegenRate);
        }

        public void DrainDivingStamina(float dt)
        {
            float drain = Mathf.Lerp(player.m_swimStaminaDrainMinSkill, player.m_swimStaminaDrainMaxSkill,
                                     player.m_skills.GetSkillFactor(Skills.SkillType.Swim));
            player.UseStamina(dt * drain * Game.m_moveStaminaRate * FiresDiving.UnderwaterStaminaDrain);
        }

        public void UpdateSwimSkill(float dt)
        {
            player.m_swimSkillImproveTimer += dt;
            if (player.m_swimSkillImproveTimer <= 1f) return;
            player.m_swimSkillImproveTimer = 0f;
            player.RaiseSkill(Skills.SkillType.Swim, 1f);
        }

        // ---- Movement ------------------------------------------------------
        public void UpdateSwimSpeed(float dt)
        {
            bool run = ZInput.GetButton("Run") || ZInput.GetButton("JoyRun");
            player.m_swimSpeed = Mathf.Clamp(
                player.m_swimSpeed + player.m_swimAcceleration * (run ? dt : -dt),
                BaseSwimSpeed, FiresDiving.MaxSwimSpeed);
        }

        public void Dive(float dt, bool ascend, out Vector3? defaultMoveDir)
        {
            m_holdingDepth = false;   // re-capture the hold level next time we coast
            defaultMoveDir = player.m_moveDir;
            player.m_moveDir = GetDiveDirection(ascend);
            player.m_swimDepth = Mathf.Max(player.m_swimDepth - CalculateSwimVelocity().y * dt, DefaultSwimDepth);
        }

        // Smooth dive direction: follow where the camera looks (full 3D pitch) so
        // motion runs along a natural angle instead of snapping to straight up/
        // down. The vertical is clamped so CROUCH always at least gently descends
        // and JUMP always at least gently rises, even when looking the other way.
        public Vector3 GetDiveDirection(bool ascend)
        {
            Vector3 look = player.m_lookDir;
            if (look.sqrMagnitude < 0.0001f) return ascend ? Vector3.up : Vector3.down;
            look.Normalize();

            Vector3 horiz = new Vector3(look.x, 0f, look.z);   // swim toward where you face
            float vertical = ascend
                ? Mathf.Max(look.y,  MinDiveVertical)          // rise: at least gently up
                : Mathf.Min(look.y, -MinDiveVertical);         // descend: at least gently down

            Vector3 dir = horiz + Vector3.up * vertical;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : (ascend ? Vector3.up : Vector3.down);
        }

        // Surface swimming (NOT diving): ride the wave surface like a boat.
        // Pinning swimDepth at treading depth makes vanilla float the body toward
        // (surface - 1.4) every frame, so the body bobs up and OVER each crest —
        // high buoyancy, you stay on the surface, and a wave can't shove your head
        // under. This is also what keeps the underwater env from triggering on
        // surface chop: swimDepth never crosses the dive-view threshold (1.55)
        // while riding. The opposite of Coast() below, which is used while DIVING
        // so you bob less.
        public void RideSurface()
        {
            m_holdingDepth = false;
            player.m_swimDepth = DefaultSwimDepth;
        }

        // Coasting while DIVING (no dive/rise input): hold a steady world-Y so the
        // submerged body doesn't get yanked up and down by the wave surface.
        // Setting swimDepth = surface - heldY makes vanilla's float target a
        // CONSTANT world height, so wave motion no longer moves the body. The Max
        // keeps us at least treading-depth under, so only a trough deep enough to
        // expose us drags us down ("drop only if a wave would pull us out").
        public void Coast()
        {
            // The CHARACTER's liquid level — fed by the generated-water swim
            // router. Floating.GetLiquidLevel(pos) only knows vanilla
            // WaterVolumes: rivers returned -10000, Coast bailed every frame,
            // nothing maintained the hold, and vanilla floated the body back
            // to the surface — the "forced up the second we release" report.
            // Ocean never showed it because ocean HAS a WaterVolume.
            float surfaceY = player.GetLiquidLevel();
            if (surfaceY <= -1000f) { m_holdingDepth = false; return; }   // no water here

            if (!m_holdingDepth)
            {
                m_holdWorldY = player.transform.position.y;
                m_holdingDepth = true;
            }

            player.m_swimDepth = Mathf.Max(surfaceY - m_holdWorldY, DefaultSwimDepth);
        }

        // Simplified vanilla swim velocity (status-effect speed mods + pushback
        // trimmed vs the ref to dodge version-specific signatures).
        public Vector3 CalculateSwimVelocity()
        {
            float speed = player.m_swimSpeed * player.GetAttackSpeedFactorMovement();
            if (player.InMinorActionSlowdown()) speed = 0f;
            return Vector3.Lerp(player.m_currentVel, player.m_moveDir * speed, player.m_swimAcceleration);
        }
    }
}
