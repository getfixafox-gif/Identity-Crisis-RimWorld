using System.Collections.Generic;
using RimWorld;
using Verse;

namespace IdentityCrisis
{
    // Small mood bonus for purebloods while converts of their race live in the colony.
    public class ThoughtWorker_PurebloodPride : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!p.Spawned || p.Map == null || p.Faction != Faction.OfPlayer || !ConversionUtility.IsPureblood(p))
            {
                return false;
            }
            MapPawns mapPawns = p.Map.mapPawns;
            return AnyConvertOfRace(mapPawns.FreeColonistsAndPrisonersSpawned, p) || AnyConvertOfRace(mapPawns.SlavesOfColonySpawned, p);
        }

        private static bool AnyConvertOfRace(IEnumerable<Pawn> pawns, Pawn observer)
        {
            if (pawns == null)
            {
                return false;
            }
            foreach (Pawn other in pawns)
            {
                if (other != observer && other.def == observer.def && ConversionUtility.IsConverted(other))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
