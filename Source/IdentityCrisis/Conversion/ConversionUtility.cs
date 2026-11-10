using System.Linq;
using RimWorld;
using Verse;

namespace IdentityCrisis
{
    public static class ConversionUtility
    {
        public static float RequiredSuppression => IdentityCrisisMod.Settings?.convincedSlaveMinSuppression ?? 0.7f;

        public static AcceptanceReport CanBeAssimilated(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return "IC_RejectDead".Translate().Resolve();
            }
            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
            {
                return "IC_RejectNotHumanlike".Translate().Resolve();
            }
            if (!(pawn.IsFreeColonist || pawn.IsSlaveOfColony || pawn.IsPrisonerOfColony))
            {
                return "IC_RejectNotOurs".Translate().Resolve();
            }
            if (pawn.IsQuestLodger())
            {
                return "IC_RejectQuestLodger".Translate().Resolve();
            }
            if (!pawn.DevelopmentalStage.Adult())
            {
                return "IC_RejectNotAdult".Translate().Resolve();
            }
            if (pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Pregnant))
            {
                return "IC_RejectPregnant".Translate().Resolve();
            }
            return true;
        }

        // Decided when the pawn enters the pod.
        public static ConversionType Classify(Pawn pawn)
        {
            if (pawn.IsPrisonerOfColony)
            {
                return ConversionType.Forced;
            }
            if (pawn.IsSlaveOfColony)
            {
                return IsConvincedSlave(pawn) ? ConversionType.Voluntary : ConversionType.Forced;
            }
            if (pawn.IsFreeColonist)
            {
                return ConversionType.Voluntary;
            }
            return ConversionType.Forced;
        }

        public static bool SharesColonyIdeo(Pawn pawn)
        {
            if (!ModsConfig.IdeologyActive)
            {
                return false;
            }
            Ideo primary = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            return primary != null && pawn.Ideo == primary;
        }

        public static float Suppression(Pawn pawn)
        {
            if (!ModsConfig.IdeologyActive)
            {
                return 0f;
            }
            Need_Suppression need = pawn.needs?.TryGetNeed<Need_Suppression>();
            return need?.CurLevelPercentage ?? 0f;
        }

        public static bool IsConvincedSlave(Pawn pawn)
        {
            return ModsConfig.IdeologyActive && SharesColonyIdeo(pawn) && Suppression(pawn) >= RequiredSuppression;
        }

        public static Hediff_ConversionRecord GetRecord(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(IC_DefOf.IC_ConversionRecord) as Hediff_ConversionRecord;
        }

        public static bool IsConverted(Pawn pawn)
        {
            return GetRecord(pawn) != null;
        }

        public static bool IsPureblood(Pawn pawn)
        {
            return pawn?.RaceProps != null && pawn.RaceProps.Humanlike && !IsConverted(pawn);
        }

        // Called right after the body has been rebuilt and the pawn has left the pod.
        // Every step runs on its own: if one fails, the rest still apply.
        // Labels of what was added go to report.consequences (the letter and the Log tab).
        public static void ApplyConversion(Pawn pawn, ConversionType type, ThingDef originalRace, Gender originalGender, AssimilationReport report = null, float recoveryMultiplier = 1f)
        {
            Hediff_ConversionRecord record = GetRecord(pawn);
            bool isNew = record == null;
            if (isNew)
            {
                record = (Hediff_ConversionRecord)HediffMaker.MakeHediff(IC_DefOf.IC_ConversionRecord, pawn);
                record.originalRace = originalRace;
                record.originalGender = originalGender;
            }
            if (isNew || type == ConversionType.Forced)
            {
                record.conversionType = type;
            }
            record.timesConverted++;
            record.tickConverted = Find.TickManager.TicksGame;
            if (isNew)
            {
                Step("record", pawn, () => pawn.health.AddHediff(record));
            }

            Step("trait", pawn, () => ApplyTrait(pawn, record.conversionType));

            if (type == ConversionType.Forced)
            {
                Step("trauma", pawn, () => Note(report, AddTrauma(pawn, IC_DefOf.IC_FormWashShock, type)));
            }
            else
            {
                Step("trauma", pawn, () => Note(report, AddTrauma(pawn, IC_DefOf.IC_PhantomDiscomfort, type)));
            }

            if (originalGender != Gender.None && pawn.gender != Gender.None && originalGender != pawn.gender)
            {
                Step("gender shift", pawn, () =>
                {
                    Hediff shift = AddTrauma(pawn, IC_DefOf.IC_GenderShift, type);
                    if (shift is Hediff_GenderShift genderShift)
                    {
                        // Remember what was changed into what: the health tab shows it.
                        genderShift.fromGender = originalGender;
                        genderShift.toGender = pawn.gender;
                    }
                    Note(report, shift);
                });
                Step("gender reactions", pawn, () => GenderShiftUtility.ApplyReactions(pawn, originalGender, pawn.gender, report));
            }

            Step("identity shift", pawn, () => Note(report, IdentityShiftUtility.Start(pawn, type)));

            // Last, because the coma downs the pawn right away.
            Step("recovery", pawn, () =>
            {
                Hediff recovery = AddTrauma(pawn, IC_DefOf.IC_AssimilationRecovery, type);
                HediffComp_FadeToFloor fade = recovery.TryGetComp<HediffComp_FadeToFloor>();
                if (fade != null) fade.durationMultiplier = UnityEngine.Mathf.Clamp(recoveryMultiplier, 0.25f, 4f);
                Note(report, recovery);
            });
            Step("records", pawn, () => pawn.records?.Increment(RecordDefOf.OperationsReceived));
        }

        // A failed step is logged and skipped instead of aborting the whole conversion.
        private static void Step(string name, Pawn pawn, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                Log.Error("[Identity Crisis] Conversion step '" + name + "' failed for " + pawn + ": " + e);
            }
        }

        private static void Note(AssimilationReport report, Hediff hediff)
        {
            if (report != null && hediff != null)
            {
                report.consequences.Add(hediff.LabelCap);
            }
        }

        private static void ApplyTrait(Pawn pawn, ConversionType type)
        {
            if (pawn.story?.traits == null)
            {
                return;
            }
            int degree = type == ConversionType.Forced ? 2 : 1;
            Trait existing = pawn.story.traits.GetTrait(IC_DefOf.IC_Converted);
            if (existing != null)
            {
                if (existing.Degree >= degree)
                {
                    return;
                }
                pawn.story.traits.RemoveTrait(existing);
            }
            pawn.story.traits.GainTrait(new Trait(IC_DefOf.IC_Converted, degree, true));
        }

        private static Hediff AddTrauma(Pawn pawn, HediffDef def, ConversionType type)
        {
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }
            Hediff hediff = HediffMaker.MakeHediff(def, pawn);
            HediffComp_FadeToFloor fade = hediff.TryGetComp<HediffComp_FadeToFloor>();
            if (fade != null)
            {
                fade.conversionType = type;
            }
            pawn.health.AddHediff(hediff);
            return hediff;
        }
    }
}
