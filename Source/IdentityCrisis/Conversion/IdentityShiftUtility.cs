using RimWorld;
using Verse;

namespace IdentityCrisis
{
    // "Identity crisis" after an assimilation, kept as stages of IC_IdentityShift (severity grows every day):
    // 0 rage (forced only) -> 1 alienation among the new race -> 2 acceptance, after which the former race feels alien.
    // Timings and mood numbers live in XML.
    public static class IdentityShiftUtility
    {
        public const int StageRage = 0;
        public const int StageAlienation = 1;
        public const int StageAcceptance = 2;

        // Restarts the crisis on every conversion. Voluntary converts skip the rage phase.
        public static Hediff Start(Pawn pawn, ConversionType type)
        {
            HediffDef def = IC_DefOf.IC_IdentityShift;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }
            Hediff hediff = HediffMaker.MakeHediff(def, pawn);
            if (type == ConversionType.Voluntary)
            {
                hediff.Severity = def.GetModExtension<IdentityShiftExtension>()?.voluntaryStartSeverity ?? 0.13f;
            }
            pawn.health.AddHediff(hediff);
            return hediff;
        }

        // -1 when the pawn has no identity crisis.
        public static int Stage(Pawn pawn)
        {
            Hediff hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(IC_DefOf.IC_IdentityShift);
            return hediff == null ? -1 : hediff.CurStageIndex;
        }

        // Is any other humanlike of this race close by? Indoors only the same room counts.
        public static bool AnyNearby(Pawn pawn, ThingDef race, float radius)
        {
            if (pawn == null || race == null || !pawn.Spawned || pawn.Map == null)
            {
                return false;
            }
            float radiusSquared = radius * radius;
            Room room = pawn.GetRoom();
            bool indoors = room != null && !room.PsychologicallyOutdoors;
            foreach (Pawn other in pawn.Map.mapPawns.AllHumanlikeSpawned)
            {
                if (other == pawn || other.Dead || other.def != race)
                {
                    continue;
                }
                if ((other.Position - pawn.Position).LengthHorizontalSquared > radiusSquared)
                {
                    continue;
                }
                if (indoors && other.GetRoom() != room)
                {
                    continue;
                }
                return true;
            }
            return false;
        }
    }

    // Balance knobs for the identity crisis: put on the hediff (voluntaryStartSeverity)
    // and on the proximity thoughts (radius).
    public class IdentityShiftExtension : DefModExtension
    {
        public float voluntaryStartSeverity = 0.13f;
        public float radius = 9f;
    }
}
