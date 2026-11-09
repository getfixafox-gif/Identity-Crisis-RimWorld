using RimWorld;
using Verse;

namespace IdentityCrisis
{
    // Opinion of a pureblood about a convert of their own race.
    public class ThoughtWorker_PurebloodDisdain : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            if (p.RaceProps == null || !p.RaceProps.Humanlike || otherPawn?.RaceProps == null || !otherPawn.RaceProps.Humanlike)
            {
                return false;
            }
            if (p.def != otherPawn.def || !ConversionUtility.IsPureblood(p))
            {
                return false;
            }
            Hediff_ConversionRecord record = ConversionUtility.GetRecord(otherPawn);
            if (record == null || !RelationsUtility.PawnsKnowEachOther(p, otherPawn))
            {
                return false;
            }
            return ThoughtState.ActiveAtStage(record.conversionType == ConversionType.Forced ? 1 : 0);
        }
    }
}
