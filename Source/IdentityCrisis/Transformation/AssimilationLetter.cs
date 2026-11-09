using System.Text;
using RimWorld;
using Verse;

namespace IdentityCrisis
{
    // The letter sent when the pod finishes: what was changed into what and every consequence.
    // Letters stay in the History tab, so the operation doesn't pass without a trace.
    public static class AssimilationLetter
    {
        public static void Send(Pawn pawn, ConversionType type, AssimilationReport report)
        {
            if (pawn == null)
            {
                return;
            }
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("IC_Finished".Translate(pawn.LabelShort, pawn.def.label).Resolve());
                if (report != null)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.Append("IC_LetterChanges".Translate(report.DescribeChanges()).Resolve());
                    if (report.consequences.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine();
                        sb.Append("IC_LetterConsequences".Translate().Resolve());
                        foreach (string line in report.consequences)
                        {
                            sb.AppendLine();
                            sb.Append("  - " + line);
                        }
                    }
                }
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("IC_LetterSeeTabs".Translate().Resolve());
                LetterDef letterDef = type == ConversionType.Forced ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent;
                Find.LetterStack.ReceiveLetter("IC_LetterLabel".Translate(pawn.LabelShort), sb.ToString(), letterDef, pawn);
            }
            catch (System.Exception e)
            {
                Log.Error("[Identity Crisis] Failed to send the assimilation letter: " + e);
                Messages.Message("IC_Finished".Translate(pawn.LabelShort, pawn.def.label), pawn, MessageTypeDefOf.PositiveEvent, true);
            }
        }
    }
}
