using RimWorld;
using Verse;
using Verse.AI;

namespace IdentityCrisis
{
    public class WorkGiver_CarryToAssimilationPod : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(IC_DefOf.IC_AssimilationPod);

        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return pawn.Map.listerThings.ThingsOfDef(IC_DefOf.IC_AssimilationPod).Count == 0;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_AssimilationPod pod) || pod.Occupant != null)
            {
                return false;
            }
            Pawn takee = pod.PendingPawn;
            if (takee == null || takee == pawn || !pod.NeedsCarrying(takee))
            {
                return false;
            }
            if (!takee.Spawned || takee.Map != pawn.Map || t.IsForbidden(pawn))
            {
                return false;
            }
            if (!pod.CanAcceptPawn(takee).Accepted)
            {
                return false;
            }
            if (!pawn.CanReserve(takee, 1, -1, null, forced) || !pawn.CanReserve(pod, 1, -1, null, forced))
            {
                return false;
            }
            return pawn.CanReach(takee, PathEndMode.OnCell, Danger.Deadly) && pawn.CanReach(pod, PathEndMode.InteractionCell, Danger.Deadly);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AssimilationPod pod = (Building_AssimilationPod)t;
            Job job = JobMaker.MakeJob(IC_DefOf.IC_CarryToAssimilationPod, pod.PendingPawn, pod);
            job.count = 1;
            return job;
        }
    }
}
