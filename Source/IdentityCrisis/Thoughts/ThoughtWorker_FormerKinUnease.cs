using RimWorld;
using Verse;

namespace IdentityCrisis
{
    // Phase 3 of the identity crisis: the new body is "mine" now, and the former race feels alien.
    public class ThoughtWorker_FormerKinUnease : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (IdentityShiftUtility.Stage(p) != IdentityShiftUtility.StageAcceptance)
            {
                return false;
            }
            Hediff_ConversionRecord record = ConversionUtility.GetRecord(p);
            if (record?.originalRace == null || record.originalRace == p.def)
            {
                return false;
            }
            float radius = def.GetModExtension<IdentityShiftExtension>()?.radius ?? 9f;
            return IdentityShiftUtility.AnyNearby(p, record.originalRace, radius);
        }
    }
}
