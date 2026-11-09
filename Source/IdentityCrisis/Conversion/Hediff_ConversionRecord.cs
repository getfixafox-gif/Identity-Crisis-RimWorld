using System.Text;
using Verse;

namespace IdentityCrisis
{
    // Permanent record of a conversion, shown in the health tab. Source of truth for converts vs purebloods.
    public class Hediff_ConversionRecord : HediffWithComps
    {
        public ThingDef originalRace;
        public Gender originalGender;
        public ConversionType conversionType;
        public int tickConverted;
        public int timesConverted;

        // "human → wolfein" next to the label.
        public override string LabelInBrackets
        {
            get
            {
                if (originalRace == null || pawn?.def == null || originalRace == pawn.def)
                {
                    return base.LabelInBrackets;
                }
                return originalRace.label + " → " + pawn.def.label;
            }
        }

        public override string TipStringExtra
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                string baseExtra = base.TipStringExtra;
                if (!baseExtra.NullOrEmpty())
                {
                    sb.AppendLine(baseExtra.TrimEndNewlines());
                }
                sb.AppendLine((conversionType == ConversionType.Forced ? "IC_ForecastForced" : "IC_ForecastVoluntary").Translate().Resolve());
                if (originalRace != null)
                {
                    sb.AppendLine("IC_RecordOriginalRace".Translate(originalRace.label).Resolve());
                }
                if (originalGender != Gender.None)
                {
                    sb.AppendLine("IC_RecordOriginalGender".Translate(originalGender.GetLabel()).Resolve());
                }
                sb.AppendLine("IC_RecordWhen".Translate((Find.TickManager.TicksGame - tickConverted).ToStringTicksToPeriod()).Resolve());
                if (timesConverted > 1)
                {
                    sb.AppendLine("IC_RecordTimes".Translate(timesConverted).Resolve());
                }
                return sb.ToString().TrimEndNewlines();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref originalRace, "originalRace");
            Scribe_Values.Look(ref originalGender, "originalGender", Gender.None);
            Scribe_Values.Look(ref conversionType, "conversionType", ConversionType.Voluntary);
            Scribe_Values.Look(ref tickConverted, "tickConverted", 0);
            Scribe_Values.Look(ref timesConverted, "timesConverted", 0);
        }
    }
}
