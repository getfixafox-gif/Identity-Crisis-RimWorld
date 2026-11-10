using RimWorld;
using Verse;

namespace IdentityCrisis
{
    // Phase 2 of the identity crisis: the convert feels like a stranger next to pawns of their new race.
    public class ThoughtWorker_AlienAmongThem : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (IdentityShiftUtility.Stage(p) != IdentityShiftUtility.StageAlienation)
            {
                return false;
            }
            float radius = def.GetModExtension<IdentityShiftExtension>()?.radius ?? 9f;
            return IdentityShiftUtility.AnyNearby(p, p.def, radius);
        }
    }
}
