using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    // Gender change consequences that depend on what was changed into what:
    // the pawn's ideoligion may put one sex above the other, and love partners
    // may not be drawn to the new body. Used by the conversion and by the forecast.
    public static class GenderShiftUtility
    {
        public static bool IsChange(Gender from, Gender to)
        {
            return from != Gender.None && to != Gender.None && from != to;
        }

        // The sex the pawn's ideoligion puts first (Ideology memes), or None.
        public static Gender PreferredGender(Pawn pawn)
        {
            if (!ModsConfig.IdeologyActive || pawn?.Ideo == null)
            {
                return Gender.None;
            }
            if (MemeDefOf.MaleSupremacy != null && pawn.Ideo.HasMeme(MemeDefOf.MaleSupremacy))
            {
                return Gender.Male;
            }
            if (MemeDefOf.FemaleSupremacy != null && pawn.Ideo.HasMeme(MemeDefOf.FemaleSupremacy))
            {
                return Gender.Female;
            }
            return Gender.None;
        }

        // IC_GenderLowered if the ideoligion puts the old sex first, IC_GenderRaised if it puts the new one first.
        public static ThoughtDef IdeoReaction(Pawn pawn, Gender from, Gender to)
        {
            if (!IsChange(from, to))
            {
                return null;
            }
            Gender preferred = PreferredGender(pawn);
            if (preferred == from)
            {
                return IC_DefOf.IC_GenderLowered;
            }
            if (preferred == to)
            {
                return IC_DefOf.IC_GenderRaised;
            }
            return null;
        }

        // Living love partners who were drawn to the old sex but are not drawn to the new one.
        public static List<Pawn> PartnersLosingAttraction(Pawn pawn, Gender from, Gender to)
        {
            List<Pawn> result = new List<Pawn>();
            if (!IsChange(from, to) || pawn?.relations == null)
            {
                return result;
            }
            foreach (DirectPawnRelation relation in LovePartnerRelationUtility.ExistingLovePartners(pawn, false))
            {
                Pawn partner = relation.otherPawn;
                if (partner == null || partner.Dead || partner.story == null || result.Contains(partner))
                {
                    continue;
                }
                if (RelationsUtility.AttractedToGender(partner, from) && !RelationsUtility.AttractedToGender(partner, to))
                {
                    result.Add(partner);
                }
            }
            return result;
        }

        public static void ApplyReactions(Pawn pawn, Gender from, Gender to, AssimilationReport report)
        {
            ThoughtDef ideoThought = IdeoReaction(pawn, from, to);
            if (ideoThought != null && pawn.needs?.mood?.thoughts?.memories != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(ideoThought);
                report?.consequences.Add(DescribeMemory(ideoThought));
            }
            foreach (Pawn partner in PartnersLosingAttraction(pawn, from, to))
            {
                if (partner.needs?.mood?.thoughts?.memories == null)
                {
                    continue;
                }
                partner.needs.mood.thoughts.memories.TryGainMemory(IC_DefOf.IC_PartnerGenderChanged, pawn);
                report?.consequences.Add(DescribePartner(partner));
            }
        }

        // "Now the lesser sex: mood -12 for 30 days"
        public static string DescribeMemory(ThoughtDef def)
        {
            ThoughtStage stage = def.stages[0];
            return "IC_ForecastMemory".Translate(stage.label.CapitalizeFirst(), Signed(stage.baseMoodEffect), Mathf.RoundToInt(def.durationDays)).Resolve();
        }

        // "Anna is not drawn to the new sex: opinion -20, mood -6"
        public static string DescribePartner(Pawn partner)
        {
            ThoughtStage stage = IC_DefOf.IC_PartnerGenderChanged.stages[0];
            return "IC_ForecastPartner".Translate(partner.LabelShort, Signed(stage.baseOpinionOffset), Signed(stage.baseMoodEffect)).Resolve();
        }

        private static string Signed(float value)
        {
            return value.ToString("+0;-0;0");
        }
    }
}
