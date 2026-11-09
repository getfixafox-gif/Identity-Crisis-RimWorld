using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    // A line in the pawn's Log tab (shown with social entries) about a finished assimilation.
    // The line names the type and the main changes; the tooltip lists every procedure.
    public class PlayLogEntry_Assimilation : LogEntry
    {
        private Pawn pawn;
        private ConversionType type;
        private ThingDef fromRace;
        private ThingDef toRace;
        private Gender fromGender;
        private Gender toGender;
        private float fromAgeYears;
        private float toAgeYears;
        private List<string> implantsMoved = new List<string>();
        private List<string> implantsExtracted = new List<string>();
        private List<string> implantsLost = new List<string>();
        private List<string> healed = new List<string>();
        private List<string> genesRemoved = new List<string>();
        private List<string> gearRemoved = new List<string>();
        private List<string> consequences = new List<string>();

        public PlayLogEntry_Assimilation()
        {
        }

        public PlayLogEntry_Assimilation(Pawn pawn, ConversionType type, AssimilationReport report)
        {
            this.pawn = pawn;
            this.type = type;
            fromRace = report.fromRace;
            toRace = report.toRace;
            fromGender = report.fromGender;
            toGender = report.toGender;
            fromAgeYears = report.fromAgeYears;
            toAgeYears = report.toAgeYears;
            implantsMoved.AddRange(report.implantsMoved);
            implantsExtracted.AddRange(report.implantsExtracted);
            implantsLost.AddRange(report.implantsLost);
            healed.AddRange(report.healed);
            genesRemoved.AddRange(report.genesRemoved);
            gearRemoved.AddRange(report.gearRemoved);
            consequences.AddRange(report.consequences);
        }

        public static void TryAdd(Pawn pawn, ConversionType type, AssimilationReport report)
        {
            if (pawn == null || report == null || Find.PlayLog == null)
            {
                return;
            }
            try
            {
                Find.PlayLog.Add(new PlayLogEntry_Assimilation(pawn, type, report));
            }
            catch (Exception e)
            {
                Log.Error("[Identity Crisis] Failed to add the assimilation to the play log: " + e);
            }
        }

        public override bool Concerns(Thing t)
        {
            return t != null && t == pawn;
        }

        public override IEnumerable<Thing> GetConcerns()
        {
            if (pawn != null)
            {
                yield return pawn;
            }
        }

        public override Texture2D IconFromPOV(Thing pov)
        {
            return IC_Textures.Assimilate;
        }

        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
        {
            string name = pawn != null ? pawn.LabelShort : "?";
            string key = type == ConversionType.Voluntary ? "IC_LogVoluntary" : "IC_LogForced";
            return key.Translate(name, Changes()).Resolve();
        }

        public override string GetTipString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(base.GetTipString());
            AppendSection(sb, "IC_LogConsequences", consequences);
            bool any = false;
            any |= AppendSection(sb, "IC_LogImplantsMoved", implantsMoved);
            any |= AppendSection(sb, "IC_LogImplantsExtracted", implantsExtracted);
            any |= AppendSection(sb, "IC_LogImplantsLost", implantsLost);
            any |= AppendSection(sb, "IC_LogHealed", healed);
            any |= AppendSection(sb, "IC_LogGenesRemoved", genesRemoved);
            any |= AppendSection(sb, "IC_LogGearRemoved", gearRemoved);
            if (!any)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("IC_LogNothing".Translate().Resolve());
            }
            return sb.ToString();
        }

        private string Changes()
        {
            List<string> parts = new List<string>();
            string oldRaceLabel = fromRace != null ? fromRace.label : "?";
            string newRaceLabel = toRace != null ? toRace.label : "?";
            parts.Add("IC_LogRace".Translate(oldRaceLabel, newRaceLabel).Resolve());
            if (fromGender != toGender)
            {
                parts.Add("IC_LogGender".Translate(fromGender.GetLabel(), toGender.GetLabel()).Resolve());
            }
            int oldAge = Mathf.FloorToInt(fromAgeYears);
            int newAge = Mathf.FloorToInt(toAgeYears);
            if (oldAge != newAge)
            {
                parts.Add("IC_LogAge".Translate(oldAge.ToString(), newAge.ToString()).Resolve());
            }
            return string.Join(", ", parts);
        }

        private static bool AppendSection(StringBuilder sb, string key, List<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return false;
            }
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(key.Translate().Resolve());
            foreach (string item in items)
            {
                sb.AppendLine();
                sb.Append("  - " + item);
            }
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_Values.Look(ref type, "type", ConversionType.Voluntary);
            Scribe_Defs.Look(ref fromRace, "fromRace");
            Scribe_Defs.Look(ref toRace, "toRace");
            Scribe_Values.Look(ref fromGender, "fromGender", Gender.None);
            Scribe_Values.Look(ref toGender, "toGender", Gender.None);
            Scribe_Values.Look(ref fromAgeYears, "fromAgeYears", 0f);
            Scribe_Values.Look(ref toAgeYears, "toAgeYears", 0f);
            Scribe_Collections.Look(ref implantsMoved, "implantsMoved", LookMode.Value);
            Scribe_Collections.Look(ref implantsExtracted, "implantsExtracted", LookMode.Value);
            Scribe_Collections.Look(ref implantsLost, "implantsLost", LookMode.Value);
            Scribe_Collections.Look(ref healed, "healed", LookMode.Value);
            Scribe_Collections.Look(ref genesRemoved, "genesRemoved", LookMode.Value);
            Scribe_Collections.Look(ref gearRemoved, "gearRemoved", LookMode.Value);
            Scribe_Collections.Look(ref consequences, "consequences", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                implantsMoved = implantsMoved ?? new List<string>();
                implantsExtracted = implantsExtracted ?? new List<string>();
                implantsLost = implantsLost ?? new List<string>();
                healed = healed ?? new List<string>();
                genesRemoved = genesRemoved ?? new List<string>();
                gearRemoved = gearRemoved ?? new List<string>();
                consequences = consequences ?? new List<string>();
            }
        }
    }
}
