using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace VerdantsAscent.Diving
{
    /// <summary>
    /// Lets AI creatures DIVE underwater instead of being stuck floating on the
    /// surface. Vanilla holds a swimming Character at <c>liquidLevel -
    /// m_swimDepth</c> (Character.UpdateSwimming) — the body's vertical position
    /// is ENTIRELY a function of m_swimDepth; the AI only drives horizontal
    /// motion (UpdateSwimming zeroes the Y of its move force). So "diving" a
    /// creature is just: raise its m_swimDepth so it sinks to the depth of
    /// whatever it's pursuing.
    ///
    /// We drive m_swimDepth toward the creature's current GOAL:
    ///   * attack target (MonsterAI.m_targetCreature) — a predator follows a
    ///     diving player/prey under the surface, OR
    ///   * follow target (MonsterAI.GetFollowTarget) — a TAMED creature follows
    ///     its diving owner down.
    /// When the goal is at/above the surface (or there's no goal) the depth eases
    /// back to the creature's prefab default, so idle creatures stay on top.
    ///
    /// Owner-only (AI + physics run on the ZDO owner). Player Characters are
    /// skipped — player swimming is Core's (PlayerSwim). Wild creatures may drop an underwater
    /// target if MonsterAI can't path to it (vanilla behaviour); tamed-follow is
    /// the reliable case. AnimalAI (passive grazers) is not covered in v1 — they
    /// flee rather than pursue, so they have no underwater goal to dive toward.
    /// </summary>
    public static class CreatureDiving
    {
        // Master on/off (config). When off, the prefab default swim depth is
        // restored so creatures behave exactly like vanilla.
        public static bool  Enabled       = true;
        // How deep below the surface a creature will chase a goal (m). Caps how
        // far they'll sink so they don't bury themselves chasing a deep target.
        public static float MaxDiveDepth  = 30f;
        // How fast m_swimDepth changes (m/s) — the descend/ascend rate.
        public static float DescendSpeed  = 3f;
    }

    /// <summary>
    /// Prefix on Character.UpdateSwimming — runs immediately before vanilla reads
    /// m_swimDepth to compute the float height, so the value we set takes effect
    /// this same physics step.
    /// </summary>
    [HarmonyPatch(typeof(Character), "UpdateSwimming")]
    internal static class Character_UpdateSwimming_CreatureDive
    {
        // Per-creature prefab default swim depth, captured the first time we see
        // it (before we ever change m_swimDepth). ConditionalWeakTable auto-drops
        // the entry when the Character is GC'd, so no manual cleanup / leak.
        private static readonly ConditionalWeakTable<Character, StrongBox<float>> s_baseDepth
            = new ConditionalWeakTable<Character, StrongBox<float>>();

        private static FieldInfo s_targetField;

        private static void Prefix(Character __instance, float dt)
        {
            if (!CreatureDiving.Enabled || __instance == null || __instance.IsPlayer()) return;

            var ai = __instance.GetComponent<MonsterAI>();
            if (ai == null) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;

            // Capture the prefab's base swim depth ONCE, before we touch it.
            float baseDepth = s_baseDepth.GetValue(__instance, c => new StrongBox<float>(c.m_swimDepth)).Value;

            if (s_targetField == null)
                s_targetField = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");

            // Current goal: attack target first, else follow target (tamed).
            Vector3? goal = null;
            if (s_targetField != null && s_targetField.GetValue(ai) is Character target && target != null)
                goal = target.transform.position;
            else
            {
                var follow = ai.GetFollowTarget();
                if (follow != null) goal = follow.transform.position;
            }

            float surface = __instance.GetLiquidLevel();
            float desired = baseDepth;
            // Only dive when the goal sits meaningfully BELOW the surface.
            if (goal.HasValue && goal.Value.y < surface - 0.5f)
                desired = Mathf.Clamp(surface - goal.Value.y, baseDepth, CreatureDiving.MaxDiveDepth);

            __instance.m_swimDepth = Mathf.MoveTowards(
                __instance.m_swimDepth, desired, CreatureDiving.DescendSpeed * dt);
        }
    }
}
