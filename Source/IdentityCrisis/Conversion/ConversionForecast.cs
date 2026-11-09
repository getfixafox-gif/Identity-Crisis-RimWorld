using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    public class ForecastLine
    {
        public string text;
        public Color color;
        public bool indent;

        public ForecastLine(string text, Color color, bool indent = false)
        {
            this.text = text;
            this.color = color;
            this.indent = indent;
        }
    }

    public class TraumaForecast
    {
        public HediffDef hediff;
        public float startMood;
        public float longMood;
        public float days;
        public bool permanent;
        public bool breakRisk;
    }

    // Builds the "psychological consequences" panel. All numbers are read from the Defs.
    public static class ConversionForecast
    {
        public static readonly Color Good = new Color(0.55f, 0.85f, 0.55f);
        public static readonly Color Bad = new Color(0.95f, 0.5f, 0.5f);
        public static readonly Color Neutral = new Color(0.9f, 0.9f, 0.9f);
        public static readonly Color Muted = new Color(0.65f, 0.65f, 0.65f);

        public static List<ForecastLine> Build(Pawn pawn, AssimilationConfig config)
        {
            List<ForecastLine> lines = new List<ForecastLine>();
            ConversionType type = ConversionUtility.Classify(pawn);
            bool voluntary = type == ConversionType.Voluntary;
            lines.Add(new ForecastLine((voluntary ? "IC_ForecastVoluntary" : "IC_ForecastForced").Translate(), voluntary ? Good : Bad));
            AddReason(lines, pawn);

            int degree = voluntary ? 1 : 2;
            Trait existing = pawn.story?.traits?.GetTrait(IC_DefOf.IC_Converted);
            if (existing != null && existing.Degree > degree)
            {
                degree = existing.Degree;
            }
            string traitLabel = IC_DefOf.IC_Converted.DataAtDegree(degree).label.CapitalizeFirst();
            lines.Add(new ForecastLine("IC_ForecastTrait".Translate(traitLabel), Neutral));

            HediffDef mainDef = voluntary ? IC_DefOf.IC_PhantomDiscomfort : IC_DefOf.IC_FormWashShock;
            ThoughtDef mainThought = voluntary ? IC_DefOf.IC_PhantomDiscomfortMood : IC_DefOf.IC_FormWashShockMood;
            TraumaForecast main = ForecastTrauma(mainDef, mainThought, type);
            List<TraumaForecast> all = new List<TraumaForecast> { main };
            lines.Add(new ForecastLine("IC_ForecastMain".Translate(mainDef.LabelCap), Bad));
            lines.Add(new ForecastLine(Describe(main), Muted, true));
            if (main.breakRisk)
            {
                lines.Add(new ForecastLine("IC_ForecastBreaks".Translate(), Bad, true));
            }

            bool genderChange = config.gender != Gender.None && pawn.gender != Gender.None && config.gender != pawn.gender;
            float extraStartMood = 0f;
            if (genderChange)
            {
                TraumaForecast shift = ForecastTrauma(IC_DefOf.IC_GenderShift, IC_DefOf.IC_GenderShiftMood, type);
                all.Add(shift);
                lines.Add(new ForecastLine("IC_ForecastExtra".Translate(), Neutral));
                lines.Add(new ForecastLine("IC_ForecastGenderShift".Translate(IC_DefOf.IC_GenderShift.LabelCap) + ": " + Describe(shift), Bad, true));
                ThoughtDef ideoThought = GenderShiftUtility.IdeoReaction(pawn, pawn.gender, config.gender);
                if (ideoThought != null)
                {
                    float ideoMood = ideoThought.stages[0].baseMoodEffect;
                    extraStartMood += ideoMood;
                    lines.Add(new ForecastLine(GenderShiftUtility.DescribeMemory(ideoThought), ideoMood < 0f ? Bad : Good, true));
                }
                foreach (Pawn partner in GenderShiftUtility.PartnersLosingAttraction(pawn, pawn.gender, config.gender))
                {
                    lines.Add(new ForecastLine(GenderShiftUtility.DescribePartner(partner), Bad, true));
                }
            }
            else
            {
                lines.Add(new ForecastLine("IC_ForecastNoExtra".Translate(), Muted));
            }

            if (RecoveryDays(out float comaDays, out float recoveryDays))
            {
                lines.Add(new ForecastLine("IC_ForecastRecovery".Translate(Mathf.RoundToInt(comaDays), Mathf.RoundToInt(recoveryDays)), Neutral));
            }

            lines.Add(new ForecastLine("IC_ForecastAgeChange".Translate(Mathf.FloorToInt(pawn.ageTracker.AgeBiologicalYearsFloat), Mathf.FloorToInt(config.ageYears)), Muted));

            float startTotal = all.Sum(t => t.startMood) + extraStartMood;
            float longTotal = all.Sum(t => t.longMood);
            lines.Add(new ForecastLine("IC_ForecastTotalStart".Translate(Signed(startTotal)), startTotal < 0f ? Bad : Neutral));
            lines.Add(new ForecastLine("IC_ForecastTotalLong".Translate(Signed(longTotal)), longTotal < 0f ? Bad : Good));

            List<ThoughtStage> disdain = IC_DefOf.IC_PurebloodDisdain.stages;
            if (!disdain.NullOrEmpty())
            {
                float opinion = disdain[Mathf.Min(voluntary ? 0 : 1, disdain.Count - 1)].baseOpinionOffset;
                lines.Add(new ForecastLine("IC_ForecastPurebloods".Translate(Signed(opinion)), Muted));
            }
            return lines;
        }

        public static TraumaForecast ForecastTrauma(HediffDef hediff, ThoughtDef thought, ConversionType type)
        {
            TraumaForecast result = new TraumaForecast { hediff = hediff };
            HediffCompProperties_FadeToFloor fade = hediff.CompProps<HediffCompProperties_FadeToFloor>();
            float start = hediff.initialSeverity;
            float floor = fade != null ? fade.FloorFor(type) : 0f;
            float perDay = fade != null ? Mathf.Abs(fade.severityPerDay) : 0f;
            result.permanent = floor > 0f || perDay <= 0f;
            result.days = perDay > 0f ? Mathf.Max(0f, start - floor) / perDay : 0f;
            result.startMood = MoodAt(hediff, thought, start);
            result.longMood = result.permanent ? MoodAt(hediff, thought, floor) : 0f;
            HediffStage stage = StageAt(hediff, start);
            result.breakRisk = stage != null && stage.mentalBreakMtbDays > 0f;
            return result;
        }

        // Coma and full recovery time after the pod, read from IC_AssimilationRecovery.
        public static bool RecoveryDays(out float comaDays, out float totalDays)
        {
            comaDays = 0f;
            totalDays = 0f;
            HediffDef def = IC_DefOf.IC_AssimilationRecovery;
            HediffCompProperties_SeverityPerDay props = def?.CompProps<HediffCompProperties_SeverityPerDay>();
            if (props == null || props.severityPerDay >= 0f)
            {
                return false;
            }
            float perDay = -props.severityPerDay;
            totalDays = def.initialSeverity / perDay;
            if (def.stages != null)
            {
                foreach (HediffStage stage in def.stages)
                {
                    if (stage.capMods != null && stage.capMods.Any(m => m.capacity == PawnCapacityDefOf.Consciousness && m.setMax <= 0.1f))
                    {
                        comaDays = Mathf.Max(comaDays, (def.initialSeverity - stage.minSeverity) / perDay);
                    }
                }
            }
            return true;
        }

        private static string Describe(TraumaForecast trauma)
        {
            if (trauma.permanent)
            {
                return "IC_ForecastPermanent".Translate(Signed(trauma.startMood), Signed(trauma.longMood), Mathf.CeilToInt(trauma.days));
            }
            return "IC_ForecastFades".Translate(Signed(trauma.startMood), Mathf.CeilToInt(trauma.days));
        }

        private static void AddReason(List<ForecastLine> lines, Pawn pawn)
        {
            if (pawn.IsPrisonerOfColony)
            {
                lines.Add(new ForecastLine("IC_ReasonPrisoner".Translate(), Muted, true));
                return;
            }
            if (pawn.IsSlaveOfColony)
            {
                lines.Add(new ForecastLine("IC_ReasonSlave".Translate(), Muted, true));
                if (!ModsConfig.IdeologyActive)
                {
                    lines.Add(new ForecastLine("IC_CondNoIdeology".Translate(), Bad, true));
                    return;
                }
                bool ideo = ConversionUtility.SharesColonyIdeo(pawn);
                lines.Add(new ForecastLine(Mark(ideo) + "IC_CondIdeo".Translate(), ideo ? Good : Bad, true));
                float suppression = ConversionUtility.Suppression(pawn);
                bool suppressed = suppression >= ConversionUtility.RequiredSuppression;
                lines.Add(new ForecastLine(Mark(suppressed) + "IC_CondSuppression".Translate(suppression.ToStringPercent(), ConversionUtility.RequiredSuppression.ToStringPercent()), suppressed ? Good : Bad, true));
                return;
            }
            if (pawn.IsFreeColonist)
            {
                lines.Add(new ForecastLine("IC_ReasonColonist".Translate(), Muted, true));
                return;
            }
            lines.Add(new ForecastLine("IC_ReasonOther".Translate(), Muted, true));
        }

        private static string Mark(bool ok)
        {
            return ok ? "(+) " : "(-) ";
        }

        private static string Signed(float value)
        {
            return value.ToString("+0;-0;0");
        }

        private static int StageIndexAt(HediffDef hediff, float severity)
        {
            if (hediff.stages.NullOrEmpty())
            {
                return -1;
            }
            int index = 0;
            for (int i = 0; i < hediff.stages.Count; i++)
            {
                if (severity >= hediff.stages[i].minSeverity)
                {
                    index = i;
                }
            }
            return index;
        }

        private static HediffStage StageAt(HediffDef hediff, float severity)
        {
            int index = StageIndexAt(hediff, severity);
            return index < 0 ? null : hediff.stages[index];
        }

        private static float MoodAt(HediffDef hediff, ThoughtDef thought, float severity)
        {
            int index = StageIndexAt(hediff, severity);
            if (index < 0 || thought?.stages == null || thought.stages.Count == 0)
            {
                return 0f;
            }
            ThoughtStage stage = thought.stages[Mathf.Min(index, thought.stages.Count - 1)];
            return stage != null ? stage.baseMoodEffect : 0f;
        }
    }
}
